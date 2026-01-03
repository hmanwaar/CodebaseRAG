using System.Collections.Generic;
using System.Threading.Tasks;
using CodebaseRAG.Core.Models;

namespace CodebaseRAG.Core.Interfaces
{
    public interface IVectorDbService
    {
        Task UpsertChunksAsync(IEnumerable<CodeChunk> chunks);
        Task<IEnumerable<SearchResult>> SearchAsync(float[] queryEmbedding, int limit = 5);
        Task<int> CountAsync();
        Task ClearAsync();
        Task<IEnumerable<string>> GetAllFilesAsync();
        Task<DateTime?> GetLastModifiedAsync(string filePath);
        Task DeleteFileChunksAsync(string filePath);
    }
}
