namespace OpenBook.Services.Interfaces;

using OpenBook.Models;

public interface IChatService
{
    Task<ChatSession> StartSessionAsync(string username);
    Task<ChatResponse> SendMessageAsync(string sessionId, ChatInput input);
    Task<ChatSession?> GetSessionAsync(string sessionId);
    Task<List<ChatSession>> ListSessionsAsync(string username);
    Task DeleteSessionAsync(string sessionId);
}