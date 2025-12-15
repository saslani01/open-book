using OpenBook.Models;

namespace OpenBook.Services.Interfaces;

public interface IGitHubService
{
    Task<CachedProfile> ScrapeProfileAsync(string username);
    Task<RateLimitInfo> CheckRateLimitAsync();
}

public class RateLimitInfo
{
    public int Limit { get; set; }
    public int Remaining { get; set; }
    public DateTime ResetTime { get; set; }
}