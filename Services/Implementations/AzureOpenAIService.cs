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
    private const int MaxParallelCalls = 5;

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

        var reposWithReadmes = profile.Repositories
            .Where(r => !string.IsNullOrEmpty(r.ReadmeContent))
            .ToList();

        _logger.LogInformation("Analyzing {Count} repositories with READMEs", reposWithReadmes.Count);

        var semaphore = new SemaphoreSlim(MaxParallelCalls);
        var tasks = reposWithReadmes.Select(repo => AnalyzeRepoAsync(repo, semaphore));
        var results = await Task.WhenAll(tasks);

        var validResults = results.Where(r => r != null).ToList();

        var tokenUsage = new TokenUsage
        {
            PromptTokens = validResults.Sum(r => r!.PromptTokens),
            CompletionTokens = validResults.Sum(r => r!.CompletionTokens),
            TotalTokens = validResults.Sum(r => r!.PromptTokens + r!.CompletionTokens)
        };

        _logger.LogInformation("Generated knowledge base for {Username}. Total tokens: {Tokens}",
            profile.Username, tokenUsage.TotalTokens);

        return new KnowledgeBase
        {
            Username = profile.Username,
            GeneratedAt = DateTime.UtcNow,
            ProfileScrapedAt = profile.CachedAt,
            ProjectSummaries = validResults.Select(r => r!.Summary).ToList(),
            TokensUsed = tokenUsage
        };
    }

    private async Task<RepoAnalysisResult?> AnalyzeRepoAsync(Repository repo, SemaphoreSlim semaphore)
    {
        await semaphore.WaitAsync();
        try
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

            _logger.LogInformation("Analyzed {RepoName}: {Tokens} tokens",
                repo.Name, usage.TotalTokenCount);

            return new RepoAnalysisResult
            {
                Summary = new ProjectSummary
                {
                    RepositoryName = repo.Name,
                    AiSummary = summary
                },
                PromptTokens = usage.InputTokenCount,
                CompletionTokens = usage.OutputTokenCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to analyze {RepoName}", repo.Name);
            return null;
        }
        finally
        {
            semaphore.Release();
        }
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

    private class RepoAnalysisResult
    {
        public required ProjectSummary Summary { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
    }
}