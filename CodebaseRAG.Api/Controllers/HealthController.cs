using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using CodebaseRAG.Infrastructure.Services;

namespace CodebaseRAG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly ILogger<HealthController> _logger;
        private readonly OllamaService _ollamaService;

        public HealthController(ILogger<HealthController> logger, OllamaService ollamaService)
        {
            _logger = logger;
            _ollamaService = ollamaService;
        }

        [HttpGet("ollama")]
        public async Task<IActionResult> CheckOllamaHealth()
        {
            try
            {
                _logger.LogInformation("Checking Ollama service health");
                
                var isHealthy = await _ollamaService.IsServiceHealthyAsync();
                
                if (isHealthy)
                {
                    return Ok(new 
                    { 
                        status = "healthy", 
                        service = "ollama",
                        timestamp = DateTime.UtcNow,
                        message = "Ollama service is running and responding"
                    });
                }
                else
                {
                    return StatusCode(503, new 
                    { 
                        status = "unhealthy", 
                        service = "ollama",
                        timestamp = DateTime.UtcNow,
                        message = "Ollama service is not responding"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking Ollama health");
                return StatusCode(503, new 
                { 
                    status = "error", 
                    service = "ollama",
                    timestamp = DateTime.UtcNow,
                    message = "Error checking Ollama service status",
                    error = ex.Message
                });
            }
        }

        [HttpGet]
        public IActionResult GeneralHealth()
        {
            return Ok(new 
            { 
                status = "healthy", 
                service = "codebase-rag-api",
                timestamp = DateTime.UtcNow,
                version = "1.0.0"
            });
        }
    }
}