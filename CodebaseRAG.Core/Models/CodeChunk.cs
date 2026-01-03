using System;

namespace CodebaseRAG.Core.Models
{
    public class CodeChunk
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public int StartLine { get; set; }
        public int EndLine { get; set; }
        public float[]? Embedding { get; set; }
        public DateTime LastModified { get; set; }
        
        // Metadata for code-aware RAG
        public string FunctionName { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new List<string>();
    }
}
