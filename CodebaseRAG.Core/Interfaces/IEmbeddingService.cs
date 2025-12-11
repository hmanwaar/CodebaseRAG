using System.Collections.Generic;
using System.Threading.Tasks;
using CodebaseRAG.Core.Models;

namespace CodebaseRAG.Core.Interfaces
{
    public interface IEmbeddingService
    {
        Task<float[]> GenerateEmbeddingAsync(string text);
        Task<float[][]> GenerateEmbeddingsAsync(IEnumerable<string> texts);
    }
}
