using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenBook.Models;
using OpenBook.Services.Interfaces;

namespace OpenBook.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("fixed")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [HttpPost("{username}/start")]
    public async Task<ActionResult<ChatSession>> StartSession(string username)
    {
        try
        {
            var session = await _chatService.StartSessionAsync(username);
            return Ok(session);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start session for {Username}", username);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("send")]
    public async Task<ActionResult<ChatResponse>> SendMessage(
        [FromQuery] string sessionId,
        [FromBody] ChatInput input)
    {
        try
        {
            var response = await _chatService.SendMessageAsync(sessionId, input);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Session not found: {SessionId}", sessionId);
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message for session {SessionId}", sessionId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("session/{sessionId}")]
    public async Task<ActionResult<ChatSession>> GetSession(string sessionId)
    {
        var session = await _chatService.GetSessionAsync(sessionId);
        
        if (session == null)
            return NotFound(new { error = $"Session {sessionId} not found" });

        return Ok(session);
    }

    [HttpDelete("session/{sessionId}")]
    public async Task<ActionResult> DeleteSession(string sessionId)
    {
        try
        {
            await _chatService.DeleteSessionAsync(sessionId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete session {SessionId}", sessionId);
            return BadRequest(new { error = ex.Message });
        }
    }
}