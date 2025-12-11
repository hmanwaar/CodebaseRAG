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
            // Retrieve multiple relevant texts
            List<string> contexts = await _textRepository.RetrieveRelevantText(query);

            // Combine multiple contexts into one string
            string combinedContext = string.Join("\n\n---\n\n", contexts);

            // If no relevant context is found, return a strict message
            if (contexts.Count == 1 && contexts[0] == "No relevant context found.")
            {
                return new
                {
                    Context = "No relevant data found in the database.",
                    Response = "I don't know."
                };
            }

            var requestBody = new
            {
                model = _modelId,
                prompt = $"""
            You are a strict AI assistant. You MUST answer ONLY using the provided context. 
            If the answer is not in the context, respond with "I don't know. No relevant data found."

            Context:
            {combinedContext}

            Question: {query}
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
                    Response = "Error: Unable to generate response."
                };
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var serializationOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var completionResponse = JsonSerializer.Deserialize<OllamaCompletionResponse>(responseJson, serializationOptions);

            return new
            {
                Context = combinedContext,
                Response = completionResponse?.Response ?? "I don't know. No relevant data found."
            };
        }

        private class OllamaCompletionResponse
        {
            [JsonPropertyName("response")]
            public string Response { get; set; } = string.Empty;
        }
    }
}
