using System.IO;
using System.Threading.Tasks;
using CodebaseRAG.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace CodebaseRAG.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IndexingController : ControllerBase
    {
        private readonly IndexingService _indexingService;

        public IndexingController(IndexingService indexingService)
        {
            _indexingService = indexingService;
        }

        [HttpPost("rebuild")]
        public IActionResult RebuildIndex([FromBody] IndexRequest request)
        {
            if (string.IsNullOrEmpty(request.RootPath))
            {
                return BadRequest("RootPath is required");
            }

            _indexingService.StartIndexing(request.RootPath, request.ExcludePatterns);
            return Accepted(new { Message = "Indexing started" });
        }

        [HttpPost("cancel")]
        public IActionResult CancelIndexing()
        {
            _indexingService.CancelIndexing();
            return Ok(new { Message = "Cancellation requested" }); 
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(_indexingService.Status);
        }

        [HttpGet("files")]
        public IActionResult GetIndexedFiles([FromQuery] string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath))
            {
                return BadRequest("RootPath is required");
            }

            // Sanitize path
            rootPath = rootPath.Trim('"').Trim('\'');

            if (!Directory.Exists(rootPath))
            {
                return NotFound($"Directory not found: {rootPath}");
            }

            try
            {
                var files = Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
                    .Where(f =>
                    {
                        var ext = Path.GetExtension(f).ToLower();
                        var binaryExtensions = new[] { ".dll", ".exe", ".pdb", ".bin", ".png", ".jpg", ".jpeg", ".gif", ".ico", ".zip", ".7z", ".tar", ".gz", ".pdf", ".doc", ".docx", ".xls", ".xlsx" };
                        return !binaryExtensions.Contains(ext);
                    })
                    .Where(f => !f.Contains("\\bin\\") && !f.Contains("\\obj\\") && !f.Contains("\\.git\\") && !f.Contains("\\node_modules\\"))
                    .Select(f => new {
                        FullPath = f,
                        FileName = Path.GetFileName(f),
                        Extension = Path.GetExtension(f),
                        Size = new System.IO.FileInfo(f).Length,
                        LastModified = System.IO.File.GetLastWriteTime(f)
                    })
                    .OrderBy(f => f.FullPath)
                    .ToList();

                return Ok(new {
                    Directory = rootPath,
                    FileCount = files.Count,
                    Files = files
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new {
                    Error = "Failed to list files",
                    Details = ex.Message
                });
            }
        }

        [HttpGet("browse")]
        public IActionResult BrowseDirectory([FromQuery] string? path)
        {
            try
            {
                // If path is empty, return logical drives
                if (string.IsNullOrEmpty(path))
                {
                   var drives = DriveInfo.GetDrives()
                       .Where(d => d.IsReady)
                       .Select(d => new { 
                           Name = d.Name, 
                           Path = d.Name,
                           Type = "Drive" 
                       })
                       .ToList();
                   return Ok(drives);
                }

                if (!Directory.Exists(path))
                {
                    return NotFound($"Directory not found: {path}");
                }

                var subDirs = Directory.GetDirectories(path)
                    .Select(d => new {
                        Name = Path.GetFileName(d),
                        Path = d,
                        Type = "Folder"
                    })
                    .OrderBy(d => d.Name)
                    .ToList();

                return Ok(subDirs);
            }
            catch (Exception ex)
            {
                 return StatusCode(500, new {
                    Error = "Failed to browse directory",
                    Details = ex.Message
                });
            }
        }

        [HttpPost("trigger")]
        public IActionResult TriggerIndexing([FromBody] IndexRequest request)
        {
            if (string.IsNullOrEmpty(request.RootPath))
            {
                // Use current directory if not specified
                request.RootPath = Directory.GetCurrentDirectory();
            }

            _indexingService.StartIndexing(request.RootPath, request.ExcludePatterns);
            return Accepted(new { Message = $"Indexing started for: {request.RootPath}" });
        }

        [HttpPost("rebuild-solution")]
        public IActionResult RebuildSolutionIndex()
        {
            // Find solution directory
            var currentDir = Directory.GetCurrentDirectory();
            var solutionDir = FindSolutionDirectory(currentDir);
            
            var excludePatterns = new[]
            {
                "\\bin\\",
                "\\obj\\",
                "\\.git\\",
                "\\node_modules\\",
                "\\.vs\\",
                "\\.vscode\\",
                "\\TestResults\\",
                "\\packages\\"
            };

            _indexingService.StartIndexing(solutionDir, excludePatterns);
            return Accepted(new { Message = $"Solution indexing started for: {solutionDir}" });
        }

        private string FindSolutionDirectory(string startPath)
        {
            var dir = new DirectoryInfo(startPath);
            while (dir != null)
            {
                if (dir.GetFiles("*.sln").Length > 0)
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            return startPath; // fallback to start path if no solution found
        }
    }

    public class IndexRequest
    {
        public string RootPath { get; set; } = string.Empty;
        public string[]? ExcludePatterns { get; set; }
    }
}
