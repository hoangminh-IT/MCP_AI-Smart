using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Mcp;

namespace SmartInventory.Api.Controllers;

public sealed class McpExecuteRequest
{
    [JsonRequired] public JsonElement Arguments { get; set; }
}

[ApiController]
[Route("api/mcp")]
[Authorize(Roles = "Admin,Staff")]
public sealed class McpController(IMcpClientService client, IMcpExecutionLogService logs) : ControllerBase
{
    [HttpGet("status")]
    public Task<McpConnectionStatus> Status(CancellationToken cancellationToken) => client.GetStatusAsync(cancellationToken);

    [HttpGet("tools")]
    public Task<IReadOnlyList<McpToolDescriptor>> Tools(CancellationToken cancellationToken) => client.ListToolsAsync(cancellationToken);

    [HttpPost("tools/{toolName}/execute")]
    [RequestSizeLimit(20 * 1024)]
    public Task<McpExecutionDto> Execute(string toolName, McpExecuteRequest request, CancellationToken cancellationToken) =>
        client.ExecuteToolAsync(toolName, request.Arguments, cancellationToken);

    [HttpGet("executions")]
    public Task<PagedResult<McpExecutionDto>> History([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) => logs.GetHistoryAsync(CurrentUserId(), page, pageSize, cancellationToken);

    [HttpGet("executions/{id:int}")]
    public Task<McpExecutionDto> Detail(int id, CancellationToken cancellationToken) => logs.GetAsync(CurrentUserId(), id, cancellationToken);

    private int CurrentUserId() => int.TryParse(User.FindFirstValue("sub"), NumberStyles.None,
        CultureInfo.InvariantCulture, out var id) && id > 0 ? id : throw new AppUnauthorizedException("A valid user identity is required.");
}
