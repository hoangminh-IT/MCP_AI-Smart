
using System.Net;
using Microsoft.AspNetCore.Mvc;
using SmartInventory.Api.Services;

namespace SmartInventory.Api.Controllers;

[ApiController]
[Route("api/ai")]
public class AiController : ControllerBase
{
    private readonly GeminiService _geminiService;

    public AiController(GeminiService geminiService)
    {
        _geminiService = geminiService;
    }

    public record ChatRequest(string Message);

    [HttpPost("chat")]
    public async Task<IActionResult> Chat(
        [FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new
            {
                error = "Cau hoi khong duoc de trong"
            });
        }

        try
        {
            var answer = await _geminiService.AskAsync(
                request.Message);

            return Ok(new { answer });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(504, new
            {
                error = "Gemini phan hoi qua 30 giay. Vui long thu lai."
            });
        }
        catch (HttpRequestException ex)
            when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return StatusCode(429, new
            {
                error = "Gemini vuot gioi han su dung API. Vui long thu lai sau."
            });
        }
        catch (HttpRequestException)
        {
            return StatusCode(502, new
            {
                error = "Khong the ket noi den Gemini AI."
            });
        }
    }
}
