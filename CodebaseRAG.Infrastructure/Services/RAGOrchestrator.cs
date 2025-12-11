using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CodebaseRAG.Core.Interfaces;
using CodebaseRAG.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodebaseRAG.Infrastructure.Services
{
    public class RAGOrchestrator
    {
        private readonly IEmbeddingService _embeddingService;
        private readonly IVectorDbService _vectorDb;
        private readonly OllamaService _ollamaService;
        private readonly ILogger<RAGOrchestrator> _logger;

        public RAGOrchestrator(
            IEmbeddingService embeddingService,
            IVectorDbService vectorDb,
            OllamaService ollamaService,
            ILogger<RAGOrchestrator> logger)
        {
            _embeddingService = embeddingService;
            _vectorDb = vectorDb;
            _ollamaService = ollamaService;
            _logger = logger;
        }

        public async Task<string> AskQuestionAsync(string question)
        {
            // Check if we have any indexed content
            var allFiles = await _vectorDb.GetAllFilesAsync();
            var fileCount = allFiles.Count();
            _logger.LogInformation("RAG Context: Found {Count} total files in index.", fileCount);

            // Check if embedding service is healthy
            var embeddingService = _embeddingService as OllamaService;
            bool isEmbeddingServiceHealthy = embeddingService != null && await embeddingService.IsServiceHealthyAsync();

            if (fileCount == 0 || !isEmbeddingServiceHealthy)
            {
                // Handle case where no files are indexed or embedding service is down
                var fileList1 = string.Join("\n", allFiles.Take(50));
                if (fileCount > 50) fileList1 += $"\n... and {fileCount - 50} more files.";

                var systemPrompt1 = new StringBuilder();
                systemPrompt1.AppendLine("You are a helpful AI assistant that answers questions about a codebase.");

                if (fileCount == 0)
                {
                    systemPrompt1.AppendLine("No files have been indexed yet. The codebase is empty.");
                    systemPrompt1.AppendLine("Suggest that the user index their codebase first.");
                }
                else if (!isEmbeddingServiceHealthy)
                {
                    systemPrompt1.AppendLine($"The codebase contains {fileCount} indexed files, but the embedding service is currently unavailable.");
                    systemPrompt1.AppendLine("Here is a list of files in the codebase:");
                    systemPrompt1.AppendLine(fileList1);
                    systemPrompt1.AppendLine();
                    systemPrompt1.AppendLine("Since the embedding service is unavailable, you cannot retrieve specific code content.");
                    systemPrompt1.AppendLine("Answer general questions about the codebase structure, but be clear that you cannot access file contents.");
                    systemPrompt1.AppendLine("Suggest that the user check if the Ollama service is running properly.");
                }

                _logger.LogWarning("RAG system operating in degraded mode. Files indexed: {FileCount}, Embedding service healthy: {IsHealthy}", fileCount, isEmbeddingServiceHealthy);
                return await _ollamaService.ChatAsync(question, systemPrompt1.ToString());
            }

            // 1. Embed question
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(question);

            // Check if we got a zero vector (fallback embedding)
            bool isZeroVector = queryEmbedding.All(x => x == 0);
            if (isZeroVector)
            {
                _logger.LogWarning("Received zero vector embedding - embedding service may be degraded");

                var fileList2 = string.Join("\n", allFiles.Take(50));
                if (fileCount > 50) fileList2 += $"\n... and {fileCount - 50} more files.";

                var systemPrompt2 = new StringBuilder();
                systemPrompt2.AppendLine("You are a helpful AI assistant that answers questions about a codebase.");
                systemPrompt2.AppendLine($"The codebase contains {fileCount} files.");
                systemPrompt2.AppendLine("Here is a list of files in the codebase:");
                systemPrompt2.AppendLine(fileList2);
                systemPrompt2.AppendLine();
                systemPrompt2.AppendLine("WARNING: The embedding service returned a zero vector, which means specific code content cannot be retrieved.");
                systemPrompt2.AppendLine("Answer questions based on the file list and general knowledge, but do not claim to have accessed specific file contents.");
                systemPrompt2.AppendLine("Be clear that detailed code analysis is unavailable due to technical issues.");

                return await _ollamaService.ChatAsync(question, systemPrompt2.ToString());
            }

            // 2. Retrieve relevant chunks
            var results = await _vectorDb.SearchAsync(queryEmbedding, limit: 5);

            // Check if we got meaningful results
            bool hasMeaningfulResults = results.Any(r => r.Similarity > 0.1); // Low threshold for similarity

            // 2.1 Retrieve all files for context (limited to avoid context overflow)
            var fileList = string.Join("\n", allFiles.Take(100)); // Limit to top 100 files to save tokens
            if (fileCount > 100) fileList += $"\n... and {fileCount - 100} more files.";

            // 3. Construct Prompt
            var contextBuilder = new StringBuilder();
            contextBuilder.AppendLine("You are a helpful AI assistant that answers questions about a codebase based on the provided context.");
            contextBuilder.AppendLine($"The codebase contains {fileCount} files.");
            contextBuilder.AppendLine("Here is a list of files in the codebase:");
            contextBuilder.AppendLine(fileList);
            contextBuilder.AppendLine();

            if (hasMeaningfulResults)
            {
                contextBuilder.AppendLine("Use the following code snippets to answer the user's question.");
                contextBuilder.AppendLine("If the answer is not in the context, say so.");
                contextBuilder.AppendLine("\nContext:");

                foreach (var result in results)
                {
                    contextBuilder.AppendLine($"--- File: {result.Chunk.FileName} (Lines {result.Chunk.StartLine}-{result.Chunk.EndLine}, Similarity: {result.Similarity:F3}) ---");
                    contextBuilder.AppendLine(result.Chunk.Content);
                    contextBuilder.AppendLine();
                }
            }
            else
            {
                contextBuilder.AppendLine("No relevant code snippets were found for this question.");
                contextBuilder.AppendLine("Answer based on the file list and general knowledge, but be clear that specific code content was not retrieved.");
            }

            var systemPrompt = contextBuilder.ToString();
            _logger.LogInformation("Generated System Prompt (Length: {Length}). Sending to LLM...", systemPrompt.Length);

            // 4. Call LLM
            return await _ollamaService.ChatAsync(question, systemPrompt);
        }
    }
}
