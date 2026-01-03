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
    public class SqlDatabaseCrawler : ICodeCrawler
    {
        private readonly ILogger<SqlDatabaseCrawler> _logger;
        private readonly ICodeParser _codeParser;
        private readonly DatabaseSchemaExtractor _schemaExtractor;

        public SqlDatabaseCrawler(ILogger<SqlDatabaseCrawler> logger, ICodeParser codeParser, DatabaseSchemaExtractor schemaExtractor)
        {
            _logger = logger;
            _codeParser = codeParser;
            _schemaExtractor = schemaExtractor;
        }

        public Task<IEnumerable<string>> ScanDirectoryAsync(string rootPath, string[] excludePatterns = null)
        {
            if (!Directory.Exists(rootPath))
            {
                _logger.LogWarning("Directory not found: {Path}", rootPath);
                return Task.FromResult(Enumerable.Empty<string>());
            }

            try
            {
                _logger.LogInformation("SQL Crawler: Starting to scan directory: {Path}", rootPath);
                
                // Look for SQL files specifically
                var sqlFiles = Directory.EnumerateFiles(rootPath, "*.sql", SearchOption.AllDirectories)
                    .Where(f => !IsExcluded(f, excludePatterns))
                    .ToList();
                
                // Also look for other database-related files
                var dbFiles = Directory.EnumerateFiles(rootPath, "*.*", SearchOption.AllDirectories)
                    .Where(f => IsDatabaseFile(f) && !IsExcluded(f, excludePatterns))
                    .ToList();
                
                var allFiles = sqlFiles.Concat(dbFiles).Distinct().ToList();
                
                _logger.LogInformation("SQL Crawler: Found {Count} database-related files", allFiles.Count);
                
                if (allFiles.Any())
                {
                    _logger.LogInformation("SQL Crawler: Sample files: {Files}",
                        string.Join(", ", allFiles.Take(5).Select(Path.GetFileName)));
                }
                
                return Task.FromResult(allFiles.AsEnumerable());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SQL Crawler: Error scanning directory {Path}", rootPath);
                throw;
            }
        }

        public async Task<IEnumerable<CodeChunk>> ProcessFileAsync(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                
                // Skip very large files (> 2MB for SQL files)
                if (fileInfo.Length > 2 * 1024 * 1024)
                {
                    _logger.LogWarning("SQL Crawler: Skipping large SQL file ({Size} bytes): {FilePath}", 
                        fileInfo.Length, filePath);
                    return Enumerable.Empty<CodeChunk>();
                }
                
                var content = await File.ReadAllTextAsync(filePath);
                
                if (string.IsNullOrWhiteSpace(content))
                {
                    return Enumerable.Empty<CodeChunk>();
                }
                
                var ext = Path.GetExtension(filePath).ToLower();
                
                // Parse SQL files with specialized SQL parsing
                if (ext == ".sql")
                {
                    return ParseSqlFile(content, filePath);
                }
                else
                {
                    // For other database files, use simple chunking
                    return SimpleLineChunking(content, filePath, ext);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SQL Crawler: Error processing file: {FilePath}", filePath);
                return Enumerable.Empty<CodeChunk>();
            }
        }
        
        private IEnumerable<CodeChunk> ParseSqlFile(string content, string filePath)
        {
            var chunks = new List<CodeChunk>();
            
            // Split SQL file into logical chunks based on statements
            var statements = SplitSqlStatements(content);
            
            foreach (var statement in statements)
            {
                if (string.IsNullOrWhiteSpace(statement)) continue;
                
                // Determine the type of SQL statement
                var statementType = DetermineSqlStatementType(statement);
                var startLine = GetLineNumber(content, statement);
                
                chunks.Add(new CodeChunk
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    Content = statement.Trim(),
                    StartLine = startLine,
                    EndLine = startLine, // Will be updated
                    LastModified = File.GetLastWriteTime(filePath),
                    Language = "sql",
                    Tags = new List<string> { statementType }
                });
            }
            
            // Update end lines
            UpdateEndLines(chunks, content);
            
            return chunks;
        }
        
        private List<string> SplitSqlStatements(string sqlContent)
        {
            var statements = new List<string>();
            var currentStatement = "";
            var inString = false;
            var inComment = false;
            var stringChar = '\0';
            
            for (int i = 0; i < sqlContent.Length; i++)
            {
                var c = sqlContent[i];
                
                // Handle string literals
                if (!inComment && (c == '\'' || c == '"'))
                {
                    if (!inString)
                    {
                        inString = true;
                        stringChar = c;
                    }
                    else if (c == stringChar)
                    {
                        inString = false;
                    }
                    currentStatement += c;
                    continue;
                }
                
                // Handle comments
                if (!inString && c == '-' && i + 1 < sqlContent.Length && sqlContent[i + 1] == '-')
                {
                    inComment = true;
                    currentStatement += "--";
                    i++; // skip next -
                    continue;
                }
                
                if (inComment && c == '\n')
                {
                    inComment = false;
                }
                
                if (inString || inComment)
                {
                    currentStatement += c;
                    continue;
                }
                
                // Check for statement terminators
                if (c == ';')
                {
                    currentStatement += c;
                    statements.Add(currentStatement.Trim());
                    currentStatement = "";
                }
                else
                {
                    currentStatement += c;
                }
            }
            
            // Add any remaining content
            if (!string.IsNullOrWhiteSpace(currentStatement))
            {
                statements.Add(currentStatement.Trim());
            }
            
            return statements;
        }
        
        private string DetermineSqlStatementType(string statement)
        {
            var normalized = statement.Trim().ToUpper();
            
            if (normalized.StartsWith("CREATE TABLE")) return "table-definition";
            if (normalized.StartsWith("CREATE PROCEDURE")) return "stored-procedure";
            if (normalized.StartsWith("CREATE FUNCTION")) return "function";
            if (normalized.StartsWith("CREATE VIEW")) return "view";
            if (normalized.StartsWith("CREATE INDEX")) return "index";
            if (normalized.StartsWith("ALTER TABLE")) return "table-modification";
            if (normalized.StartsWith("INSERT INTO")) return "data-insert";
            if (normalized.StartsWith("UPDATE")) return "data-update";
            if (normalized.StartsWith("DELETE FROM")) return "data-delete";
            if (normalized.StartsWith("SELECT")) return "query";
            if (normalized.StartsWith("DROP")) return "drop-statement";
            if (normalized.StartsWith("EXEC")) return "execution";
            
            return "sql-statement";
        }
        
        private int GetLineNumber(string content, string statement)
        {
            var lines = content.Split(new[] { '\n' }, StringSplitOptions.None);
            var contentSoFar = "";
            
            for (int i = 0; i < lines.Length; i++)
            {
                contentSoFar += lines[i] + "\n";
                if (contentSoFar.Contains(statement.Trim()))
                {
                    return i + 1;
                }
            }
            
            return 1;
        }
        
        private void UpdateEndLines(List<CodeChunk> chunks, string content)
        {
            var lines = content.Split(new[] { '\n' }, StringSplitOptions.None);
            
            for (int i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var chunkContent = chunk.Content;
                var contentSoFar = "";
                
                for (int lineNum = chunk.StartLine - 1; lineNum < lines.Length; lineNum++)
                {
                    contentSoFar += lines[lineNum] + "\n";
                    if (contentSoFar.Contains(chunkContent))
                    {
                        chunk.EndLine = lineNum + 1;
                        break;
                    }
                }
            }
        }
        
        private IEnumerable<CodeChunk> SimpleLineChunking(string content, string filePath, string extension)
        {
            var chunks = new List<CodeChunk>();
            var lines = content.Split(new[] { '\n' }, StringSplitOptions.None);
            var currentChunk = "";
            var startLine = 1;
            var currentLine = 1;
            int TargetChunkSize = 3000; // Larger chunks for database files
            
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
                case ".sql": return "sql";
                case ".db": return "database";
                case ".ddl": return "sql";
                case ".dml": return "sql";
                default: return "text";
            }
        }
        
        private bool IsDatabaseFile(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLower();
            var databaseExtensions = new[] { ".db", ".ddl", ".dml", ".sql", ".mdf", ".ldf" };
            return databaseExtensions.Contains(ext);
        }
        
        private bool IsExcluded(string filePath, string[] excludePatterns)
        {
            if (excludePatterns == null) return false;
            
            foreach (var pattern in excludePatterns)
            {
                if (filePath.Contains(pattern, StringComparison.OrdinalIgnoreCase)) return true;
            }
            
            // Default excludes
            if (filePath.Contains("\\bin\\") || filePath.Contains("\\obj\\") || 
                filePath.Contains("\\.git\\") || filePath.Contains("\\node_modules\\")) return true;
            
            return false;
        }
    }

    public class DatabaseSchemaExtractor
    {
        private readonly ILogger<DatabaseSchemaExtractor> _logger;

        public DatabaseSchemaExtractor(ILogger<DatabaseSchemaExtractor> logger)
        {
            _logger = logger;
        }

        public async Task<IEnumerable<CodeChunk>> ExtractSchemaFromDatabaseAsync(string connectionString)
        {
            // This would be implemented to connect to actual databases
            // For now, return empty as this is a placeholder
            _logger.LogInformation("Database schema extraction would be implemented here for connection: {Connection}", 
                connectionString);
            return Enumerable.Empty<CodeChunk>();
        }

        public async Task<IEnumerable<CodeChunk>> ExtractStoredProceduresAsync(string connectionString)
        {
            // Placeholder for stored procedure extraction
            _logger.LogInformation("Stored procedure extraction would be implemented here");
            return Enumerable.Empty<CodeChunk>();
        }
    }
}