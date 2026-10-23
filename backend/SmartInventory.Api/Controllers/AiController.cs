using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartInventory.Application.Ai;

namespace SmartInventory.Api.Controllers;

[ApiController, Route("api/ai"), Authorize(Roles = "Admin,Staff")]
public sealed class AiController(IAiChatService chat) : ControllerBase
{
    private int UserId => int.Parse(User.FindFirstValue("sub")!, System.Globalization.CultureInfo.InvariantCulture);

    [HttpGet("status")]
    public AiProviderStatus Status() => chat.GetStatus();
    [HttpGet("sessions")]
    public Task<IReadOnlyList<AiSessionDto>> List(CancellationToken token) => chat.ListAsync(UserId, token);
    [HttpPost("sessions")]
    public async Task<ActionResult<AiSessionDto>> Create(AiTitleRequest request, CancellationToken token)
    {
        var session = await chat.CreateAsync(UserId, request.Title, token);
        return CreatedAtAction(nameof(Get), new { sessionId = session.Id }, session);
    }
    [HttpGet("sessions/{sessionId:guid}")]
    public Task<AiConversationDto> Get(Guid sessionId, CancellationToken token) => chat.GetAsync(UserId, sessionId, token);
    [HttpPut("sessions/{sessionId:guid}")]
    public Task<AiSessionDto> Rename(Guid sessionId, AiTitleRequest request, CancellationToken token) => chat.RenameAsync(UserId, sessionId, request.Title, token);
    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> Delete(Guid sessionId, CancellationToken token)
    {
        await chat.DeleteAsync(UserId, sessionId, token);
        return NoContent();
    }
    [HttpPost("sessions/{sessionId:guid}/messages"), EnableRateLimiting("ai")]
    public Task<AiReply> Send(Guid sessionId, AiMessageRequest request, CancellationToken token) => chat.SendAsync(UserId, sessionId, request.Message, token);
}
