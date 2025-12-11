using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CodebaseRAG.Core.Interfaces;
using Npgsql;

namespace CodebaseRAG.Infrastructure.Repositories
{
    public class TextRepository
    {
        private readonly string _connectionString;
        private readonly IEmbeddingService _embeddingService;

        public TextRepository(string connectionString, IEmbeddingService embeddingService)
        {
            _connectionString = connectionString;
            _embeddingService = embeddingService;
        }

        public async Task StoreTextAsync(string content)
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(content);

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            // Ensure table exists - simple migration for demo purposes
            var createTableCmd = new NpgsqlCommand(@"
                CREATE TABLE IF NOT EXISTS text_contexts (
                    id SERIAL PRIMARY KEY,
                    content TEXT NOT NULL,
                    embedding vector(384) -- Assuming default dimension for mistral/nomic-embed-text
                );
            ", conn);
            await createTableCmd.ExecuteNonQueryAsync();

            string query = "INSERT INTO text_contexts (content, embedding) VALUES (@content, @embedding)";
            using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("content", content);
            cmd.Parameters.AddWithValue("embedding", embedding); 
            // NOTE: This assumes Npgsql maps float[] to vector, OR that pgvector extension allows implicit cast from float array literal.
            // If this fails at runtime, we might need Pgvector.Npgsql package or string formatting like in retrieval. 

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<List<string>> RetrieveRelevantText(string query)
        {
            var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);

            using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            string querySql = @"
                SELECT content 
                FROM text_contexts 
                WHERE embedding <-> CAST(@queryEmbedding AS vector) > 0.7 
                ORDER BY embedding <-> CAST(@queryEmbedding AS vector) 
                LIMIT 5";

            using var cmd = new NpgsqlCommand(querySql, conn);

            // User's retrieval code construction:
            string embeddingString = $"[{string.Join(",", queryEmbedding.Select(v => v.ToString("G", CultureInfo.InvariantCulture)))}]";
            cmd.Parameters.AddWithValue("queryEmbedding", embeddingString);
            
            using var reader = await cmd.ExecuteReaderAsync();
            var results = new List<string>();
            while (await reader.ReadAsync())
            {
                results.Add(reader.GetString(0));
            }

            return results.Any() ? results : new List<string> { "No relevant context found." };
        }
    }
}
