using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodebaseRAG.Core.Models;
using CodebaseRAG.Infrastructure.Services;
using Xunit;

namespace CodebaseRAG.Tests
{
    public class InMemoryVectorDbTests
    {
        [Fact]
        public async Task SearchAsync_ReturnsRelevantChunks()
        {
            // Arrange
            var db = new InMemoryVectorDb();
            var chunk1 = new CodeChunk { Id = "1", Content = "Apple", Embedding = new float[] { 1, 0, 0 } };
            var chunk2 = new CodeChunk { Id = "2", Content = "Banana", Embedding = new float[] { 0, 1, 0 } };
            var chunk3 = new CodeChunk { Id = "3", Content = "Orange", Embedding = new float[] { 0, 0, 1 } };

            await db.UpsertChunksAsync(new[] { chunk1, chunk2, chunk3 });

            // Act
            // Query for "Apple" (1, 0, 0)
            var results = await db.SearchAsync(new float[] { 1, 0, 0 }, limit: 1);

            // Assert
            Assert.Single(results);
            Assert.Equal("1", results.First().Chunk.Id);
            Assert.Equal(1, results.First().Similarity, 3);
        }
    }
}
