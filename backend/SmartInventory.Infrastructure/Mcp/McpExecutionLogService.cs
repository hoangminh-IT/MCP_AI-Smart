using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartInventory.Application.Catalog;
using SmartInventory.Application.Common;
using SmartInventory.Application.Entities;
using SmartInventory.Application.Mcp;
using SmartInventory.Infrastructure.Data;

namespace SmartInventory.Infrastructure.Mcp;

public sealed class McpExecutionLogService(AppDbContext database) : IMcpExecutionLogService
{
    public async Task<int> AppendAsync(int userId, string toolName, JsonElement input, JsonElement? output,
        string status, long durationMs, string? errorMessage, CancellationToken cancellationToken = default)
    {
        var safeInput = McpJsonRedactor.Sanitize(input).GetRawText();
        var safeOutput = output.HasValue ? McpJsonRedactor.Sanitize(output.Value).GetRawText() : null;
        if (safeOutput is not null && Encoding.UTF8.GetByteCount(safeOutput) > 1024 * 1024)
            safeOutput = JsonSerializer.Serialize(new { truncated = true, reason = "Result exceeds the 1 MiB audit limit." });
        var log = new McpToolExecutionLog
        {
            UserId = userId, ToolName = toolName.Length > 128 ? toolName[..128] : toolName,
            SanitizedArguments = safeInput, SanitizedOutput = safeOutput, Status = status,
            DurationMs = Math.Max(0, durationMs), ErrorMessage = errorMessage is { Length: > 1000 } ? errorMessage[..1000] : errorMessage
        };
        database.McpToolExecutionLogs.Add(log);
        await database.SaveChangesAsync(cancellationToken);
        return log.Id;
    }

    public async Task<PagedResult<McpExecutionDto>> GetHistoryAsync(int userId, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue)
            throw new AppValidationException("page must be positive and pageSize must be between 1 and 100.");
        var query = database.McpToolExecutionLogs.AsNoTracking().Where(log => log.UserId == userId);
        var total = await query.CountAsync(cancellationToken);
        var logs = await query.OrderByDescending(log => log.CreatedAt).ThenByDescending(log => log.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new(logs.Select(ToDto).ToList(), total, page, pageSize);
    }

    public async Task<McpExecutionDto> GetAsync(int userId, int executionId, CancellationToken cancellationToken = default)
    {
        var log = await database.McpToolExecutionLogs.AsNoTracking()
            .SingleOrDefaultAsync(item => item.UserId == userId && item.Id == executionId, cancellationToken)
            ?? throw new AppNotFoundException("Tool execution was not found.");
        return ToDto(log);
    }

    private static McpExecutionDto ToDto(McpToolExecutionLog log) => new(log.Id, log.ToolName,
        Parse(log.SanitizedArguments), log.SanitizedOutput is null ? null : Parse(log.SanitizedOutput),
        log.Status != McpExecutionStatuses.Succeeded, log.DurationMs, log.CreatedAt, log.ErrorMessage, log.Status);

    private static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
