using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CodebaseRAG.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System.Net;

namespace CodebaseRAG.Infrastructure.Services
{
    public class OllamaService : IEmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OllamaService> _logger;
        private readonly string _embeddingModel;
        private readonly string _chatModel;
        private readonly string _baseUrl;
        private readonly TimeSpan _requestTimeout;
        private readonly AsyncRetryPolicy _retryPolicy;
        private bool _isServiceHealthy = false;
        private DateTime _lastHealthCheck = DateTime.MinValue;

        public OllamaService(HttpClient httpClient, IConfiguration configuration, ILogger<OllamaService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            
            _baseUrl = _configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
            _embeddingModel = _configuration["Ollama:EmbeddingModel"] ?? "nomic-embed-text";
            _chatModel = _configuration["Ollama:ChatModel"] ?? "llama3";
            
            var timeoutMinutes = _configuration.GetValue<int>("Ollama:RequestTimeoutMinutes", 5);
            _requestTimeout = TimeSpan.FromMinutes(timeoutMinutes);
            
            // Configure HTTP client
            _httpClient.BaseAddress = new Uri(_baseUrl);
            _httpClient.Timeout = _requestTimeout;
            
            // Configure retry policy with exponential backoff
            var maxRetries = _configuration.GetValue<int>("Ollama:MaxRetries", 3);
            var retryDelaySeconds = _configuration.GetValue<int>("Ollama:RetryDelaySeconds", 2);
            
            _retryPolicy = Policy
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .Or<TimeoutException>()
                .WaitAndRetryAsync(
                    maxRetries,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(retryDelaySeconds, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning("Retry {RetryCount} for Ollama request. Waiting {Delay}ms",
                            retryCount, timespan.TotalMilliseconds);
                    });

            _logger.LogInformation("OllamaService initialized. BaseUrl: {BaseUrl}, EmbeddingModel: {EmbeddingModel}, ChatModel: {ChatModel}, Timeout: {Timeout}",
                _baseUrl, _embeddingModel, _chatModel, _requestTimeout);
        }

        public async Task<bool> IsServiceHealthyAsync()
        {
            try
            {
                // Cache health check result for 30 seconds to avoid excessive requests
                if (DateTime.UtcNow - _lastHealthCheck < TimeSpan.FromSeconds(30) && _isServiceHealthy)
                {
                    return _isServiceHealthy;
                }

                _lastHealthCheck = DateTime.UtcNow;
                
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await _httpClient.GetAsync("/api/tags", cts.Token);
                
                _isServiceHealthy = response.IsSuccessStatusCode;
                
                if (_isServiceHealthy)
                {
                    _logger.LogDebug("Ollama service health check passed");
                }
                else
                {
                    _logger.LogWarning("Ollama service health check failed. Status: {Status}", response.StatusCode);
                }
                
                return _isServiceHealthy;
            }
            catch (Exception ex)
            {
                _isServiceHealthy = false;
                _logger.LogWarning(ex, "Ollama service health check failed");
                return false;
            }
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            try
            {
                _logger.LogDebug("Generating embedding for text (length: {Length})", text.Length);
                
                // Check service health before making request
                if (!await IsServiceHealthyAsync())
                {
                    _logger.LogWarning("Ollama service is not healthy, skipping embedding generation");
                    return CreateFallbackEmbedding();
                }

                var request = new { model = _embeddingModel, input = text };
                
                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    try
                    {
                        var response = await _httpClient.PostAsJsonAsync("/api/embed", request);
                        return await ProcessEmbeddingResponse(response, "/api/embed");
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.InternalServerError)
                    {
                        _logger.LogError("Ollama service internal error. This may indicate the service is not properly configured or the model is not available. Error: {Error}", ex.Message);

                        // Return fallback embedding when Ollama service has internal issues
                        return CreateFallbackEmbedding();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating embedding for text: {Text}",
                    text.Length > 50 ? text.Substring(0, 50) + "..." : text);
                
                _isServiceHealthy = false; // Mark service as unhealthy on error
                return CreateFallbackEmbedding();
            }
        }

