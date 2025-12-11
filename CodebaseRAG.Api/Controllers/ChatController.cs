using System.Threading.Tasks;
using CodebaseRAG.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace CodebaseRAG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly RAGOrchestrator _ragOrchestrator;

        public ChatController(RAGOrchestrator ragOrchestrator)
        {
            _ragOrchestrator = ragOrchestrator;
        }

        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            if (string.IsNullOrEmpty(request.Message))
            {
                return BadRequest("Message is required");
            }

            var response = await _ragOrchestrator.AskQuestionAsync(request.Message);
            return Ok(new { Answer = response });
        }

        [HttpGet("test")]
        public async Task<IActionResult> TestChat()
        {
            // Simple test to check if RAG system is working
            var testMessage = "List all the files in the codebase";
            var response = await _ragOrchestrator.AskQuestionAsync(testMessage);
            return Ok(new { TestQuestion = testMessage, Answer = response });
        }
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
    }
}
