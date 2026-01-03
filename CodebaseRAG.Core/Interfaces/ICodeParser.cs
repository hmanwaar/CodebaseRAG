using System.Collections.Generic;
using System.Threading.Tasks;
using CodebaseRAG.Core.Models;

namespace CodebaseRAG.Core.Interfaces
{
    public interface ICodeParser
    {
        Task<IEnumerable<CodeChunk>> ParseAsync(string content, string filePath);
    }
}
