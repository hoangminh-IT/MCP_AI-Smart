using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.Mcp;

namespace SmartInventory.Api.Controllers;

public sealed class McpPromptRequest
{
    [JsonRequired] public JsonElement Arguments { get; set; }
}

[ApiController]
[Route("api/mcp")]
[Authorize(Roles = "Admin,Staff")]
public sealed class McpKnowledgeController(IMcpKnowledgeClient client) : ControllerBase
{
    [HttpGet("resources")]
    public Task<IReadOnlyList<McpResourceDescriptor>> Resources(CancellationToken cancellationToken) => client.ListResourcesAsync(cancellationToken);

    [HttpGet("resources/read")]
    public Task<JsonElement> ReadResource([FromQuery] string uri, CancellationToken cancellationToken) => client.ReadResourceAsync(uri, cancellationToken);

    [HttpGet("prompts")]
    public Task<IReadOnlyList<McpPromptDescriptor>> Prompts(CancellationToken cancellationToken) => client.ListPromptsAsync(cancellationToken);

    [HttpPost("prompts/{name}/get")]
    [RequestSizeLimit(2048)]
    public Task<JsonElement> GetPrompt(string name, McpPromptRequest request, CancellationToken cancellationToken) =>
        client.GetPromptAsync(name, request.Arguments, cancellationToken);
}
