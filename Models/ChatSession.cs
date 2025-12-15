namespace OpenBook.Models;

public class ChatSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
    public List<ChatMessage> Messages { get; set; } = new();
    public List<TokenUsage> TokenHistory { get; set; } = new();
    public int TotalTokensUsed { get; set; }
}