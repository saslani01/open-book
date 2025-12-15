namespace OpenBook.Services.Implementations;

using OpenAI.Chat;
using OpenBook.Models;

public class IntentDetector
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<IntentDetector> _logger;

    public IntentDetector(ChatClient chatClient, ILogger<IntentDetector> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<(bool IsSpecific, string? RepoName)> DetectIntentAsync(
        string userMessage,
        List<Repository> repositories)
    {
        if (string.IsNullOrWhiteSpace(userMessage) || repositories.Count == 0)
            return (false, null);

        var repoNames = repositories.Select(r => r.Name).ToList();
        var repoList = string.Join(", ", repoNames);

        var prompt = $@"Classify this user question into one of two categories:

        GENERAL - Questions about:
        - Skills, languages, experience (e.g., ""Do you know Python?"")
        - Overall background, bio, who they are
        - General work history or interests
        - Anything NOT about a specific project

        DETAILED - Questions about a SPECIFIC project from this list:
        {repoList}

        User question: ""{userMessage}""

        Respond with ONLY one of:
        - GENERAL
        - DETAILED:project-name

        Examples:
        ""Are you good at C#?"" → GENERAL
        ""Tell me about poly-ratings-llm"" → DETAILED:poly-ratings-llm
        ""What's your experience?"" → GENERAL
        ""How does the poly ratings project work?"" → DETAILED:poly-ratings-llm";

        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            new SystemChatMessage("You are a classifier. Respond with only GENERAL or DETAILED:repo-name. Nothing else."),
            new UserChatMessage(prompt)
        };

        try
        {
            var completion = await _chatClient.CompleteChatAsync(messages);
            var response = completion.Value.Content[0].Text.Trim();

            _logger.LogInformation("Intent classification: {Response}", response);

            if (response.StartsWith("DETAILED:", StringComparison.OrdinalIgnoreCase))
            {
                var repoName = response.Substring(9).Trim();
                
                var matchedRepo = repoNames.FirstOrDefault(r => 
                    r.Equals(repoName, StringComparison.OrdinalIgnoreCase));

                if (matchedRepo != null)
                    return (true, matchedRepo);
            }

            return (false, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Intent detection failed, defaulting to General");
            return (false, null);
        }
    }
}