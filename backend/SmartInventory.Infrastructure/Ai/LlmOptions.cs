namespace SmartInventory.Infrastructure.Ai;

public sealed class LlmOptions
{
    public string Provider { get; set; } = "OpenAICompatible";
    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int RequestTimeoutSeconds { get; set; } = 60;
    public int MaxToolRounds { get; set; } = 4;
    public int MaxToolCalls { get; set; } = 8;
    public int MaxOutputTokens { get; set; } = 1024;
    public int MaxContextCharacters { get; set; } = 64000;
}
