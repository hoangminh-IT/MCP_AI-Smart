using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using SmartInventory.Application.Mcp;

namespace SmartInventory.Application.Ai;

public sealed record AiProviderStatus(bool Configured, string Provider, string Model, string? Reason);
public sealed record AiFunctionCall(string Id, string Name, string Arguments);
public sealed record AiProviderMessage(string Role, string? Content, IReadOnlyList<AiFunctionCall>? ToolCalls = null, string? ToolCallId = null);
public sealed record AiCompletion(string? Content, IReadOnlyList<AiFunctionCall> ToolCalls);

/// <summary>Provider boundary; an Ollama or other adapter can implement this without changing MCP orchestration.</summary>
public interface ILlmProvider
{
    AiProviderStatus GetStatus();
    Task<AiCompletion> CompleteAsync(IReadOnlyList<AiProviderMessage> messages,
        IReadOnlyList<McpToolDescriptor> tools, CancellationToken cancellationToken);
}

public sealed record AiSessionDto(Guid Id, string Title, DateTime CreatedAt, DateTime UpdatedAt);
public sealed record AiMessageDto(Guid Id, string Role, string Content, string Status, DateTime CreatedAt, JsonElement? ToolMetadata);
public sealed record AiConversationDto(AiSessionDto Session, IReadOnlyList<AiMessageDto> Messages);
public sealed record AiTraceStep(string Name, DateTime StartedAt, long DurationMs, string Status,
    int? ExecutionId = null, string? ToolName = null, JsonElement? Input = null, JsonElement? Output = null);
public sealed record AiReply(Guid SessionId, AiMessageDto Message, IReadOnlyList<McpExecutionDto> ToolExecutions, IReadOnlyList<AiTraceStep> Trace);
public sealed record AiTitleRequest([Required, MaxLength(120)] string Title);
public sealed record AiMessageRequest([Required, MaxLength(4000)] string Message);

public interface IAiChatService
{
    AiProviderStatus GetStatus();
    Task<IReadOnlyList<AiSessionDto>> ListAsync(int userId, CancellationToken cancellationToken);
    Task<AiSessionDto> CreateAsync(int userId, string title, CancellationToken cancellationToken);
    Task<AiConversationDto> GetAsync(int userId, Guid sessionId, CancellationToken cancellationToken);
    Task<AiSessionDto> RenameAsync(int userId, Guid sessionId, string title, CancellationToken cancellationToken);
    Task DeleteAsync(int userId, Guid sessionId, CancellationToken cancellationToken);
    Task<AiReply> SendAsync(int userId, Guid sessionId, string message, CancellationToken cancellationToken);
}

public sealed class AiUnavailableException(string message) : Exception(message);
public sealed class AiProviderException(string message) : Exception(message);
