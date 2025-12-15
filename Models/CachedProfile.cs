namespace OpenBook.Models;

public class CachedProfile
{
    // User Info
    public string Username { get; set; } = String.Empty;
    public string? Name { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Company { get; set; }
    public string? Location { get; set; }
    public int PublicRepos { get; set; }
    public int Followers { get; set; }
    public int Following { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;


    // Repositories
    public List<Repository> Repositories { get; set; } = new();
    
    // Calculated Metadata
    public int TotalStars { get; set; }
    public Dictionary<string, LanguageStat> LanguageStats { get; set; } = new();
}

public class LanguageStat
{
    public double Percentage { get; set; }
    public int ReposUsingThisLanguage { get; set; }
}