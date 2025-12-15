namespace OpenBook.Models;

public class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public string ContextMode { get; set; } = string.Empty;
    public string? MatchedRepository { get; set; }
}