using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Application.Common;
using SmartInventory.Application.Mcp;

namespace SmartInventory.Api.Controllers;

public sealed class McpWriteIntentRequest
{
    public string ToolName { get; set; } = string.Empty;
    [JsonRequired] public JsonElement Arguments { get; set; }
    public Guid? SessionId { get; set; }
}

public sealed record McpWriteConfirmationResult(McpWriteIntentDto Intent, McpExecutionDto? Execution);

/// <summary>Browser/user confirmation is outside the LLM. The opaque server proof never leaves this application.</summary>
[ApiController, Route("api/mcp/write-intents"), Authorize(Roles = "Admin,Staff")]
public sealed class McpWriteIntentsController(IMcpWriteIntentService intents, IMcpConfirmedToolClient client) : ControllerBase
{
    private int UserId => int.TryParse(User.FindFirstValue("sub"), NumberStyles.None,
        CultureInfo.InvariantCulture, out var id) && id > 0 ? id : throw new AppUnauthorizedException("A valid identity is required.");

    [HttpGet]
    public Task<IReadOnlyList<McpWriteIntentDto>> List([FromQuery] Guid? sessionId, CancellationToken token) =>
        intents.ListAsync(UserId, sessionId, token);

    [HttpGet("{id:guid}")]
    public Task<McpWriteIntentDto> Get(Guid id, CancellationToken token) => intents.GetAsync(UserId, id, token);

    [HttpPost, RequestSizeLimit(20 * 1024)]
    public async Task<ActionResult<McpWriteIntentDto>> Create(McpWriteIntentRequest request, CancellationToken token)
    {
        var intent = await intents.CreateAsync(UserId, request.ToolName, request.Arguments, request.SessionId, token);
        return CreatedAtAction(nameof(Get), new { id = intent.Id }, intent);
    }

    [HttpPost("{id:guid}/confirm")]
    public async Task<McpWriteConfirmationResult> Confirm(Guid id, CancellationToken token)
    {
        var approval = await intents.ApproveAsync(UserId, id, token);
        if (approval.Intent.Status == "Succeeded") return new(approval.Intent, null);
        var execution = await client.ExecuteConfirmedAsync(approval.Intent.ToolName, approval.Intent.Arguments, approval.Proof, token);
        // A transport timeout is not evidence of rollback. Return the current
        // persisted intent so the UI can recover with this same ID.
        return new(await intents.GetAsync(UserId, id, token), execution);
    }

    [HttpPost("{id:guid}/cancel")]
    public Task<McpWriteIntentDto> Cancel(Guid id, CancellationToken token) => intents.CancelAsync(UserId, id, token);
}
