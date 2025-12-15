namespace OpenBook.Models;

public class KnowledgeBase // GitHub Raw Scraped
{
    public string Username { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime ProfileScrapedAt { get; set; }
    public List<ProjectSummary> ProjectSummaries { get; set; } = new();
    public TokenUsage TokensUsed { get; set; } = new();
}

public class ProjectSummary
{
    public string RepositoryName { get; set; } = string.Empty;
    public string AiSummary { get; set; } = string.Empty;
}