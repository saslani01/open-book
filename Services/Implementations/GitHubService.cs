using Microsoft.Extensions.Options;
using OpenBook.Configuration;
using OpenBook.Models;
using OpenBook.Services.Interfaces;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenBook.Services.Implementations;

public class GitHubService : IGitHubService
{
    private readonly HttpClient _httpClient;
    private readonly GitHubOptions _options;
    private readonly ILogger<GitHubService> _logger;

    public GitHubService(
        IHttpClientFactory httpClientFactory,
        IOptions<GitHubOptions> options,
        ILogger<GitHubService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("GitHub");
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrEmpty(_options.AccessToken))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"token {_options.AccessToken}");
        }
    }

    public async Task<CachedProfile> ScrapeProfileAsync(string username)
    {
        _logger.LogInformation("Scraping GitHub profile for {Username}", username);

        var rateLimit = await CheckRateLimitAsync();
        if (rateLimit.Remaining < 50)
        {
            _logger.LogWarning("Low rate limit before scraping: {Remaining}/{Limit}", 
                rateLimit.Remaining, rateLimit.Limit);
        }

        var profile = new CachedProfile
        {
            Username = username,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await PopulateUserProfileAsync(profile);
        await PopulateRepositoriesAsync(profile);
        CalculateMetadata(profile);

        _logger.LogInformation("Successfully scraped {RepoCount} repositories for {Username}", 
            profile.Repositories.Count, profile.Username);

        return profile;
    }

    public async Task<RateLimitInfo> CheckRateLimitAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/rate_limit");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);
            var rate = data.GetProperty("rate");

            var resetTimestamp = rate.GetProperty("reset").GetInt64();
            var resetTime = DateTimeOffset.FromUnixTimeSeconds(resetTimestamp).UtcDateTime;

            var info = new RateLimitInfo
            {
                Limit = rate.GetProperty("limit").GetInt32(),
                Remaining = rate.GetProperty("remaining").GetInt32(),
                ResetTime = resetTime
            };

            if (info.Remaining < 100)
            {
                _logger.LogWarning("Low rate limit: {Remaining}/{Limit} remaining, resets at {ResetTime}",
                    info.Remaining, info.Limit, info.ResetTime);
            }

            return info;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check rate limit");
            return new RateLimitInfo();
        }
    }

    private async Task PopulateUserProfileAsync(CachedProfile profile)
    {
        var response = await _httpClient.GetAsync($"/users/{profile.Username}");
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(json);

        profile.Name = data.TryGetProperty("name", out var name) ? name.GetString() : null;
        profile.Bio = data.TryGetProperty("bio", out var bio) ? bio.GetString() : null;
        profile.AvatarUrl = data.GetProperty("avatar_url").GetString();
        profile.Company = data.TryGetProperty("company", out var company) ? company.GetString() : null;
        profile.Location = data.TryGetProperty("location", out var location) ? location.GetString() : null;
        profile.PublicRepos = data.GetProperty("public_repos").GetInt32();
        profile.Followers = data.GetProperty("followers").GetInt32();
        profile.Following = data.GetProperty("following").GetInt32();
        profile.CreatedAt = data.GetProperty("created_at").GetDateTime();
        profile.UpdatedAt = data.GetProperty("updated_at").GetDateTime();
    }

    private async Task PopulateRepositoriesAsync(CachedProfile profile)
    {
        var page = 1;

        while (true)
        {
            var response = await _httpClient.GetAsync(
                $"/users/{profile.Username}/repos?per_page=100&page={page}&sort=updated");
            
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var repos = JsonSerializer.Deserialize<JsonElement>(json);

            if (repos.GetArrayLength() == 0)
                break;

            foreach (var repoElement in repos.EnumerateArray())
            {
                var repoName = repoElement.GetProperty("name").GetString() ?? "";
                var readmeContent = await GetReadmeAsync(profile, repoName);
                var languages = await GetRepositoryLanguagesAsync(profile, repoName);

                profile.Repositories.Add(new Repository
                {
                    Name = repoName,
                    Description = repoElement.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                    PrimaryLanguage = repoElement.TryGetProperty("language", out var lang) ? lang.GetString() : null,
                    Stars = repoElement.GetProperty("stargazers_count").GetInt32(),
                    Forks = repoElement.GetProperty("forks_count").GetInt32(),
                    IsFork = repoElement.GetProperty("fork").GetBoolean(),
                    CreatedAt = repoElement.GetProperty("created_at").GetDateTime(),
                    UpdatedAt = repoElement.GetProperty("updated_at").GetDateTime(),
                    Url = repoElement.GetProperty("html_url").GetString(),
                    ReadmeContent = !string.IsNullOrEmpty(readmeContent) ? CleanReadmeContent(readmeContent) : null,
                    Languages = languages
                });

                _logger.LogInformation("Processed {RepoName} ({LanguageCount} languages)", 
                    repoName, languages.Count);
            }

            page++;
            await Task.Delay(100);
        }
    }

    private async Task<string?> GetReadmeAsync(CachedProfile profile, string repoName)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/repos/{profile.Username}/{repoName}/readme");
            
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);
            
            var content = data.GetProperty("content").GetString();
            if (string.IsNullOrEmpty(content))
                return null;

            var bytes = Convert.FromBase64String(content);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }

    private async Task<Dictionary<string, LanguageInfo>> GetRepositoryLanguagesAsync(CachedProfile profile, string repoName)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/repos/{profile.Username}/{repoName}/languages");
            
            if (!response.IsSuccessStatusCode)
                return new Dictionary<string, LanguageInfo>();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            var languages = new Dictionary<string, LanguageInfo>();
            long totalBytes = 0;

            foreach (var lang in data.EnumerateObject())
                totalBytes += lang.Value.GetInt64();

            foreach (var lang in data.EnumerateObject())
            {
                var bytes = lang.Value.GetInt64();
                languages[lang.Name] = new LanguageInfo
                {
                    Bytes = bytes,
                    Percentage = totalBytes > 0 ? (double)bytes / totalBytes * 100 : 0
                };
            }

            return languages;
        }
        catch
        {
            return new Dictionary<string, LanguageInfo>();
        }
    }

    private string CleanReadmeContent(string readmeText)
    {
        if (string.IsNullOrEmpty(readmeText))
            return string.Empty;

        var text = readmeText;

        text = Regex.Replace(text, @"\[!\[.*?\]\(.*?\)\]\((.*?)\)", "$1");
        text = Regex.Replace(text, @"\[([^\]]*)\]\(([^\)]*)\)", m =>
        {
            var textPart = m.Groups[1].Value.Trim();
            var urlPart = m.Groups[2].Value.Trim();
            return textPart == urlPart ? urlPart : $"{textPart} {urlPart}";
        });
        text = Regex.Replace(text, @"<[^>]+>", "");
        text = Regex.Replace(text, @"^#+\s*", "", RegexOptions.Multiline);
        text = Regex.Replace(text, @"^[-*_]{3,}\s*$", "", RegexOptions.Multiline);
        text = Regex.Replace(text, @"\*\*([^*]+)\*\*", "$1");
        text = Regex.Replace(text, @"\*([^*]+)\*", "$1");
        text = Regex.Replace(text, @"__([^_]+)__", "$1");
        text = Regex.Replace(text, @"_([^_]+)_", "$1");
        text = Regex.Replace(text, @"`([^`]+)`", "$1");
        text = Regex.Replace(text, @"```.*?```", "", RegexOptions.Singleline);
        text = Regex.Replace(text, @"[^\w\s\.\,\!\?\;\:\(\)\-]", "");
        text = text.Replace("\n", " ").Replace("\r", " ");
        text = Regex.Replace(text, @"\s+", " ");

        return text.Trim();
    }

    private void CalculateMetadata(CachedProfile profile)
{
    profile.TotalStars = profile.Repositories.Sum(r => r.Stars);
    
    var languageCounts = new Dictionary<string, int>();
    var languageBytes = new Dictionary<string, long>();

    foreach (var repo in profile.Repositories)
    {
        // Use the detailed Languages dictionary if available
        if (repo.Languages.Any())
        {
            foreach (var lang in repo.Languages)
            {
                if (!languageCounts.ContainsKey(lang.Key))
                {
                    languageCounts[lang.Key] = 0;
                    languageBytes[lang.Key] = 0;
                }
                languageCounts[lang.Key]++;
                languageBytes[lang.Key] += lang.Value.Bytes;
            }
        }
        // Fallback to PrimaryLanguage if Languages is empty
        else if (!string.IsNullOrEmpty(repo.PrimaryLanguage))
        {
            if (!languageCounts.ContainsKey(repo.PrimaryLanguage))
            {
                languageCounts[repo.PrimaryLanguage] = 0;
                languageBytes[repo.PrimaryLanguage] = 0;
            }
            languageCounts[repo.PrimaryLanguage]++;
        }
    }

    // Calculate total bytes for percentage
    var totalBytes = languageBytes.Values.Sum();

    profile.LanguageStats = languageCounts
        .OrderByDescending(kvp => languageBytes.GetValueOrDefault(kvp.Key, 0))
        // .Take(10)  lets take all for now!
        .ToDictionary(
            kvp => kvp.Key,
            kvp => new LanguageStat
            {
                ReposUsingThisLanguage = kvp.Value,
                Percentage = totalBytes > 0 
                    ? (double)languageBytes[kvp.Key] / totalBytes * 100 
                    : 0
            }
        );
}


    

    

}

