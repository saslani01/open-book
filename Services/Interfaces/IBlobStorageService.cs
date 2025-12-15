using OpenBook.Models;

namespace OpenBook.Services.Interfaces;

public interface IBlobStorageService
{
    // Profile operations
    Task<CachedProfile?> LoadProfileAsync(string username);
    Task SaveProfileAsync(CachedProfile profile);
    Task<bool> IsProfileFreshAsync(string username);
    
    // Knowledge Base operations
    Task<KnowledgeBase?> LoadKnowledgeBaseAsync(string username);
    Task SaveKnowledgeBaseAsync(KnowledgeBase knowledgeBase);
    Task<bool> IsKnowledgeBaseFreshAsync(string username);
    
    // Chat Session operations
    Task<ChatSession?> LoadChatSessionAsync(string sessionId);
    Task SaveChatSessionAsync(ChatSession session);
    Task<List<string>> ListChatSessionsAsync(string username);
    Task DeleteChatSessionAsync(string sessionId);
}