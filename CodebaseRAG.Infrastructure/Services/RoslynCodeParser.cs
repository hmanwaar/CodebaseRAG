using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CodebaseRAG.Core.Interfaces;
using CodebaseRAG.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace CodebaseRAG.Infrastructure.Services
{
    public class RoslynCodeParser : ICodeParser
    {
        private readonly ILogger<RoslynCodeParser> _logger;

        public RoslynCodeParser(ILogger<RoslynCodeParser> logger)
        {
            _logger = logger;
        }

        public Task<IEnumerable<CodeChunk>> ParseAsync(string content, string filePath)
        {
            var chunks = new List<CodeChunk>();
            
            try 
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(content);
                var root = syntaxTree.GetRoot();
                
                // 1. Extract Classes
                var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                foreach (var cls in classes)
                {
                    // For now, we chunk methods separately, so we might want the class definition 
                    // *excluding* the methods if the class is huge, OR just the whole class if it's small.
                    // Strategy: 
                    // - Create a chunk for the Class signature + fields/properties (context).
                    // - Create chunks for each Method.
                    
                    var className = cls.Identifier.Text;
                    var startLine = cls.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    var endLine = cls.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
                    
                    // We can also just take the whole class text if we want coarse granularity.
                    // Let's rely on methods for fine-grained and maybe a "Class Summary" chunk?
                    // For RAG, specific methods are usually what we want. 
                    // Let's just do Methods for now and maybe the whole file fallback if no methods found.
                }

                // 2. Extract Methods
                var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>();
                
                if (!methods.Any())
                {
                    // Fallback to file-level chunk if no methods (e.g. POCOs, Interfaces, Scripts)
                    // Or use the naive line splitter which we might keep in FileSystemCrawler as fallback.
                    // For now, let's return empty and let caller handle fallback or implement simple splitting here?
                    // Better: return the whole thing as one chunk if small enough.
                    return Task.FromResult<IEnumerable<CodeChunk>>(new[] 
                    { 
                        new CodeChunk 
                        {
                            FilePath = filePath,
                            FileName = System.IO.Path.GetFileName(filePath),
                            Content = content,
                            StartLine = 1,
                            EndLine = content.Split('\n').Length,
                            Language = "csharp",
                            Tags = new List<string> { "file-level" }
                        }
                    });
                }

                foreach (var method in methods)
                {
                    var methodName = method.Identifier.Text;
                    var methodCode = method.ToFullString().Trim();
                    
                    // Find containing class
                    var parentClass = method.Parent as ClassDeclarationSyntax;
                    var className = parentClass?.Identifier.Text ?? "Global";

                    var startLine = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    var endLine = method.GetLocation().GetLineSpan().EndLinePosition.Line + 1;

                    chunks.Add(new CodeChunk
                    {
                        FilePath = filePath,
                        FileName = System.IO.Path.GetFileName(filePath),
                        Content = methodCode,
                        StartLine = startLine,
                        EndLine = endLine,
                        FunctionName = methodName,
                        ClassName = className,
                        Language = "csharp",
                        Tags = new List<string> { "method" }
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse C# file {Path}", filePath);
                // On error, return empty so fallback can handle it? Or throw?
                // Returning empty list implies "no chunks found".
            }

            return Task.FromResult(chunks.AsEnumerable());
        }
    }
}
