using System.Collections.Generic;
using System.Threading.Tasks;
using CodebaseRAG.Core.Models;

namespace CodebaseRAG.Core.Interfaces
{
    public interface ICodeCrawler
    {
        Task<IEnumerable<string>> ScanDirectoryAsync(string rootPath, string[]? excludePatterns = null);
        Task<IEnumerable<CodeChunk>> ProcessFileAsync(string filePath);
    }
}
