using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodebaseRAG.Core.Interfaces;
using CodebaseRAG.Core.Models;

namespace CodebaseRAG.Infrastructure.Services
{
    public class InMemoryVectorDb : IVectorDbService
    {
        // Simple in-memory storage: List of chunks
        private readonly List<CodeChunk> _chunks = new();
        private readonly object _lock = new();

        public Task UpsertChunksAsync(IEnumerable<CodeChunk> chunks)
        {
            lock (_lock)
            {
                // For simplicity in this MVP, we just add. In a real DB we'd update existing IDs.
                // We'll remove duplicates by ID if any
                foreach (var chunk in chunks)
                {
                    var existing = _chunks.FirstOrDefault(c => c.Id == chunk.Id);
                    if (existing != null)
                    {
                        _chunks.Remove(existing);
                    }
                    _chunks.Add(chunk);
                }
            }
            return Task.CompletedTask;
        }

        public Task<IEnumerable<SearchResult>> SearchAsync(float[] queryEmbedding, int limit = 5)
        {
            // Brute-force cosine similarity
            var results = new List<SearchResult>();

            lock (_lock)
            {
                foreach (var chunk in _chunks)
                {
                    if (chunk.Embedding == null) continue;

                    var similarity = CosineSimilarity(queryEmbedding, chunk.Embedding);
                    results.Add(new SearchResult { Chunk = chunk, Similarity = similarity });
                }
            }

            return Task.FromResult(results.OrderByDescending(r => r.Similarity).Take(limit));
        }

        public Task<int> CountAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(_chunks.Count);
            }
        }

        public Task ClearAsync()
        {
            lock (_lock)
            {
                _chunks.Clear();
            }
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> GetAllFilesAsync()
        {
            lock (_lock)
            {
                var files = _chunks.Select(c => c.FilePath).Distinct().OrderBy(f => f).ToList();
                return Task.FromResult((IEnumerable<string>)files);
            }
        }

        public Task<DateTime?> GetLastModifiedAsync(string filePath)
        {
            lock (_lock)
            {
                var chunk = _chunks.FirstOrDefault(c => c.FilePath == filePath);
                return Task.FromResult(chunk?.LastModified);
            }
        }

        public Task DeleteFileChunksAsync(string filePath)
        {
            lock (_lock)
            {
                _chunks.RemoveAll(c => c.FilePath == filePath);
            }
            return Task.CompletedTask;
        }

        private double CosineSimilarity(float[] vectorA, float[] vectorB)
        {
            if (vectorA.Length != vectorB.Length) return 0;

            double dotProduct = 0;
            double normA = 0;
            double normB = 0;

            for (int i = 0; i < vectorA.Length; i++)
            {
                dotProduct += vectorA[i] * vectorB[i];
                normA += vectorA[i] * vectorA[i];
                normB += vectorB[i] * vectorB[i];
            }

            if (normA == 0 || normB == 0) return 0;

            return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }
    }
}
