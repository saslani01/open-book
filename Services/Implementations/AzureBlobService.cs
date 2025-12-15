using Azure.Storage.Blobs;
using Microsoft.Extensions.Options;
using OpenBook.Configuration;
using OpenBook.Models;
using OpenBook.Services.Interfaces;
using System.Text.Json;

namespace OpenBook.Services.Implementations;

public class AzureBlobService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly AzureStorageOptions _storageOptions;
    private readonly CacheSettings _cacheSettings;
    private readonly ILogger<AzureBlobService> _logger;

    public AzureBlobService(
        IOptions<AzureStorageOptions> storageOptions,
        IOptions<CacheSettings> cacheSettings,
        ILogger<AzureBlobService> logger)
    {
        _storageOptions = storageOptions.Value;
        _cacheSettings = cacheSettings.Value;
        _logger = logger;

        _blobServiceClient = new BlobServiceClient(_storageOptions.ConnectionString);
    }

    // GitHub Profile Methods
    public async Task SaveProfileAsync(CachedProfile profile)
    {
        try
        {
            profile.CachedAt = DateTime.UtcNow;

            var containerClient = _blobServiceClient.GetBlobContainerClient(_storageOptions.ProfilesContainer);
            await containerClient.CreateIfNotExistsAsync();
            
            var blobClient = containerClient.GetBlobClient($"{profile.Username}.json");
            var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
            
            await blobClient.UploadAsync(BinaryData.FromString(json), overwrite: true);
            
            _logger.LogInformation("Saved profile for {Username} to blob storage", profile.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save profile for {Username}", profile.Username);
            throw;
        }
    }

    public async Task<CachedProfile?> LoadProfileAsync(string username)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_storageOptions.ProfilesContainer);
            var blobClient = containerClient.GetBlobClient($"{username}.json");
            
            if (!await blobClient.ExistsAsync())
            {
                _logger.LogInformation("Profile not found in blob storage for {Username}", username);
                return null;
            }
            
            var response = await blobClient.DownloadContentAsync();
            var json = response.Value.Content.ToString();
            var profile = JsonSerializer.Deserialize<CachedProfile>(json);
            
            _logger.LogInformation("Loaded profile for {Username} from blob storage", username);
            return profile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load profile for {Username}", username);
            throw;
        }
    }

    public async Task<bool> IsProfileFreshAsync(string username)
    {
        var profile = await LoadProfileAsync(username);
        
        if (profile == null)
            return false;
        
    var age = DateTime.UtcNow - profile.CachedAt; 
        var isFresh = age.TotalHours < _cacheSettings.ProfileMaxAgeHours;
        
        _logger.LogInformation("Profile for {Username} is {Status} (Age: {Hours:F1} hours, Max: {MaxHours} hours)",
            username, isFresh ? "FRESH" : "STALE", age.TotalHours, _cacheSettings.ProfileMaxAgeHours);
        
        return isFresh;
    }

    // Knowledge Base Methods
    public async Task SaveKnowledgeBaseAsync(KnowledgeBase knowledgeBase)
    {
        try
        {
            var profile = await LoadProfileAsync(knowledgeBase.Username);
            
            if (profile == null)
            {
                _logger.LogWarning("Cannot save knowledge base for {Username} - profile not found in blob storage", 
                    knowledgeBase.Username);
                throw new InvalidOperationException(
                    $"Cannot save knowledge base for '{knowledgeBase.Username}'. " +
                    $"Profile must be saved to blob storage first. " +
                    $"Create profile with: POST /api/Test/blob/profile/{knowledgeBase.Username}");
            }
            
            var containerClient = _blobServiceClient.GetBlobContainerClient(_storageOptions.KnowledgeBasesContainer);
            await containerClient.CreateIfNotExistsAsync();
            
            var blobClient = containerClient.GetBlobClient($"{knowledgeBase.Username}-kb.json");
            var json = JsonSerializer.Serialize(knowledgeBase, new JsonSerializerOptions { WriteIndented = true });
            
            await blobClient.UploadAsync(BinaryData.FromString(json), overwrite: true);
            
            _logger.LogInformation("Saved knowledge base for {Username} to blob storage", knowledgeBase.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save knowledge base for {Username}", knowledgeBase.Username);
            throw;
        }
    }

    public async Task<KnowledgeBase?> LoadKnowledgeBaseAsync(string username)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_storageOptions.KnowledgeBasesContainer);
            var blobClient = containerClient.GetBlobClient($"{username}-kb.json");
            
            if (!await blobClient.ExistsAsync())
            {
                _logger.LogInformation("Knowledge base not found in blob storage for {Username}", username);
                return null;
            }
            
            var response = await blobClient.DownloadContentAsync();
            var json = response.Value.Content.ToString();
            var knowledgeBase = JsonSerializer.Deserialize<KnowledgeBase>(json);
            
            _logger.LogInformation("Loaded knowledge base for {Username} from blob storage", username);
            return knowledgeBase;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load knowledge base for {Username}", username);
            throw;
        }
    }

    public async Task<bool> IsKnowledgeBaseFreshAsync(string username)
    {
        var knowledgeBase = await LoadKnowledgeBaseAsync(username);
        var profile = await LoadProfileAsync(username);
        
        if (knowledgeBase == null || profile == null)
            return false;
        
        // KB is fresh if it matches the current profile timestamp
        var isFresh = knowledgeBase.ProfileScrapedAt == profile.CachedAt;
        
        _logger.LogInformation("Knowledge base for {Username} is {Status} (KB timestamp: {KBTime}, Profile timestamp: {ProfileTime})",
            username, isFresh ? "FRESH" : "STALE", knowledgeBase.ProfileScrapedAt, profile.CachedAt);
        
        return isFresh;
    }

    // Chat Session Methods
    
    public async Task SaveChatSessionAsync(ChatSession session)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_storageOptions.ChatSessionsContainer);
            await containerClient.CreateIfNotExistsAsync();
            
            var blobClient = containerClient.GetBlobClient($"{session.SessionId}.json");
            var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
            
            await blobClient.UploadAsync(BinaryData.FromString(json), overwrite: true);
            
            _logger.LogInformation("Saved chat session {SessionId} for {Username} to blob storage", 
                session.SessionId, session.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save chat session {SessionId}", session.SessionId);
            throw;
        }
    }

    public async Task<ChatSession?> LoadChatSessionAsync(string sessionId)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_storageOptions.ChatSessionsContainer);
            var blobClient = containerClient.GetBlobClient($"{sessionId}.json");
            
            if (!await blobClient.ExistsAsync())
            {
                _logger.LogInformation("Chat session {SessionId} not found in blob storage", sessionId);
                return null;
            }
            
            var response = await blobClient.DownloadContentAsync();
            var json = response.Value.Content.ToString();
            var session = JsonSerializer.Deserialize<ChatSession>(json);
            
            _logger.LogInformation("Loaded chat session {SessionId} from blob storage", sessionId);
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load chat session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<List<string>> ListChatSessionsAsync(string username)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_storageOptions.ChatSessionsContainer);
            
            // Check if container exists
            if (!await containerClient.ExistsAsync())
            {
                _logger.LogInformation("Chat sessions container does not exist yet");
                return new List<string>();
            }
            
            var sessionIds = new List<string>();
            
            // List all blobs in container
            await foreach (var blobItem in containerClient.GetBlobsAsync())
            {
                // Load each session to check username
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                var response = await blobClient.DownloadContentAsync();
                var json = response.Value.Content.ToString();
                var session = JsonSerializer.Deserialize<ChatSession>(json);
                
                // If session belongs to this username, add to list
                if (session?.Username == username)
                {
                    sessionIds.Add(session.SessionId);
                }
            }
            
            _logger.LogInformation("Found {Count} chat sessions for {Username}", sessionIds.Count, username);
            return sessionIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list chat sessions for {Username}", username);
            throw;
        }
    }

    public async Task DeleteChatSessionAsync(string sessionId)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(_storageOptions.ChatSessionsContainer);
            var blobClient = containerClient.GetBlobClient($"{sessionId}.json");
            
            await blobClient.DeleteIfExistsAsync();
            
            _logger.LogInformation("Deleted chat session {SessionId} from blob storage", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete chat session {SessionId}", sessionId);
            throw;
        }
    }
}