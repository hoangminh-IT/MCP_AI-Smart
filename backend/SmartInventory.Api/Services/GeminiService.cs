
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace SmartInventory.Api.Services;

public class GeminiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GeminiService(
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<string> AskAsync(string question)
    {
        // Lay API Key tu User Secrets
        var apiKey = _configuration["Gemini:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new Exception("Chua co Gemini API Key");
        }

        // Dia chi Gemini API
        var url =
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent";

        // Noi dung cau hoi
        var body = new
{
    // System Prompt
    systemInstruction = new
    {
        parts = new[]
        {
            new { text = InventorySystemPrompt.Text }
        }
    },

    // Cau hoi nguoi dung
    contents = new[]
    {
        new
        {
            parts = new[]
            {
                new { text = question }
            }
        }
    }
};

        using var request =
            new HttpRequestMessage(HttpMethod.Post, url);

        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = JsonContent.Create(body);

        // Gui cau hoi den Gemini
        using var response =
            await _httpClient.SendAsync(request);

        response.EnsureSuccessStatusCode();

        // Doc ket qua AI tra ve
        var result = await response.Content
            .ReadFromJsonAsync<JsonNode>();

        var answer = result?["candidates"]?[0]?
            ["content"]?["parts"]?[0]?
            ["text"]?.GetValue<string>();

        return answer ?? "Gemini khong tra loi";
    }
}
