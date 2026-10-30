using System.Text.Json;

namespace SmartInventory.Application.Mcp;

/// <summary>Application-only execution after an explicit user confirmation. Proof is never a model argument.</summary>
public interface IMcpConfirmedToolClient
{
    Task<McpExecutionDto> ExecuteConfirmedAsync(string toolName, JsonElement arguments, string confirmationProof,
        CancellationToken cancellationToken = default);
}
