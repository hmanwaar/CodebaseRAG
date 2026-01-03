using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodebaseRAG.Core.Interfaces;
using CodebaseRAG.Core.Models;
using Microsoft.Extensions.Logging;

namespace CodebaseRAG.Infrastructure.Services
{
    public class FileSystemCrawler : ICodeCrawler
    {
        private readonly ILogger<FileSystemCrawler> _logger;
        private readonly ICodeParser _codeParser;

        public FileSystemCrawler(ILogger<FileSystemCrawler> logger, ICodeParser codeParser)
        {
            _logger = logger;
            _codeParser = codeParser;
        }

        public Task<IEnumerable<string>> ScanDirectoryAsync(string rootPath, string[]? excludePatterns = null)
        {
            if (!Directory.Exists(rootPath))
            {
                _logger.LogWarning("Directory not found: {Path}", rootPath);
                return Task.FromResult(Enumerable.Empty<string>());
            }

            // Use EnumerateFiles for better memory usage on large directories
            try
            {
                _logger.LogInformation("Starting to scan directory: {Path}", rootPath);
                var allFiles = Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories).ToList();
                _logger.LogInformation("Scanner found {Count} raw files in {Path}", allFiles.Count, rootPath);

                var filteredFiles = allFiles.Where(f =>
                {
                    var isBin = IsBinary(f);
                    var isExcl = IsExcluded(f, excludePatterns);
                    if (isBin || isExcl)
                    {
                        _logger.LogDebug("Skipping file {File}. Binary: {IsBin}, Excluded: {IsExcl}", f, isBin, isExcl);
                        return false;
                    }
                    return true;
                }).ToList();

                _logger.LogInformation("After filtering: {Count} files will be processed", filteredFiles.Count);
                
                // Log some example files for debugging
                if (filteredFiles.Any())
                {
                    _logger.LogInformation("Sample files to be processed: {Files}",
                        string.Join(", ", filteredFiles.Take(5).Select(Path.GetFileName)));
                }

                return Task.FromResult(filteredFiles.AsEnumerable());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scanning directory {Path}", rootPath);
                throw;
            }
        }

        public async Task<IEnumerable<CodeChunk>> ProcessFileAsync(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);

                // Skip very large files (> configurable size) to avoid hanging
                var maxFileSizeMB = 1; // Default, can be made configurable
                if (fileInfo.Length > maxFileSizeMB * 1024 * 1024)
                {
                    _logger.LogWarning("Skipping large file ({Size} bytes, > {MaxSize}MB): {FilePath}",
                        fileInfo.Length, maxFileSizeMB, filePath);
                    return Enumerable.Empty<CodeChunk>();
                }

                // Use streaming for large files to reduce memory usage
                var content = await ReadFileWithStreamingAsync(filePath);
                
                if (string.IsNullOrWhiteSpace(content))
                {
                    return Enumerable.Empty<CodeChunk>();
                }
                
                // Generic file type handling
                var ext = Path.GetExtension(filePath).ToLower();
                
