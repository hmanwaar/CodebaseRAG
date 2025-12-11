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
    }
}
