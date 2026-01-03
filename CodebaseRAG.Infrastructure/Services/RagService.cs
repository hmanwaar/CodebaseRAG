using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CodebaseRAG.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;

namespace CodebaseRAG.Infrastructure.Services
{
    public class RagService
    {
        private readonly TextRepository _textRepository;
        private readonly HttpClient _httpClient;
        private readonly Uri _ollamaUrl;
        private readonly string _modelId;

        public RagService(TextRepository textRepository, HttpClient httpClient, IConfiguration configuration)
        {
            _textRepository = textRepository;
            _httpClient = httpClient;
            
            var ollamaBaseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
            _ollamaUrl = new Uri(ollamaBaseUrl);
            _modelId = configuration["Ollama:ChatModel"] ?? "mistral";
        }

        public async Task<object> GetAnswerAsync(string query)
        {
            // Retrieve multiple relevant chunks with metadata
            var chunks = await _textRepository.RetrieveRelevantChunksAsync(query);

            if (!chunks.Any())
            {
                 return new
                 {
                     Context = "No relevant data found in the database.",
                     Response = "I don't know. No relevant code found to answer your question."
                 };
            }

            // Build rich context string
            var sb = new StringBuilder();
            foreach (var chunk in chunks)
            {
                sb.AppendLine("---");
                if (!string.IsNullOrEmpty(chunk.FileName)) sb.AppendLine($"File: {chunk.FileName}");
                if (!string.IsNullOrEmpty(chunk.ClassName)) sb.AppendLine($"Class: {chunk.ClassName}");
                if (!string.IsNullOrEmpty(chunk.FunctionName)) sb.AppendLine($"Method: {chunk.FunctionName}");
                sb.AppendLine($"Lines: {chunk.StartLine}-{chunk.EndLine}");
                sb.AppendLine("Code:");
                sb.AppendLine(chunk.Content);
                sb.AppendLine();
            }
            string combinedContext = sb.ToString();

            var requestBody = new
            {
                model = _modelId,
                prompt = $"""
            You are an expert AI software engineer. Answer the user's question based strictly on the provided code snippets.
            The snippets include metadata like File, Class, and Method names to help you understand the structure.
            
            Context:
            {combinedContext}

            Question: {query}
            
            Answer:
            """,
                stream = false
            };

            var response = await _httpClient.PostAsync(
                new Uri(_ollamaUrl, "/api/generate"),
                new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                return new
                {
                    Context = combinedContext,
                    Response = "Error: Unable to generate response from LLM."
                };
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var serializationOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var completionResponse = JsonSerializer.Deserialize<OllamaCompletionResponse>(responseJson, serializationOptions);

            return new
            {
                Context = combinedContext,
                Response = completionResponse?.Response ?? "I don't know."
            };
        }

        private class OllamaCompletionResponse
        {
            [JsonPropertyName("response")]
            public string Response { get; set; } = string.Empty;
        }
    }
}
