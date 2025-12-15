using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenBook.Configuration;
using OpenBook.Models;
using OpenBook.Services.Interfaces;

namespace OpenBook.Services.Implementations;

public class AzureOpenAIService : IKnowledgeBaseService
{
    private readonly OpenAI.Chat.ChatClient _chatClient;
    private readonly AzureOpenAIOptions _options;
    private readonly ILogger<AzureOpenAIService> _logger;

    public AzureOpenAIService(
        IOptions<AzureOpenAIOptions> options,
        ILogger<AzureOpenAIService> logger)
    {
        _options = options.Value;
        _logger = logger;

        var azureClient = new AzureOpenAIClient(
            new Uri(_options.Endpoint),
            new AzureKeyCredential(_options.ApiKey));

        _chatClient = azureClient.GetChatClient(_options.DeploymentName);
    }

    public async Task<KnowledgeBase> GenerateKnowledgeBaseAsync(CachedProfile profile)
    {
        _logger.LogInformation("Generating knowledge base for {Username}", profile.Username);
        var projectSummaries = new List<ProjectSummary>(); 
        int totalPromptTokens = 0;
        int totalCompletionTokens = 0;

        var reposWithReadmes = profile.Repositories
            .Where(r => !string.IsNullOrEmpty(r.ReadmeContent))
            .ToList();

        _logger.LogInformation("Analyzing {Count} repositories with READMEs", reposWithReadmes.Count);

        foreach (var repo in reposWithReadmes)
        {
            var prompt = BuildReadmePrompt(repo);

            var messages = new List<OpenAI.Chat.ChatMessage>
            {
                new OpenAI.Chat.SystemChatMessage("You are a technical analyst specializing in extracting key information from project documentation."),
                new OpenAI.Chat.UserChatMessage(prompt)
            };

            var completionResult = await _chatClient.CompleteChatAsync(messages);
            
            var summary = completionResult.Value.Content[0].Text;
            var usage = completionResult.Value.Usage;

            totalPromptTokens += usage.InputTokenCount;
            totalCompletionTokens += usage.OutputTokenCount;

            projectSummaries.Add(new ProjectSummary
            {
                RepositoryName = repo.Name,
                AiSummary = summary
            });

            _logger.LogInformation("Analyzed {RepoName}: {Tokens} tokens", 
                repo.Name, usage.TotalTokenCount);

            await Task.Delay(100);
        }

        var tokenUsage = new TokenUsage
        {
            PromptTokens = totalPromptTokens,
            CompletionTokens = totalCompletionTokens,
            TotalTokens = totalPromptTokens + totalCompletionTokens
        };

        _logger.LogInformation("Generated knowledge base for {Username}. Total tokens: {Tokens}",
            profile.Username, tokenUsage.TotalTokens);

        return new KnowledgeBase
        {
            Username = profile.Username,
            GeneratedAt = DateTime.UtcNow,
            ProfileScrapedAt = profile.CachedAt,
            ProjectSummaries = projectSummaries,
            TokensUsed = tokenUsage
        };
    }

    private string BuildReadmePrompt(Repository repo)
    {
        return $@"Analyze this project's README and extract key technical information.

PROJECT: {repo.Name}
DESCRIPTION: {repo.Description ?? "N/A"}
PRIMARY LANGUAGE: {repo.PrimaryLanguage ?? "N/A"}

README CONTENT:
{repo.ReadmeContent}

Extract and summarize in 3-5 concise paragraphs:

1. PROJECT PURPOSE: What problem does this solve? What does it do?
2. TECHNICAL IMPLEMENTATION: Key technologies, frameworks, libraries, architecture used. Be specific.
3. KEY FEATURES: Main functionality, what makes it notable.
4. USAGE/DEPLOYMENT: Installation, commands, configuration mentioned.
5. TECHNICAL INSIGHTS: Interesting implementation details, challenges solved.

Be specific and technical. Include actual technology names from the README. Keep it concise.";
    }
}