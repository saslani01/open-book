using OpenBook.Models;

namespace OpenBook.Services.Interfaces;

public interface IKnowledgeBaseService
{
    Task<KnowledgeBase> GenerateKnowledgeBaseAsync(CachedProfile profile);
}