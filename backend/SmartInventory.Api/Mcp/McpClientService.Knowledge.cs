using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using SmartInventory.Application.Common;
using SmartInventory.Application.Mcp;

namespace SmartInventory.Api.Mcp;

public sealed partial class McpClientService
{
    /// <summary>Discovers direct resources using the official SDK; absence of the optional capability yields an empty list.</summary>
    public Task<IReadOnlyList<McpResourceDescriptor>> ListResourcesAsync(CancellationToken cancellationToken = default) =>
        WithKnowledgeClientAsync<IReadOnlyList<McpResourceDescriptor>>(async (client, token) =>
        {
            if (client.ServerCapabilities.Resources is null) return [];
            var resources = await client.ListResourcesAsync(cancellationToken: token);
            return resources.Where(resource => McpKnowledgePolicy.IsResourceAllowed(resource.Uri))
                .Select(resource => new McpResourceDescriptor(resource.Uri, resource.Name, resource.Description, resource.MimeType)).ToList();
        }, cancellationToken);

    /// <summary>Reads only first-party documentation URIs; no local files, arbitrary URLs or configuration are exposed.</summary>
    public Task<JsonElement> ReadResourceAsync(string uri, CancellationToken cancellationToken = default)
    {
        if (!McpKnowledgePolicy.IsResourceAllowed(uri))
            throw new AppValidationException("The requested MCP documentation resource is not permitted.");
        return WithKnowledgeClientAsync(async (client, token) =>
        {
            if (client.ServerCapabilities.Resources is null)
                throw new AppServiceUnavailableException("This MCP server does not advertise resources.");
            var result = await client.ReadResourceAsync(uri, cancellationToken: token);
            return JsonSerializer.SerializeToElement(result, McpJsonUtilities.DefaultOptions);
        }, cancellationToken);
    }

    /// <summary>Lists reusable templates through ListPromptsAsync; does not invoke a language model.</summary>
    public Task<IReadOnlyList<McpPromptDescriptor>> ListPromptsAsync(CancellationToken cancellationToken = default) =>
        WithKnowledgeClientAsync<IReadOnlyList<McpPromptDescriptor>>(async (client, token) =>
        {
            if (client.ServerCapabilities.Prompts is null) return [];
            var prompts = await client.ListPromptsAsync(cancellationToken: token);
            return prompts.Where(prompt => McpKnowledgePolicy.IsPromptAllowed(prompt.Name))
                .Select(prompt => new McpPromptDescriptor(prompt.Name, prompt.Description,
                    prompt.ProtocolPrompt.Arguments?.Select(argument => new McpPromptArgument(
                        argument.Name, argument.Description, argument.Required is true)).ToList() ?? [])).ToList();
        }, cancellationToken);

    /// <summary>Retrieves a prompt template through GetPromptAsync with bounded string arguments.</summary>
    public Task<JsonElement> GetPromptAsync(string name, JsonElement arguments, CancellationToken cancellationToken = default)
    {
        if (!McpKnowledgePolicy.IsPromptAllowed(name)) throw new AppValidationException("The requested MCP prompt is not permitted.");
        if (arguments.ValueKind != JsonValueKind.Object || arguments.GetRawText().Length > 1024)
            throw new AppValidationException("Prompt arguments must be a JSON object of at most 1024 characters.");
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var argument in arguments.EnumerateObject())
        {
            if (name != McpKnowledgePolicy.AnalyzeLowStock || argument.Name != "horizonDays" ||
                argument.Value.ValueKind != JsonValueKind.String || !values.TryAdd(argument.Name, argument.Value.GetString()))
                throw new AppValidationException("Only the AnalyzeLowStock horizonDays string argument is accepted.");
        }
        if (values.TryGetValue("horizonDays", out var horizon) &&
            (!int.TryParse((string?)horizon, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var days) || days is < 1 or > 90))
            throw new AppValidationException("horizonDays must be an integer string between 1 and 90.");
        return WithKnowledgeClientAsync(async (client, token) =>
        {
            if (client.ServerCapabilities.Prompts is null)
                throw new AppServiceUnavailableException("This MCP server does not advertise prompts.");
            var result = await client.GetPromptAsync(name, values, cancellationToken: token);
            return JsonSerializer.SerializeToElement(result, McpJsonUtilities.DefaultOptions);
        }, cancellationToken);
    }

    private async Task<T> WithKnowledgeClientAsync<T>(Func<McpClient, CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        try { return await WithClientAsync(operation, cancellationToken); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new AppServiceUnavailableException("The MCP documentation request timed out."); }
        catch (Exception exception) when (exception is HttpRequestException or McpException or IOException)
        { throw new AppServiceUnavailableException("The MCP server could not provide the requested documentation."); }
    }
}
