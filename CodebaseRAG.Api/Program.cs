using CodebaseRAG.Core.Interfaces;
using CodebaseRAG.Infrastructure.Services;
using CodebaseRAG.Infrastructure.Repositories;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Get the CodebaseRAG solution directory for indexing
var currentDirectory = Directory.GetCurrentDirectory();
var solutionDirectory = FindSolutionDirectory(currentDirectory);

Console.WriteLine($"Indexing solution directory: {solutionDirectory}");

static string FindSolutionDirectory(string startPath)
{
    var dir = new DirectoryInfo(startPath);
    while (dir != null)
    {
        if (dir.GetFiles("*.sln").Length > 0)
        {
            return dir.FullName;
        }
        dir = dir.Parent;
    }
    return startPath; // fallback to start path if no solution found
}

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Core Services
builder.Services.AddSingleton<IVectorDbService, InMemoryVectorDb>();
builder.Services.AddTransient<ICodeCrawler, FileSystemCrawler>();

// Infrastructure Services - Simplified HTTP client configuration
builder.Services.AddHttpClient<OllamaService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "CodebaseRAG/1.0");
});

builder.Services.AddTransient<IEmbeddingService>(sp => sp.GetRequiredService<OllamaService>());
builder.Services.AddTransient<OllamaService>(); // Register concrete type for Orchestrator

// Orchestration
builder.Services.AddSingleton<IndexingService>();
builder.Services.AddTransient<RAGOrchestrator>();

// RAG Services
builder.Services.AddSingleton<TextRepository>(sp => 
    new TextRepository(
        builder.Configuration.GetConnectionString("PostgreSQL") ?? throw new InvalidOperationException("PostgreSQL connection string is missing."), 
        sp.GetRequiredService<IEmbeddingService>()
    ));
builder.Services.AddHttpClient<RagService>();
builder.Services.AddTransient<RagService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        builder => builder
            .WithOrigins("http://localhost:4200")
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngular");

app.UseAuthorization();

app.MapControllers();

// RAG Endpoints
app.MapPost("/add-text", async (TextRepository textRepository, [FromBody] AddTextRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request?.Content))
    {
        return Results.BadRequest("Content is required.");
    }
    await textRepository.StoreTextAsync(request.Content);
    return Results.Ok("Text added successfully.");
});

app.MapGet("/ask", async (RagService ragService, string query) =>
{
    var answer = await ragService.GetAnswerAsync(query);
    return Results.Ok(answer);
});

// Start automatic indexing in the background
var indexingService = app.Services.GetRequiredService<IndexingService>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

logger.LogInformation("Starting automatic indexing for directory: {Directory}", solutionDirectory);

// Start indexing in background (non-blocking)
// Start indexing in background (non-blocking) - DISABLED per user request
// _ = Task.Run(async () =>
// {
//     try
//     {
//         await Task.Delay(2000); // Wait 2 seconds for app to fully start
        
//         // Define exclude patterns to skip build artifacts
//         var excludePatterns = new[] 
//         { 
//             "\\bin\\", 
//             "\\obj\\", 
//             "\\.git\\", 
//             "\\node_modules\\", 
//             "\\.vs\\",
//             "\\.vscode\\",
//             "\\TestResults\\",
//             "\\packages\\"
//         };
        
//         indexingService.StartIndexing(solutionDirectory, excludePatterns);
//         logger.LogInformation("Automatic indexing started for: {Directory}", solutionDirectory);
//     }
//     catch (Exception ex)
//     {
//         logger.LogError(ex, "Failed to start automatic indexing");
//     }
// });

app.Run();

record AddTextRequest(string Content);
