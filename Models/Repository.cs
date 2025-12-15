namespace OpenBook.Models;

public class Repository
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PrimaryLanguage { get; set; }
    public int Stars { get; set; }
    public int Forks { get; set; }
    public bool IsFork { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? Url { get; set; }
    // public List<string> Topics { get; set; } = new(); // did not use this
    public string? ReadmeContent { get; set; }
    public Dictionary<string, LanguageInfo> Languages { get; set; } = new();
}