        private async Task<float[]> ProcessEmbeddingResponse(HttpResponseMessage response, string endpoint)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Ollama Embeddings API failed. Endpoint: {Endpoint}, Status: {Status}, Error: {Error}",
                    endpoint, response.StatusCode, errorContent);
                throw new HttpRequestException($"Ollama Embeddings API failed: {response.StatusCode} - {errorContent}");
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaEmbeddingResponse>();
            var embedding = result?.Embedding ?? Array.Empty<float>();
            
            if (embedding.Length == 0)
            {
                _logger.LogWarning("Received empty embedding from {Endpoint}", endpoint);
                return CreateFallbackEmbedding();
            }
            
            _logger.LogDebug("Generated embedding with {Dimension} dimensions from {Endpoint}", embedding.Length, endpoint);
            return embedding;
        }

        private float[] CreateFallbackEmbedding()
        {
            // Return zero vector as fallback - this maintains compatibility but indicates an issue
            var dimension = _configuration.GetValue<int>("Ollama:FallbackEmbeddingDimension", 384);
            _logger.LogWarning("Returning zero embedding as fallback (dimension: {Dimension})", dimension);
            return new float[dimension];
        }

        public async Task<float[][]> GenerateEmbeddingsAsync(IEnumerable<string> texts)
        {
            var embeddings = new List<float[]>();
            var textsList = texts.ToList();
            
            _logger.LogInformation("Generating embeddings for {Count} texts", textsList.Count);
            
            for (int i = 0; i < textsList.Count; i++)
            {
                var text = textsList[i];
                try
                {
                    var embedding = await GenerateEmbeddingAsync(text);
                    embeddings.Add(embedding);
                    
                    if ((i + 1) % 10 == 0)
                    {
                        _logger.LogDebug("Processed {Processed}/{Total} embeddings", i + 1, textsList.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate embedding for text {Index}: {Text}", i,
                        text.Length > 100 ? text.Substring(0, 100) + "..." : text);
                    embeddings.Add(CreateFallbackEmbedding());
                }
            }
            
            _logger.LogInformation("Completed embedding generation. Success rate: {SuccessRate}%",
                (embeddings.Count(e => e.Any(x => x != 0)) * 100.0 / embeddings.Count).ToString("F1"));
            
            return embeddings.ToArray();
        }

        public async Task<string> ChatAsync(string prompt, string systemPrompt)
        {
            try
            {
                _logger.LogDebug("Starting chat request with prompt length: {PromptLength}, system prompt length: {SystemPromptLength}",
                    prompt.Length, systemPrompt.Length);
                
                // Check service health before making request
                if (!await IsServiceHealthyAsync())
                {
                    _logger.LogWarning("Ollama service is not healthy, returning error response");
                    return "I'm sorry, but I'm currently unable to process your request. The AI service appears to be unavailable. Please try again later.";
                }

                var request = new
                {
                    model = _chatModel,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = prompt }
                    },
                    stream = false
                };

                return await _retryPolicy.ExecuteAsync(async () =>
                {
                    var response = await _httpClient.PostAsJsonAsync("/api/chat", request);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Ollama Chat failed. Status: {Status}, Error: {Error}", response.StatusCode, errorContent);
                        throw new HttpRequestException($"Ollama Chat failed: {response.StatusCode} - {errorContent}");
                    }

                    var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>();
                    var content = result?.Message?.Content ?? string.Empty;
                    
                    if (string.IsNullOrEmpty(content))
                    {
                        _logger.LogWarning("Received empty response from Ollama");
                        content = "I apologize, but I received an empty response. Please try rephrasing your question.";
                    }
                    
                    _logger.LogDebug("Chat request completed successfully. Response length: {ResponseLength}", content.Length);
                    return content;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during chat request");
                _isServiceHealthy = false; // Mark service as unhealthy on error
                
                return "I apologize, but I'm currently experiencing technical difficulties. Please try again later.";
            }
        }

        private class OllamaEmbeddingResponse
        {
            [JsonPropertyName("embedding")]
            public float[]? Embedding { get; set; }
        }

        private class OllamaChatResponse
        {
            [JsonPropertyName("message")]
            public OllamaMessage? Message { get; set; }
        }

        private class OllamaMessage
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }
        }
    }
}
