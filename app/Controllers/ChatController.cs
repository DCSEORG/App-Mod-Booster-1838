using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>Send a message to the AI assistant</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { message = "Message cannot be empty." });

        try
        {
            var response = await _chatService.ChatAsync(request.Message, request.History);
            return Ok(new ChatResponse { Message = response });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in POST /api/chat");
            return StatusCode(500, new ChatResponse { Message = $"Error: {ex.Message}" });
        }
    }
}

public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? History { get; set; }
}

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
}
