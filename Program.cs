using System.Threading.RateLimiting;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using OpenBook.Configuration;
using OpenBook.Services.Interfaces;
using OpenBook.Services.Implementations;

var builder = WebApplication.CreateBuilder(args);

// CORS - allow both local dev and production
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(
                "https://sahandbuilds.netlify.app",
                "http://localhost:5165",
                "https://localhost:5165"
              )
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Rate Limiting - Fixed Window: 10 requests per minute per IP
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    options.AddFixedWindowLimiter("fixed", config =>
    {
        config.Window = TimeSpan.FromMinutes(1);
        config.PermitLimit = 10;
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 0;
    });
});

// Add controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Options Pattern
builder.Services.Configure<GitHubOptions>(
    builder.Configuration.GetSection("GitHub"));

builder.Services.Configure<AzureStorageOptions>(
    builder.Configuration.GetSection("AzureStorage"));

builder.Services.Configure<AzureOpenAIOptions>(
    builder.Configuration.GetSection("AzureOpenAI"));

builder.Services.Configure<CacheSettings>(
    builder.Configuration.GetSection("CacheSettings"));

// Register HttpClient
builder.Services.AddHttpClient("GitHub", client =>
{
    client.BaseAddress = new Uri("https://api.github.com");
    client.DefaultRequestHeaders.Add("User-Agent", "OpenBook-API");
    client.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
});

// Register Azure OpenAI ChatClient
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;
    var azureClient = new AzureOpenAIClient(
        new Uri(options.Endpoint),
        new AzureKeyCredential(options.ApiKey));
    return azureClient.GetChatClient(options.DeploymentName);
});

// Register Services
builder.Services.AddScoped<IGitHubService, GitHubService>();
builder.Services.AddScoped<IKnowledgeBaseService, AzureOpenAIService>();
builder.Services.AddScoped<IBlobStorageService, AzureBlobService>();

// Chat Services
builder.Services.AddSingleton<IntentDetector>();
builder.Services.AddSingleton<ChatContextBuilder>();
builder.Services.AddScoped<IChatService, ChatService>();

var app = builder.Build();

// Middleware order matters!
app.UseCors();
app.UseRateLimiter();

// Serve static files (HTML/CSS/JS)
app.UseDefaultFiles();
app.UseStaticFiles();

// Enable Swagger at /swagger
app.UseSwagger();
app.UseSwaggerUI(c => 
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "OpenBook API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();