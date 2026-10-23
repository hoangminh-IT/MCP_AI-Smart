using System.Buffers;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;

namespace SmartInventory.Infrastructure.Catalog;

public sealed record ProductImageStorageOptions(string RootPath);

public sealed class ProductImageService(
    IProductService products,
    ProductImageStorageOptions storage) : IProductImageService
{
    private const int MaximumLength = 5 * 1024 * 1024;

    public async Task<ProductImageResult> UploadAsync(int productId, Stream stream,
        string fileName, string contentType, long length, CancellationToken cancellationToken)
    {
        if (length is <= 0 or > MaximumLength)
            throw InvalidImage("Choose an image between 1 byte and 5 MiB.");

        var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
        var expectedType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => throw InvalidImage("Only JPG, PNG and WebP images are supported.")
        };
        if (!string.Equals(expectedType, contentType?.Trim(), StringComparison.OrdinalIgnoreCase))
            throw InvalidImage("The image content type must match its file extension.");
        if (stream is null || !stream.CanRead)
            throw InvalidImage("The image content could not be read.");

        await products.GetProductAsync(productId, cancellationToken);
        using var content = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            while (true)
            {
                // Read at most one byte over the limit, including when the reported length is false.
                var read = await stream.ReadAsync(
                    buffer.AsMemory(0, Math.Min(buffer.Length, MaximumLength + 1 - (int)content.Length)),
                    cancellationToken);
                if (read == 0)
                    break;
                await content.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                if (content.Length > MaximumLength)
                    throw InvalidImage("The image must be no larger than 5 MiB.");
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }

        if (content.Length != length)
            throw InvalidImage("The image length does not match the uploaded content.");
        if (!HasExpectedSignature(content.GetBuffer().AsSpan(0, (int)content.Length), expectedType))
            throw InvalidImage("The file does not contain a supported image matching its extension.");

        var storedExtension = expectedType == "image/jpeg" ? ".jpg" : extension;
        var generatedName = Guid.NewGuid().ToString("N") + storedExtension;
        var imageUrl = "/uploads/products/" + generatedName;
        Directory.CreateDirectory(storage.RootPath);
        var destinationPath = Path.Combine(storage.RootPath, generatedName);
        var createdFile = false;

        try
        {
            await using (var destination = new FileStream(destinationPath, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                createdFile = true;
                content.Position = 0;
                await content.CopyToAsync(destination, cancellationToken);
            }
            await products.SetProductImageAsync(productId, imageUrl, cancellationToken);
        }
        catch
        {
            // Only this request's generated file is eligible for cleanup; never follow a stored URL.
            try
            {
                if (createdFile)
                    File.Delete(destinationPath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            throw;
        }

        return new ProductImageResult(imageUrl);
    }

    private static bool HasExpectedSignature(ReadOnlySpan<byte> bytes, string contentType) =>
        contentType switch
        {
            "image/png" => bytes.Length >= 8 &&
                bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            "image/jpeg" => bytes.Length >= 3 &&
                bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            "image/webp" => bytes.Length >= 12 &&
                bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };

    private static AppValidationException InvalidImage(string message) =>
        new("Product image validation failed.",
            new Dictionary<string, string[]> { ["image"] = [message] });
}
