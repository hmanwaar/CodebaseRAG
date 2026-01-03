using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodebaseRAG.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CodebaseRAG.Infrastructure.Services
{
    public class IndexingService
    {
        private readonly CrawlerFactory _crawlerFactory;
        private readonly IEmbeddingService _embeddingService;
        private readonly IVectorDbService _vectorDb;
        private readonly ILogger<IndexingService> _logger;
        private readonly CodebaseRAG.Infrastructure.Repositories.TextRepository _repository;
        private CancellationTokenSource? _cts;
        private readonly int _maxParallelism;
        private readonly int _embeddingBatchSize;

        public IndexingStatus Status { get; private set; } = new();

        public IndexingService(
            CrawlerFactory crawlerFactory,
            IEmbeddingService embeddingService,
            IVectorDbService vectorDb,
            CodebaseRAG.Infrastructure.Repositories.TextRepository repository,
            ILogger<IndexingService> logger,
            IConfiguration configuration)
        {
            _crawlerFactory = crawlerFactory;
            _embeddingService = embeddingService;
            _vectorDb = vectorDb;
            _repository = repository;
            _logger = logger;
            
            // Configuration for parallelism and batching
            _maxParallelism = configuration.GetValue<int>("Indexing:MaxParallelism", Environment.ProcessorCount);
            _embeddingBatchSize = configuration.GetValue<int>("Indexing:EmbeddingBatchSize", 50);
            
            _logger.LogInformation("IndexingService configured with MaxParallelism: {MaxParallelism}, EmbeddingBatchSize: {BatchSize}",
                _maxParallelism, _embeddingBatchSize);
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
                // Create project-type specific crawler
                var crawler = _crawlerFactory.CreateCrawler(rootPath);
                
                var files = await crawler.ScanDirectoryAsync(rootPath, excludePatterns);
                var fileList = files.ToList();
                Status.TotalFiles = fileList.Count;
                Status.Message = $"Found {Status.TotalFiles} files. Starting processing...";
                _logger.LogInformation("Found {Count} files", Status.TotalFiles);

                // Use concurrent collection for thread-safe processing
                var chunksToEmbed = new ConcurrentBag<CodebaseRAG.Core.Models.CodeChunk>();
                var filesProcessed = new ConcurrentDictionary<string, bool>();
                
                // Parallel options with limited concurrency
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = _maxParallelism,
                    CancellationToken = token
                };

                // Process files in parallel
                await Parallel.ForEachAsync(fileList, parallelOptions, async (file, cancellationToken) =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    Status.CurrentFile = file;
                    Status.Message = $"Processing {filesProcessed.Count + 1}/{Status.TotalFiles}: {System.IO.Path.GetFileName(file)}";
                    
                    try 
                    {
                        // INCREMENTAL INDEXING CHECK
                        var fileInfo = new System.IO.FileInfo(file);
                        var lastModLocal = fileInfo.LastWriteTimeUtc; // Use UTC
                        DateTime? lastModRemote = null;
                        try
                        {
                            lastModRemote = await _repository.GetLastModifiedAsync(file);
                        }
                        catch (Exception lastModEx)
                        {
                            Console.WriteLine($"[IndexingService] Warning: Failed to check remote modification time for {file}: {lastModEx.Message}");
                        }

                        // If remote exists and local is not newer, skip
                        if (lastModRemote.HasValue && lastModLocal <= lastModRemote.Value)
                        {
                            _logger.LogDebug("Skipping unmodified file: {File}", file);
                            filesProcessed.TryAdd(file, true);
                            Status.ProcessedFiles = filesProcessed.Count;
                            return;
                        }

                        // If we are here, file is new or modified. Delete old chunks if modified
                        if (lastModRemote.HasValue)
                        {
                            await _repository.DeleteFileChunksAsync(file);
                            _logger.LogInformation("Re-indexing modified file: {File}", file);
                        }

                        var chunks = await crawler.ProcessFileAsync(file);
                        
                        // Add file timestamp to each chunk
                        foreach (var chunk in chunks)
                        {
                            chunk.LastModified = lastModLocal;
                            chunksToEmbed.Add(chunk);
                        }
                        
                        filesProcessed.TryAdd(file, true);
                        Status.ProcessedFiles = filesProcessed.Count;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to index file {File}", file);
                    }
                });

                // Process embeddings in batches
                if (!token.IsCancellationRequested && chunksToEmbed.Any())
                {
                    Status.Message = "Generating embeddings...";
                    _logger.LogInformation("Starting embedding generation for {Count} chunks", chunksToEmbed.Count);
                    
                    // Convert concurrent bag to list for batching
                    var allChunks = chunksToEmbed.ToList();
                    
                    // Process in batches
                    for (int i = 0; i < allChunks.Count; i += _embeddingBatchSize)
                    {
                        if (token.IsCancellationRequested)
                        {
                            break;
                        }
                        
                        var batch = allChunks.Skip(i).Take(_embeddingBatchSize).ToList();
                        List<string> batchContents = batch.Select(c => c.Content).ToList();
                        
                        Status.Message = $"Generating embeddings batch {i/_embeddingBatchSize + 1}/{Math.Ceiling((double)allChunks.Count/_embeddingBatchSize)}...";
                        
                        try
                        {
                            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(batchContents);
                            
                            // Assign embeddings back to chunks
                            for (int j = 0; j < batch.Count; j++)
                            {
                                batch[j].Embedding = embeddings[j];
                            }
                            
                            // Store chunks in database
                            foreach (var chunk in batch)
                            {
                                await _repository.StoreTextAsync(chunk);
                            }
                            
                            _logger.LogDebug("Processed batch {BatchIndex} with {BatchSize} chunks", 
                                i/_embeddingBatchSize + 1, batch.Count);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to process embedding batch {BatchIndex}", i/_embeddingBatchSize + 1);
                        }
                    }
                }

                if (!token.IsCancellationRequested)
                {
                    Status.IsIndexing = false;
                    Status.Message = $"Completed. Indexed {filesProcessed.Count} files with {chunksToEmbed.Count} chunks.";
                    Status.CurrentFile = string.Empty;
                    _logger.LogInformation("Indexing completed. Processed {Count} files with {ChunkCount} chunks.", 
                        filesProcessed.Count, chunksToEmbed.Count);
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
