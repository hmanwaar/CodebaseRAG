namespace CodebaseRAG.Core.Models
{
    public class SearchResult
    {
        public CodeChunk Chunk { get; set; } = new();
        public double Similarity { get; set; }
    }
}