                // Use appropriate parser based on file type
                switch (ext)
                {
                    case ".cs":
                        return await _codeParser.ParseAsync(content, filePath);
                    
                    case ".cshtml":
                    case ".html":
                    case ".htm":
                        return ParseHtmlOrRazor(content, filePath, ext);
                    
                    case ".js":
                    case ".ts":
                    case ".jsx":
                    case ".tsx":
                        return ParseJavaScript(content, filePath, ext);
                    
                    case ".py":
                        return ParsePython(content, filePath);
                    
                    case ".json":
                    case ".xml":
                    case ".yaml":
                    case ".yml":
                    case ".md":
                    case ".txt":
                    case ".config":
                    case ".sql":
                        return SimpleLineChunking(content, filePath, ext);
                    
                    default:
                        // For unknown file types, try to detect if it's code-like or use simple chunking
                        if (IsLikelyCodeFile(content, ext))
                        {
                            return SimpleLineChunking(content, filePath, ext);
                        }
                        else
                        {
                            _logger.LogDebug("Skipping unknown file type: {FilePath}", filePath);
                            return Enumerable.Empty<CodeChunk>();
                        }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file: {FilePath}", filePath);
                return Enumerable.Empty<CodeChunk>();
            }
        }
        
        private IEnumerable<CodeChunk> ParseHtmlOrRazor(string content, string filePath, string extension)
        {
            var chunks = new List<CodeChunk>();
            var lines = content.Split('\n');
            var currentChunk = "";
            var startLine = 1;
            var currentLine = 1;
            int TargetChunkSize = 2000;
            
            // Determine language based on extension
            var language = extension == ".cshtml" ? "razor" : "html";
            
            foreach (var line in lines)
            {
                if (currentChunk.Length + line.Length > TargetChunkSize && !string.IsNullOrWhiteSpace(currentChunk))
                {
                    chunks.Add(new CodeChunk
                    {
                        FilePath = filePath,
                        FileName = Path.GetFileName(filePath),
                        Content = currentChunk.TrimEnd(),
                        StartLine = startLine,
                        EndLine = currentLine - 1,
                        LastModified = File.GetLastWriteTime(filePath),
                        Language = language
                    });
                    currentChunk = "";
                    startLine = currentLine;
                }
                currentChunk += line + "\n";
                currentLine++;
            }
            
            if (!string.IsNullOrWhiteSpace(currentChunk))
            {
                chunks.Add(new CodeChunk
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    Content = currentChunk.TrimEnd(),
                    StartLine = startLine,
                    EndLine = currentLine - 1,
                    LastModified = File.GetLastWriteTime(filePath),
                    Language = language
                });
            }
            return chunks;
        }
        
        private IEnumerable<CodeChunk> ParseJavaScript(string content, string filePath, string extension)
        {
            // For JavaScript/TypeScript files, we can use simple chunking for now
            // In future, could add proper AST parsing
            return SimpleLineChunking(content, filePath, extension);
        }
        
        private IEnumerable<CodeChunk> ParsePython(string content, string filePath)
        {
            // For Python files, use simple chunking for now
            // Could add proper Python AST parsing in future
            return SimpleLineChunking(content, filePath, ".py");
        }
        
        private IEnumerable<CodeChunk> SimpleLineChunking(string content, string filePath, string extension)
        {
            var chunks = new List<CodeChunk>();
            var lines = content.Split('\n');
            var currentChunk = "";
            var startLine = 1;
            var currentLine = 1;
            int TargetChunkSize = 2000;
            
            // Determine language based on extension
            var language = GetLanguageFromExtension(extension);
            
            foreach (var line in lines)
            {
                if (currentChunk.Length + line.Length > TargetChunkSize && !string.IsNullOrWhiteSpace(currentChunk))
                {
                    chunks.Add(new CodeChunk
                    {
                        FilePath = filePath,
                        FileName = Path.GetFileName(filePath),
                        Content = currentChunk.TrimEnd(),
                        StartLine = startLine,
                        EndLine = currentLine - 1,
                        LastModified = File.GetLastWriteTime(filePath),
                        Language = language
                    });
                    currentChunk = "";
                    startLine = currentLine;
                }
                currentChunk += line + "\n";
                currentLine++;
            }
            
            if (!string.IsNullOrWhiteSpace(currentChunk))
            {
                chunks.Add(new CodeChunk
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    Content = currentChunk.TrimEnd(),
                    StartLine = startLine,
                    EndLine = currentLine - 1,
                    LastModified = File.GetLastWriteTime(filePath),
                    Language = language
                });
            }
            return chunks;
        }
        
        private string GetLanguageFromExtension(string extension)
        {
            switch (extension.ToLower())
            {
                case ".cs": return "csharp";
                case ".cshtml": return "razor";
                case ".html":
                case ".htm": return "html";
                case ".js": return "javascript";
                case ".ts": return "typescript";
                case ".jsx": return "javascript";
                case ".tsx": return "typescript";
                case ".py": return "python";
                case ".json": return "json";
                case ".xml": return "xml";
                case ".yaml":
                case ".yml": return "yaml";
                case ".md": return "markdown";
                case ".sql": return "sql";
                default: return "text";
            }
        }
        
        private async Task<string> ReadFileWithStreamingAsync(string filePath)
        {
            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
                using var streamReader = new StreamReader(fileStream);
                
                // Read file in chunks to reduce memory usage
                var content = await streamReader.ReadToEndAsync();
                return content;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading file with streaming: {FilePath}", filePath);
                return string.Empty;
            }
        }
        
        private bool IsLikelyCodeFile(string content, string extension)
        {
            // Simple heuristic to determine if unknown file type is likely code
            if (string.IsNullOrWhiteSpace(content))
                return false;
            
            // Check for common code patterns
            var likelyCodePatterns = new[] { "function", "class", "import", "export", "def", "return", "if (", "for (", "while (" };
            
            // If it has common code patterns, treat as code
            if (likelyCodePatterns.Any(pattern => content.Contains(pattern)))
                return true;
            
            // If it looks like structured data, treat as text
            if (content.TrimStart().StartsWith("{") || content.TrimStart().StartsWith("[") ||
                content.TrimStart().StartsWith("<") || content.TrimStart().StartsWith("#"))
                return true;
            
            return false;
        }
        
        private bool IsBinary(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLower();
            // Allow .exe files to be processed for metadata indexing
            var binaryExtensions = new[] { ".dll", ".pdb", ".bin", ".png", ".jpg", ".jpeg", ".gif", ".ico", ".zip", ".7z", ".tar", ".gz", ".pdf", ".doc", ".docx", ".xls", ".xlsx" };
            return binaryExtensions.Contains(ext);
        }
        
        private bool IsExcluded(string filePath, string[]? excludePatterns)
        {
            if (excludePatterns == null) return false;
            
            // Simple contains check for now. Can be improved with Glob matching.
            foreach (var pattern in excludePatterns)
            {
                if (filePath.Contains(pattern, StringComparison.OrdinalIgnoreCase)) return true;
            }
            
            // Default excludes
            if (filePath.Contains("\\bin\\") || filePath.Contains("\\obj\\") || filePath.Contains("\\.git\\") || filePath.Contains("\\node_modules\\")) return true;
            
            return false;
        }
    }
}
