# OpenBook
🔗 https://openbook-hhaddpgnfcfqd3b2.canadacentral-01.azurewebsites.net/index.html

An AI-powered chat interface that lets you have conversations with GitHub developers through their repositories and documentation. (SOON: uploading resume feature for knowledge base)

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat\&logo=dotnet)
![Azure](https://img.shields.io/badge/Azure-OpenAI-0078D4?style=flat\&logo=microsoft-azure)
![License](https://img.shields.io/badge/License-MIT-green.svg)

## Overview

OpenBook scrapes GitHub profiles, analyzes repositories using AI, and creates intelligent personas that can answer questions about a developer's work, skills, and projects. Ask about their tech stack, dive deep into specific projects, or explore their coding experience.

## Key Features

* **GitHub Profile Scraping** – Fetches user data, repositories, languages, and README files
* **AI-Powered Knowledge Base** – Generates summaries of each repository using Azure OpenAI
* **Smart Intent Detection** – AI classifies questions as general or project-specific
* **Intelligent Context Building** – Optimizes token usage by sending only relevant context
* **Multi-Session Chat** – Supports multiple concurrent chat sessions
* **Persistent Storage** – Profiles, knowledge bases, and chat sessions stored in Azure Blob Storage
* **Rate Limiting** – Fixed window rate limiting to prevent API abuse
* **CORS Protection** – Restricted to allowed origins only

## Tech Stack

| Layer | Technology |
|------|------------|
| **Framework** | ASP.NET Core 9.0 |
| **Language** | C# |
| **AI** | Azure OpenAI (GPT-4.1-mini after gpt-4o-mini retirement and auto-upgrade) |
| **Storage** | Azure Blob Storage |
| **External API** | GitHub REST API |
| **Frontend** | Vanilla HTML/CSS/JS (demo only) |
| **Hosting** | Azure App Service (Web App) |

Note: Deployment name is `gpt-4o-mini`, but because of auto-upgrade 
after model retirement, the current model being used is `gpt-4.1-mini`.

## Getting Started

### Prerequisites

- .NET 9.0 SDK
- Azure CLI
- Azure Account for:
  - Azure OpenAI Service
  - Azure Storage Account (Blob)
  - Azure App Service
- GitHub Personal Access Token

> 🔐 **Secrets Management**  
> Use .NET User Secrets for local development:
>
> ```bash
> dotnet user-secrets init
> dotnet user-secrets set "KeyName" "SecretValue"
> ```

### Installation

1. **Clone the repository**
```bash
git clone https://github.com/saslani01/open-book.git
cd open-book
```

2. **Configure settings**

Create `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "GitHub": {
    "AccessToken": ""
  },
  "AzureStorage": {
    "ConnectionString": "",
    "ProfilesContainer": "github-profiles",
    "KnowledgeBasesContainer": "knowledge-bases",
    "ChatSessionsContainer": "chat-sessions"
  },
  "AzureOpenAI": {
    "Endpoint": "",
    "ApiKey": "",
    "DeploymentName": "gpt-4o-mini"
  },
  "CacheSettings": {
    "ProfileMaxAgeHours": 24
  }
}
```

3. **Run the application**

```bash
dotnet restore
dotnet run
```

4. **Open in browser**

* Demo Interface: `http://localhost:{port}/index.html`
* Endpoints: `http://localhost:{port}/swagger/index.html`

## API Endpoints

### Chat

| Method | Endpoint                        | Description              |
| ------ | ------------------------------- | ------------------------ |
| POST   | `/api/chat/{username}/start`    | Start a new chat session |
| POST   | `/api/chat/send?sessionId={id}` | Send a message           |
| GET    | `/api/chat/session/{sessionId}` | Get session history      |
| DELETE | `/api/chat/session/{sessionId}` | Delete a session         |

## Security

### Rate Limiting

The API uses ASP.NET Core's built-in rate limiting middleware with a fixed window policy:

| Policy | Limit | Applied To |
|--------|-------|------------|
| `fixed` | 10 requests/minute | All endpoints |

When limits are exceeded, the API returns `429 Too Many Requests`.

### CORS

Cross-Origin Resource Sharing is restricted to my portfolio wesbite and local host.

## How It Works

1. **Profile Scraping**
   Fetches GitHub profile data, repositories, README files, and language stats.

2. **Knowledge Base Generation**
   Azure OpenAI summarizes each repository and stores results in Blob Storage.

3. **Intent Detection**
   Determines whether a question is general or project-specific.

4. **Context Building**
   Builds minimal, scoped context to reduce token usage.

5. **Response Generation**
   AI responds as the developer persona using curated context only.
  
**NOTE:** Models are equipped with TokenUsage for logging and optimization.

## Project Structure

```
OpenBook
├── Configuration
│   ├── AzureOpenAIOptions.cs
│   ├── AzureStorageOptions.cs
│   ├── CacheSettings.cs
│   └── GitHubOptions.cs
├── Controllers
│   └── ChatController.cs 
├── Models
│   ├── CachedProfile.cs
│   ├── ChatInput.cs
│   ├── ChatMessage.cs
│   ├── ChatResponse.cs
│   ├── ChatSession.cs
│   ├── ErrorViewModel.cs
│   ├── KnowledgeBase.cs
│   ├── LanguageInfo.cs
│   ├── Repository.cs
│   └── TokenUsage.cs
├── Services
│   ├── Implementations
│   │   ├── AzureBlobService.cs
│   │   ├── AzureOpenAIService.cs
│   │   ├── ChatContextBuilder.cs
│   │   ├── ChatService.cs
│   │   ├── GitHubService.cs
│   │   └── IntentDetector.cs
│   └── Interfaces
│       ├── IBlobStorageService.cs
│       ├── IChatService.cs
│       ├── IGitHubService.cs
│       └── IKnowledgeBaseService.cs
├── wwwroot
│   └── index.html
├── Program.cs
├── OpenBook.csproj
└── OpenBook.sln
```

## Configuration Options

| Setting              | Description                      | Default |
| -------------------- | -------------------------------- | ------- |
| `ProfileMaxAgeHours` | Hours before re-scraping profile | 24      |

## Deployment

### Current Method (Manual Zip Deploy)

```bash
# Build for release
dotnet publish -c Release -o ./publish

# Create deployment package
cd publish
zip -r ../openbook-deploy.zip .
cd ..

# Deploy to Azure
az webapp deploy \
  --resource-group openbook-rg \
  --name OpenBook \
  --src-path openbook-deploy.zip \
  --type zip
```

### Production Setup

* Azure App Service (Linux)
* Azure OpenAI resource
* Azure Storage Account (Blob)
* Secrets stored in Azure App Settings (not in code)

### Deployment Improvements

The current manual deployment works but could be improved:

| Improvement | Description |
|-------------|-------------|
| **GitHub Actions CI/CD** | Automate build and deploy on push to `main`. Eliminates manual steps and ensures consistent deployments. |
| **Application Insights** | Add Azure Monitor for logging, performance tracking, and error alerting. |

## Limitations

* GitHub API rate limits apply
* Public repositories only
* Frontend is a minimal demo

## Future Improvements

* VERY IMPORTANT: seprate the background job of KB generation with sending HTTP request to avoid Azure 504 error for very large profiles
* Robust standalone frontend
* Resume upload for richer personas 
* CI/CD pipeline with GitHub Actions

## License

MIT License

## Acknowledgments

* Built with Microsoft Azure Services
* GitHub data via GitHub REST API
