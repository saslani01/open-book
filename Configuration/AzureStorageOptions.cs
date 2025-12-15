namespace OpenBook.Configuration;

public class AzureStorageOptions
{
    public string ConnectionString { get; set; } = string.Empty;
    public string ProfilesContainer { get; set; } = "github-profiles";
    public string KnowledgeBasesContainer { get; set; } = "knowledge-bases";
    public string ChatSessionsContainer { get; set; } = "chat-sessions";
}