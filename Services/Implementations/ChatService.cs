namespace OpenBook.Services.Implementations;

using OpenAI.Chat;
using OpenBook.Models;
using OpenBook.Services.Interfaces;

public class ChatService : IChatService
{
    private readonly ChatClient _chatClient;
    private readonly IBlobStorageService _blobStorage;
    private readonly IGitHubService _gitHubService;
    private readonly IKnowledgeBaseService _kbService;
    private readonly IntentDetector _intentDetector;
    private readonly ChatContextBuilder _contextBuilder;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        ChatClient chatClient,
        IBlobStorageService blobStorage,
        IGitHubService gitHubService,
        IKnowledgeBaseService kbService,
        IntentDetector intentDetector,
        ChatContextBuilder contextBuilder,
        ILogger<ChatService> logger)
    {
        _chatClient = chatClient;
        _blobStorage = blobStorage;
        _gitHubService = gitHubService;
        _kbService = kbService;
        _intentDetector = intentDetector;
        _contextBuilder = contextBuilder;
        _logger = logger;
    }

    public async Task<ChatSession> StartSessionAsync(string username)
    {
        await EnsureProfileAndKnowledgeBaseAsync(username);

        var session = new ChatSession
        {
            SessionId = Guid.NewGuid().ToString(),
            Username = username,
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow
        };

        await _blobStorage.SaveChatSessionAsync(session);
        _logger.LogInformation("Started session {SessionId} for {Username}", session.SessionId, username);

        return session;
    }

    public async Task<ChatResponse> SendMessageAsync(string sessionId, ChatInput input)
    {
        // 1. Load session
        var session = await _blobStorage.LoadChatSessionAsync(sessionId);
        if (session == null)
            throw new InvalidOperationException($"Session {sessionId} not found");

        // 2. Ensure fresh profile and KB
        var (profile, kb) = await EnsureProfileAndKnowledgeBaseAsync(session.Username);

        // 3. Detect intent
        var (isSpecific, repoName) = await _intentDetector.DetectIntentAsync(input.Message, profile.Repositories);
        var contextMode = isSpecific ? "Detailed" : "General";

        _logger.LogInformation("Intent: {Mode}, Repo: {Repo}", contextMode, repoName ?? "none");

        // 4. Build context
        var personaPrompt = _contextBuilder.BuildPersonaPrompt(profile);
        string contextPrompt;

        if (isSpecific && repoName != null)
        {
            var repo = profile.Repositories.FirstOrDefault(r => r.Name == repoName);
            contextPrompt = repo != null
                ? _contextBuilder.BuildDetailedContext(profile, repo, kb)
                : _contextBuilder.BuildGeneralContext(profile, kb);
        }
        else
        {
            contextPrompt = _contextBuilder.BuildGeneralContext(profile, kb);
        }

        // 5. Build messages for OpenAI
        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            new SystemChatMessage(personaPrompt),
            new SystemChatMessage(contextPrompt)
        };

        // Add conversation history (limit to last 5 messages to save tokens) (maybe I can add a window/que or sth later)
        foreach (var msg in session.Messages.TakeLast(5))
        {
            if (msg.Role == "user")
                messages.Add(new UserChatMessage(msg.Content));
            else
                messages.Add(new AssistantChatMessage(msg.Content));
        }

        // Add new user message
        messages.Add(new UserChatMessage(input.Message));

        // 6. Call OpenAI
        var completion = await _chatClient.CompleteChatAsync(messages);
        var responseText = completion.Value.Content[0].Text;
        var usage = completion.Value.Usage;

        // 7. Update session
        session.Messages.Add(new Models.ChatMessage
        {
            Role = "user",
            Content = input.Message,
            Timestamp = DateTime.UtcNow
        });

        session.Messages.Add(new Models.ChatMessage
        {
            Role = "assistant",
            Content = responseText,
            Timestamp = DateTime.UtcNow
        });

        session.TokenHistory.Add(new TokenUsage
        {
            PromptTokens = usage.InputTokenCount,
            CompletionTokens = usage.OutputTokenCount,
            TotalTokens = usage.TotalTokenCount
        });

        session.TotalTokensUsed += usage.TotalTokenCount;
        session.LastMessageAt = DateTime.UtcNow;

        // 8. Save session
        await _blobStorage.SaveChatSessionAsync(session);

        _logger.LogInformation("Message processed. Tokens: {Tokens}", usage.TotalTokenCount);

        return new ChatResponse
        {
            Message = responseText,
            TokensUsed = usage.TotalTokenCount,
            ContextMode = contextMode,
            MatchedRepository = repoName
        };
    }

    public async Task<ChatSession?> GetSessionAsync(string sessionId)
    {
        return await _blobStorage.LoadChatSessionAsync(sessionId);
    }

    public async Task<List<ChatSession>> ListSessionsAsync(string username)
    {
        var sessionIds = await _blobStorage.ListChatSessionsAsync(username);
        var sessions = new List<ChatSession>();

        foreach (var sessionId in sessionIds)
        {
            var session = await _blobStorage.LoadChatSessionAsync(sessionId);
            if (session != null)
                sessions.Add(session);
        }

        return sessions;
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        await _blobStorage.DeleteChatSessionAsync(sessionId);
        _logger.LogInformation("Deleted session {SessionId}", sessionId);
    }

    private async Task<(CachedProfile profile, KnowledgeBase kb)> EnsureProfileAndKnowledgeBaseAsync(string username)
    {
        CachedProfile? profile;
        KnowledgeBase? kb;

        var isProfileFresh = await _blobStorage.IsProfileFreshAsync(username);
        if (isProfileFresh)
        {
            profile = await _blobStorage.LoadProfileAsync(username);
            _logger.LogInformation("Loaded fresh profile for {Username}", username);
        }
        else
        {
            _logger.LogInformation("Profile stale or missing, scraping {Username}", username);
            profile = await _gitHubService.ScrapeProfileAsync(username);
            await _blobStorage.SaveProfileAsync(profile);
        }

        if (profile == null)
            throw new InvalidOperationException($"Could not load or scrape profile for {username}");

        var isKbFresh = await _blobStorage.IsKnowledgeBaseFreshAsync(username);
        if (isKbFresh)
        {
            kb = await _blobStorage.LoadKnowledgeBaseAsync(username);
            _logger.LogInformation("Loaded fresh KB for {Username}", username);
        }
        else
        {
            _logger.LogInformation("KB stale or missing, generating for {Username}", username);
            kb = await _kbService.GenerateKnowledgeBaseAsync(profile);
            await _blobStorage.SaveKnowledgeBaseAsync(kb);
        }

        if (kb == null)
            throw new InvalidOperationException($"Could not load or generate KB for {username}");

        return (profile, kb);
    }
}