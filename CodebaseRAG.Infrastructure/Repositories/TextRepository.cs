using System.Collections.Generic;
using System.Threading.Tasks;
using CodebaseRAG.Core.Interfaces;
using CodebaseRAG.Core.Models;

namespace CodebaseRAG.Infrastructure.Repositories
{
    public class TextRepository
    {
        private readonly IVectorDbService _vectorDb;
        private readonly IEmbeddingService _embeddingService;

        public TextRepository(IVectorDbService vectorDb, IEmbeddingService embeddingService)
        {
            _vectorDb = vectorDb;
            _embeddingService = embeddingService;
        }

        public async Task StoreTextAsync(CodeChunk chunk)
        {
            if (chunk.Embedding == null)
            {
                chunk.Embedding = await _embeddingService.GenerateEmbeddingAsync(chunk.Content);
            }

            await _vectorDb.UpsertChunksAsync(new[] { chunk });
        }

        public async Task<List<CodeChunk>> RetrieveRelevantChunksAsync(string query, string? languageFilter = null)
        {
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

            var searchResults = await _vectorDb.SearchAsync(queryEmbedding, 5);
            var chunks = searchResults.Select(sr => sr.Chunk).ToList();

            if (!string.IsNullOrEmpty(languageFilter))
            {
                chunks = chunks.Where(c => c.Language == languageFilter).ToList();
            }

            return chunks;
        }

        // Kept for backward compatibility if needed, but RagService should switch to above
        public async Task<List<string>> RetrieveRelevantText(string query, string? languageFilter = null)
        {
            var chunks = await RetrieveRelevantChunksAsync(query, languageFilter);
            return chunks.Select(c => c.Content).ToList();
        }

        public async Task<DateTime?> GetLastModifiedAsync(string filePath)
        {
            return await _vectorDb.GetLastModifiedAsync(filePath);
        }

        public async Task DeleteFileChunksAsync(string filePath)
        {
            await _vectorDb.DeleteFileChunksAsync(filePath);
        }
    }
}
