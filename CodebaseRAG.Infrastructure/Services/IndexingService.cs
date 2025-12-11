using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodebaseRAG.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace CodebaseRAG.Infrastructure.Services
{
    public class IndexingService
    {
        private readonly ICodeCrawler _crawler;
        private readonly IEmbeddingService _embeddingService;
        private readonly IVectorDbService _vectorDb;
        private readonly ILogger<IndexingService> _logger;
        private CancellationTokenSource? _cts;

        public IndexingStatus Status { get; private set; } = new();

        public IndexingService(
            ICodeCrawler crawler,
            IEmbeddingService embeddingService,
            IVectorDbService vectorDb,
            ILogger<IndexingService> logger)
        {
            _crawler = crawler;
            _embeddingService = embeddingService;
            _vectorDb = vectorDb;
            _logger = logger;
        }

        public void StartIndexing(string rootPath, string[]? excludePatterns = null)
        {
            if (Status.IsIndexing)
            {
               _logger.LogWarning("Indexing already in progress.");
               return; 
            }

            _cts = new CancellationTokenSource();
            _ = Task.Run(() => IndexCodebaseAsync(rootPath, _cts.Token, excludePatterns));
        }

        public void CancelIndexing()
        {
            if (_cts != null && !_cts.IsCancellationRequested && Status.IsIndexing)
            {
                _cts.Cancel();
                _logger.LogInformation("Cancellation requested.");
                Status.Message = "Cancelling...";
            }
        }

        private async Task IndexCodebaseAsync(string rootPath, CancellationToken token, string[]? excludePatterns = null)
        {
            // Sanitize path: Remove quotes if user added them
            rootPath = rootPath.Trim('"').Trim('\'');
            
            Status = new IndexingStatus { IsIndexing = true, Message = "Scanning files..." };
            _logger.LogInformation("Starting indexing for path: '{Path}'", rootPath);
            
            if (!Directory.Exists(rootPath))
            {
                _logger.LogError("Directory does not exist: '{Path}'", rootPath);
                Status.IsIndexing = false;
                Status.Message = $"Error: Directory not found: {rootPath}";
                return;
            }

            try 
            {
                var files = await _crawler.ScanDirectoryAsync(rootPath, excludePatterns);
                var fileList = files.ToList();
                Status.TotalFiles = fileList.Count;
                Status.Message = $"Found {Status.TotalFiles} files. Starting processing...";
                _logger.LogInformation("Found {Count} files", Status.TotalFiles);

                int processedCount = 0;
                foreach (var file in fileList)
                {
                    if (token.IsCancellationRequested)
                    {
                        Status.Message = "Indexing cancelled by user.";
                        Status.IsIndexing = false;
                        Status.CurrentFile = "";
                         _logger.LogInformation("Indexing cancelled.");
                        return;
                    }

                    Status.CurrentFile = file;
                    Status.Message = $"Processing {processedCount + 1}/{Status.TotalFiles}: {System.IO.Path.GetFileName(file)}";
                    
                    try 
                    {
                        // Pass token if crawler supports it, otherwise just check before/after
                        if (token.IsCancellationRequested) break;

                        var chunks = await _crawler.ProcessFileAsync(file);
                        foreach (var chunk in chunks)
                        {
                            if (token.IsCancellationRequested) break;
                            chunk.Embedding = await _embeddingService.GenerateEmbeddingAsync(chunk.Content);
                        }

                        if (chunks.Any() && !token.IsCancellationRequested)
                        {
                            await _vectorDb.UpsertChunksAsync(chunks);
                        }
                        
                        if (!token.IsCancellationRequested)
                        {
                            processedCount++;
                            Status.ProcessedFiles = processedCount;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to index file {File}", file);
                    }
                }

                if (!token.IsCancellationRequested)
                {
                    Status.IsIndexing = false;
                    Status.Message = $"Completed. Indexed {processedCount} files.";
                    Status.CurrentFile = string.Empty;
                    _logger.LogInformation("Indexing completed. Processed {Count} files.", processedCount);
                }
            }
            catch (Exception ex)
            {
                Status.IsIndexing = false;
                Status.Message = $"Failed: {ex.Message}";
                _logger.LogError(ex, "Indexing failed");
            }
        }
    }

    public class IndexingStatus
    {
        public bool IsIndexing { get; set; }
        public string Message { get; set; } = "Idle";
        public int TotalFiles { get; set; }
        public int ProcessedFiles { get; set; }
        public string CurrentFile { get; set; } = string.Empty;
    }
}
