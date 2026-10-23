using System.Globalization;
using System.Text;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;

namespace SmartInventory.Infrastructure.Catalog;

public sealed class ProductCsvService(IProductService products) : IProductCsvService
{
    private const int MaximumBytes = 2 * 1024 * 1024;

    public async Task<byte[]> ExportAsync(CatalogQuery query, CancellationToken cancellationToken)
    {
        var rows = await products.ExportProductsAsync(query, cancellationToken);
        var output = new StringBuilder("id,sku,name,categoryId,categoryName,description,price,costPrice,minStock,imageUrl,isActive,quantity,csvFormat\r\n");
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            output.AppendJoin(',', row.Id.ToString(CultureInfo.InvariantCulture), Cell(row.Sku), Cell(row.Name),
                row.CategoryId.ToString(CultureInfo.InvariantCulture), Cell(row.CategoryName), Cell(row.Description),
                row.Price.ToString("0.00", CultureInfo.InvariantCulture), row.CostPrice.ToString("0.00", CultureInfo.InvariantCulture),
                row.MinStock.ToString(CultureInfo.InvariantCulture), Cell(row.ImageUrl ?? string.Empty),
                row.IsActive ? "true" : "false", row.Quantity.ToString(CultureInfo.InvariantCulture), "smart-inventory-v1");
            output.Append("\r\n");
        }
        var encoding = new UTF8Encoding(true);
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(output.ToString())];
    }

    public async Task<ProductImportResult> ImportAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var content = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (content.Length + count > MaximumBytes)
                throw new AppValidationException("CSV file must be no larger than 2 MiB.");
            await content.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }
        content.Position = 0;
        string text;
        try
        {
            using var reader = new StreamReader(content, new UTF8Encoding(false, true), true);
            text = await reader.ReadToEndAsync(cancellationToken);
        }
        catch (DecoderFallbackException)
        {
            throw new AppValidationException("CSV file must contain valid UTF-8 text.");
        }

        var records = Parse(text);
        if (records.Count < 2)
            throw new AppValidationException("CSV must include a header and at least one product row.");
        var headers = records[0].Select(header => header.Trim()).ToArray();
        if (headers.Any(string.IsNullOrEmpty) || headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Length)
            throw new AppValidationException("CSV headers must be nonempty and unique.");
        var columns = headers.Select((name, index) => (name, index))
            .ToDictionary(column => column.name, column => column.index, StringComparer.OrdinalIgnoreCase);
        foreach (var required in new[] { "sku", "name", "categoryId", "description", "price", "costPrice", "minStock", "imageUrl" })
            if (!columns.ContainsKey(required))
                throw new AppValidationException($"CSV is missing required column '{required}'.");

        var errors = new Dictionary<string, string[]>();
        var requests = new List<ProductRequest>();
        for (var index = 1; index < records.Count; index++)
        {
            var row = records[index];
            if (row.Count != headers.Length)
            {
                errors[$"rows[{index}]"] = ["Column count does not match the header."];
                continue;
            }
            var isExport = columns.TryGetValue("csvFormat", out var marker) && row[marker] == "smart-inventory-v1";
            if (columns.ContainsKey("csvFormat") && !isExport)
            {
                errors[$"rows[{index}].csvFormat"] = ["CSV format marker is not supported."];
                continue;
            }
            string Get(string name)
            {
                var value = columns.TryGetValue(name, out var column) ? row[column] : string.Empty;
                if (isExport && value.StartsWith('\'')) value = value[1..];
                return value.Trim();
            }
            if (!int.TryParse(Get("categoryId"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var categoryId) ||
                !decimal.TryParse(Get("price"), NumberStyles.Number, CultureInfo.InvariantCulture, out var price) ||
                !decimal.TryParse(string.IsNullOrEmpty(Get("costPrice")) ? "0" : Get("costPrice"), NumberStyles.Number, CultureInfo.InvariantCulture, out var costPrice) ||
                !int.TryParse(string.IsNullOrEmpty(Get("minStock")) ? "10" : Get("minStock"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minStock))
            {
                errors[$"rows[{index}]"] = ["categoryId/minStock must be integers and price/costPrice must use an invariant decimal format."];
                continue;
            }
            requests.Add(new ProductRequest
            {
                Sku = Get("sku"), Name = Get("name"), CategoryId = categoryId, Description = Get("description"),
                Price = price, CostPrice = costPrice, MinStock = minStock,
                ImageUrl = string.IsNullOrEmpty(Get("imageUrl")) ? null : Get("imageUrl")
            });
        }
        if (errors.Count > 0)
            throw new AppValidationException("CSV validation failed. No products were changed.", errors);
        return await products.ImportProductsAsync(requests, cancellationToken);
    }

    private static string Cell(string value)
    {
        // Prevent spreadsheet formulas when an exported string starts with a formula marker.
        if (value.Length > 0 && ("'=+-@\t\r\n".Contains(value[0]) ||
            value.TrimStart().FirstOrDefault() is '=' or '+' or '-' or '@'))
            value = "'" + value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static List<List<string>> Parse(string text)
    {
        var records = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var closedQuote = false;
        var started = false;

        void EndField()
        {
            row.Add(field.ToString());
            field.Clear();
            closedQuote = false;
            started = false;
        }
        void EndRow()
        {
            EndField();
            if (row.Any(value => value.Length != 0)) records.Add(row);
            row = [];
            if (records.Count > 1001)
                throw new AppValidationException("CSV may contain at most 1,000 product rows.");
        }

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (quoted)
            {
                if (character == '"')
                {
                    if (index + 1 < text.Length && text[index + 1] == '"') { field.Append('"'); index++; }
                    else { quoted = false; closedQuote = true; }
                }
                else field.Append(character);
            }
            else if (character == ',') EndField();
            else if (character is '\r' or '\n')
            {
                EndRow();
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n') index++;
            }
            else if (closedQuote)
                throw new AppValidationException("Unexpected text after a closing CSV quote.");
            else if (character == '"')
            {
                if (started) throw new AppValidationException("CSV quotes must enclose an entire field.");
                quoted = true;
                started = true;
            }
            else { field.Append(character); started = true; }

            if (field.Length > 16000)
                throw new AppValidationException("CSV field exceeds the maximum supported length.");
        }
        if (quoted) throw new AppValidationException("CSV contains an unterminated quoted field.");
        if (started || closedQuote || row.Count > 0 || field.Length > 0) EndRow();
        return records;
    }
}
