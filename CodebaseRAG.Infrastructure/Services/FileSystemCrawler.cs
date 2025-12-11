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
        // Simple token estimation: 1 word ~= 1.3 tokens. 
        // We'll split by lines to keep code structure intact, aiming for ~500 tokens.
        private const int TargetChunkSize = 2000; // Characters approx
        private const int Overlap = 200; // Characters

        public FileSystemCrawler(ILogger<FileSystemCrawler> logger)
        {
            _logger = logger;
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
            var chunks = new List<CodeChunk>();
            try
            {
                var ext = Path.GetExtension(filePath).ToLower();
                var fileInfo = new FileInfo(filePath);

                // Skip very large files (> 1MB) to avoid hanging
                if (fileInfo.Length > 1024 * 1024)
                {
                    _logger.LogWarning("Skipping large file ({Size} bytes): {FilePath}", fileInfo.Length, filePath);
                    return chunks;
                }

                // Special handling for executable files
                if (ext == ".exe")
                {
                    var chunk = new CodeChunk
                    {
                        FilePath = filePath,
                        FileName = Path.GetFileName(filePath),
                        Content = $"[EXECUTABLE FILE]\n" +
                                 $"Name: {Path.GetFileName(filePath)}\n" +
                                 $"Path: {filePath}\n" +
                                 $"Size: {fileInfo.Length} bytes\n" +
                                 $"Last Modified: {fileInfo.LastWriteTime}\n" +
                                 $"This is an executable file (Windows .exe format).",
                        StartLine = 1,
                        EndLine = 1,
                        LastModified = fileInfo.LastWriteTime
                    };
                    chunks.Add(chunk);
                    _logger.LogDebug("Created metadata chunk for executable file: {FilePath}", filePath);
                    return chunks;
                }

                var content = await File.ReadAllTextAsync(filePath);

                if (string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogDebug("File is empty or whitespace only: {FilePath}", filePath);
                    return chunks;
                }

                var lines = content.Split('\n');
                _logger.LogDebug("Processing file with {LineCount} lines: {FilePath}", lines.Length, filePath);

                // Simple chunking strategy: Group lines until size limit
                var currentChunk = "";
                var startLine = 1;
                var currentLine = 1;

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
                            LastModified = File.GetLastWriteTime(filePath)
                        });

                        // Start new chunk with overlap (simplified: just reset for now, overlap logic is complex with lines)
                        // Ideally we'd keep the last N lines.
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
                        LastModified = File.GetLastWriteTime(filePath)
                    });
                }

                _logger.LogDebug("Created {ChunkCount} chunks for file: {FilePath}", chunks.Count, filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file: {FilePath}", filePath);
            }

            return chunks;
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
