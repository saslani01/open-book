# OpenBook 📖

An AI-powered chat interface that lets you have conversations with GitHub developers through their code and documentation.

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?style=flat&logo=dotnet)
![Azure](https://img.shields.io/badge/Azure-OpenAI-0078D4?style=flat&logo=microsoft-azure)
![License](https://img.shields.io/badge/License-MIT-green.svg)

## Overview

OpenBook scrapes GitHub profiles, analyzes repositories using AI, and creates intelligent personas that can answer questions about a developer's work, skills, and projects. Ask about their tech stack, dive deep into specific projects, or explore their coding experience.

### Key Features

- **GitHub Profile Scraping** - Fetches user data, repositories, languages, and README files
- **AI-Powered Knowledge Base** - Generates summaries of each repository using Azure OpenAI
- **Smart Intent Detection** - AI classifies questions as general or project-specific
- **Intelligent Context Building** - Optimizes token usage by sending relevant context only
- **Multi-Session Chat** - Support for multiple concurrent chat sessions
- **Caching System** - Profiles and knowledge bases cached in Azure Blob Storage

## Architecture
```
┌─────────────────────────────────────────────────────────────┐
│                        Frontend                             │
│                   (Minimal Demo UI)                         │
└─────────────────────┬───────────────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────────────┐
│                     ASP.NET Core API                        │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │   GitHub    │  │    Chat     │  │    Knowledge Base   │  │
│  │  Controller │  │  Controller │  │      Controller     │  │
│  └──────┬──────┘  └──────┬──────┘  └──────────┬──────────┘  │
└─────────┼────────────────┼────────────────────┼─────────────┘
          │                │                    │
┌─────────▼────────────────▼────────────────────▼─────────────┐
│                      Services Layer                         │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐  │
│  │   GitHub    │  │    Chat     │  │   Knowledge Base    │  │
│  │   Service   │  │   Service   │  │      Service        │  │
│  └─────────────┘  └──────┬──────┘  └─────────────────────┘  │
│                          │                                  │
│  ┌─────────────┐  ┌──────▼──────┐  ┌─────────────────────┐  │
│  │   Intent    │  │   Context   │  │       Blob          │  │
│  │  Detector   │  │   Builder   │  │      Storage        │  │
│  └─────────────┘  └─────────────┘  └─────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
          │                │                    │
          ▼                ▼                    ▼
   ┌──────────┐    ┌──────────────┐    ┌──────────────┐
   │  GitHub  │    │ Azure OpenAI │    │ Azure Blob   │
   │   API    │    |(GPT-4o-)mini │    │   Storage    │
   └──────────┘    └──────────────┘    └──────────────┘
```

## Tech Stack

| Layer | Technology |
|-------|------------|
| **Framework** | ASP.NET Core 9.0 |
| **Language** | C# 12 |
| **AI** | Azure OpenAI (GPT-4o-mini) |
| **Storage** | Azure Blob Storage |
| **External API** | GitHub REST API v3 |
| **Frontend** | Vanilla HTML/CSS/JS (demo only) |

## Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download)
- [Azure Account](https://azure.microsoft.com/free/) with:
  - Azure OpenAI Service
  - Azure Storage Account
- [GitHub Personal Access Token](https://github.com/settings/tokens) (optional, for higher rate limits)

### Installation

1. **Clone the repository**
```bash
   git clone https://github.com/yourusername/OpenBook.git
   cd OpenBook
```

2. **Configure settings**
   
   Create `appsettings.Development.json`:
```json
   {
     "GitHub": {
       "AccessToken": "your-github-token"
     },
     "AzureStorage": {
       "ConnectionString": "your-connection-string",
       "ProfileContainer": "profiles",
       "KnowledgeBaseContainer": "knowledgebases",
       "ChatSessionContainer": "chatsessions"
     },
     "AzureOpenAI": {
       "Endpoint": "https://your-resource.openai.azure.com/",
       "ApiKey": "your-api-key",
       "DeploymentName": "gpt-4o-mini"
     },
     "CacheSettings": {
       "ProfileCacheHours": 24,
       "KnowledgeBaseCacheHours": 168
     }
   }
```

3. **Install dependencies**
```bash
   dotnet restore
```

4. **Run the application**
```bash
   dotnet run
```

5. **Open in browser**
   - UI: http://localhost:5165
   - Swagger: http://localhost:5165/swagger

## API Endpoints

### Chat

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/chat/{username}/start` | Start a new chat session |
| `POST` | `/api/chat/send?sessionId={id}` | Send a message |
| `GET` | `/api/chat/session/{sessionId}` | Get session history |
| `GET` | `/api/chat/{username}/sessions` | List all sessions for user |
| `DELETE` | `/api/chat/session/{sessionId}` | Delete a session |

### GitHub

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/github/{username}` | Fetch and cache profile |
| `GET` | `/api/github/{username}/rate-limit` | Check API rate limit |

### Knowledge Base

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/api/knowledgebase/{username}/generate` | Generate AI knowledge base |
| `GET` | `/api/knowledgebase/{username}` | Get cached knowledge base |

## How It Works

### 1. Profile Scraping
When you start a chat, OpenBook fetches the GitHub user's:
- Profile information (bio, location, stats)
- All public repositories
- README content for each repo
- Language statistics per repo

### 2. Knowledge Base Generation
For each repository with a README, Azure OpenAI generates a summary covering:
- Project purpose and features
- Technical implementation details
- Technologies used
- Deployment/usage instructions

### 3. Intent Detection
When you send a message, AI classifies it:
- **General**: Questions about skills, experience, background
- **Detailed**: Questions about a specific project

### 4. Context Building
Based on intent, the system builds optimized context:

| Intent | Context Sent | ~Tokens |
|--------|--------------|---------|
| General | Profile + language stats + repo list | ~400 |
| Detailed | Profile + specific repo's AI summary | ~700 |

### 5. Response Generation
The AI responds as the developer persona, using only the provided context.

## Project Structure
```
OpenBook/
├── Controllers/
│   ├── ChatController.cs
│   ├── GitHubController.cs
│   └── KnowledgeBaseController.cs
├── Models/
│   ├── CachedProfile.cs
│   ├── ChatSession.cs
│   ├── KnowledgeBase.cs
│   └── Repository.cs
├── Services/
│   ├── Interfaces/
│   │   ├── IBlobStorageService.cs
│   │   ├── IChatService.cs
│   │   ├── IGitHubService.cs
│   │   └── IKnowledgeBaseService.cs
│   └── Implementations/
│       ├── AzureBlobService.cs
│       ├── AzureOpenAIService.cs
│       ├── ChatContextBuilder.cs
│       ├── ChatService.cs
│       ├── GitHubService.cs
│       └── IntentDetector.cs
├── Configuration/
│   ├── AzureOpenAIOptions.cs
│   ├── AzureStorageOptions.cs
│   ├── CacheSettings.cs
│   └── GitHubOptions.cs
├── wwwroot/
│   └── index.html
├── Program.cs
└── appsettings.json
```

## Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `ProfileCacheHours` | Hours before re-scraping profile | 24 |
| `KnowledgeBaseCacheHours` | Hours before regenerating KB | 168 (7 days) |

## Token Optimization

OpenBook is designed to minimize API costs:

| Optimization | Savings |
|--------------|---------|
| General context: repo names only (no AI summaries) | ~80% |
| Detailed context: single repo summary (not README) | ~60% |
| Chat history limited to last 10 messages | Variable |
| AI intent detection (~50 tokens) vs wrong context (~5000 tokens) | ~99% |

## Limitations

- GitHub API rate limits (60/hour unauthenticated, 5000/hour with token)
- Azure OpenAI token limits vary by tier
- Only analyzes public repositories
- Frontend is a minimal demo (focus is backend API)

## Future Improvements

- [ ] Add authentication/rate limiting for API
- [ ] Support for private repositories (with OAuth)
- [ ] Streaming responses
- [ ] Conversation memory/persistence
- [ ] Deploy to Azure App Service
- [ ] Add unit tests

## License

MIT License - see [LICENSE](LICENSE) for details.

## Acknowledgments

- Built with [Azure OpenAI Service](https://azure.microsoft.com/en-us/products/ai-services/openai-service)
- GitHub data via [GitHub REST API](https://docs.github.com/en/rest)