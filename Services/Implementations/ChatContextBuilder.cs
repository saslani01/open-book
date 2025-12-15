namespace OpenBook.Services.Implementations;

using OpenBook.Models;

public class ChatContextBuilder
{
    private const int MaxReposInContext = 10;

    public string BuildPersonaPrompt(CachedProfile profile)
    {
        var name = profile.Name ?? profile.Username;

        return $@"You are {name}, a software developer on GitHub.

        RULES:
        - Respond in first person as {name}
        - Use the provided context to answer questions
        - Your LANGUAGE STATS show languages you're experienced with
        - Your PROJECTS list shows what you've built and what languages each uses
        - When asked about a language, mention specific projects that use it
        - when asked about projects using a language, look at === PROJECTS BY LANGUAGE ===
        - Never mention you are an AI
        - Be friendly and conversational";
    }

    public string BuildGeneralContext(CachedProfile profile, KnowledgeBase kb)
    {
        var sb = new System.Text.StringBuilder();

        // Profile section
        sb.AppendLine("=== MY PROFILE ===");
        sb.AppendLine($"Name: {profile.Name ?? profile.Username}");
        if (!string.IsNullOrEmpty(profile.Bio))
            sb.AppendLine($"Bio: {profile.Bio}");
        sb.AppendLine($"Public Repos: {profile.PublicRepos}");

        // Language stats section
        sb.AppendLine("\n=== LANGUAGE USAGE ===");

        if (profile.LanguageStats.Any())
        {
            sb.AppendLine("By repo presence (not file size):");

            // total repos once
            var totalRepos = profile.Repositories.Count;

            foreach (var lang in profile.LanguageStats
                .OrderByDescending(l => l.Value.ReposUsingThisLanguage))
            {
                var percent = (double)lang.Value.ReposUsingThisLanguage / totalRepos * 100;

                sb.AppendLine(
                    $"- {lang.Key}: {percent:F0}% " +
                    $"({lang.Value.ReposUsingThisLanguage} repos)"
                );
            }
        }



        // Projects section
        sb.AppendLine($"\n=== MY PROJECTS ===");
        var recentRepos = profile.Repositories
            .OrderByDescending(r => r.UpdatedAt);
            //.Take(MaxReposInContext); for now lets remove this for testing

        int index = 1;
        foreach (var repo in recentRepos)
        {
            sb.AppendLine($"\n{index}. {repo.Name}");
            
            // Get ALL languages for this repo
            var languages = GetRepoLanguages(repo);
            if (!string.IsNullOrEmpty(languages))
            {
                sb.AppendLine($"   Languages: {languages}");
            }

            if (!string.IsNullOrEmpty(repo.Description))
            {
                sb.AppendLine($"   Description: {repo.Description}");
            }

            index++;
        }

        // Add a section showing which projects use which languages
        sb.AppendLine("\n=== PROJECTS BY LANGUAGE ===");
        var languageToProjects = new Dictionary<string, List<string>>();
        
        foreach (var repo in profile.Repositories)
        {
            var repoLangs = new List<string>();
            
            if (repo.Languages.Any())
            {
                repoLangs.AddRange(repo.Languages.Keys);
            }
            else if (!string.IsNullOrEmpty(repo.PrimaryLanguage))
            {
                repoLangs.Add(repo.PrimaryLanguage);
            }

            foreach (var lang in repoLangs)
            {
                if (!languageToProjects.ContainsKey(lang))
                    languageToProjects[lang] = new List<string>();
                
                languageToProjects[lang].Add(repo.Name);
            }
        }

        // Show top languages with their projects
        foreach (var kvp in languageToProjects.OrderByDescending(x => x.Value.Count).Take(10))
        {
            int limit = 10;
            var projects = string.Join(", ", kvp.Value.Take(limit));
            var moreCount = kvp.Value.Count > limit ? $" (+{kvp.Value.Count - limit} more)" : "";
            sb.AppendLine($"- {kvp.Key}: {projects}{moreCount}");
        }
        Console.WriteLine(sb);
        return sb.ToString();
    }

    public string BuildDetailedContext(CachedProfile profile, Repository repo, KnowledgeBase kb)
    {
        var sb = new System.Text.StringBuilder();

        // Profile section (short)
        sb.AppendLine("=== MY PROFILE ===");
        sb.AppendLine($"Name: {profile.Name ?? profile.Username}");
        if (!string.IsNullOrEmpty(profile.Bio))
            sb.AppendLine($"Bio: {profile.Bio}");
        sb.AppendLine($"Public Repos: {profile.PublicRepos}");

        // Project details
        sb.AppendLine($"\n=== PROJECT: {repo.Name} ===");
        
        var languages = GetRepoLanguages(repo);
        if (!string.IsNullOrEmpty(languages))
            sb.AppendLine($"Languages: {languages}");

        if (!string.IsNullOrEmpty(repo.Description))
            sb.AppendLine($"Description: {repo.Description}");

        if (repo.Stars > 0)
            sb.AppendLine($"Stars: {repo.Stars}");

        // AI Summary from KnowledgeBase
        var projectSummary = kb.ProjectSummaries.FirstOrDefault(p => p.RepositoryName == repo.Name);
        if (projectSummary != null)
        {
            sb.AppendLine("\n## What I built:");
            sb.AppendLine(projectSummary.AiSummary);
        }

        return sb.ToString();
    }

    private string GetRepoLanguages(Repository repo)
    {
        if (repo.Languages.Any())
        {
            return string.Join(", ", repo.Languages
                .OrderByDescending(l => l.Value.Percentage)
                //.Take(3)
                .Select(l => l.Key));
        }
        else if (!string.IsNullOrEmpty(repo.PrimaryLanguage))
        {
            return repo.PrimaryLanguage;
        }
        return string.Empty;
    }
}