using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace CodebaseRAG.Infrastructure.Services
{
    public enum ProjectType
    {
        Unknown,
        DotNetCore,
        DotNetFramework,
        WebForms,
        Python,
        NodeJS,
        Angular,
        React,
        Vue,
        Java,
        SQLDatabase,
        Mixed
    }

    public class ProjectTypeDetector
    {
        private readonly ILogger<ProjectTypeDetector> _logger;

        public ProjectTypeDetector(ILogger<ProjectTypeDetector> logger)
        {
            _logger = logger;
        }

        public ProjectType DetectProjectType(string rootPath)
        {
            if (!Directory.Exists(rootPath))
            {
                _logger.LogWarning("Directory does not exist: {Path}", rootPath);
                return ProjectType.Unknown;
            }

            try
            {
                var detectionResults = new System.Collections.Generic.List<ProjectType>();
                
                // Check for .NET Core/5+ projects
                if (Directory.Exists(Path.Combine(rootPath, "Properties")) && 
                    File.Exists(Path.Combine(rootPath, "Program.cs")))
                {
                    detectionResults.Add(ProjectType.DotNetCore);
                }
                
                // Check for .NET Framework projects
                if (File.Exists(Path.Combine(rootPath, "packages.config")) ||
                    File.Exists(Path.Combine(rootPath, "App.config")))
                {
                    detectionResults.Add(ProjectType.DotNetFramework);
                }
                
                // Check for WebForms projects
                if (Directory.Exists(Path.Combine(rootPath, "App_Code")) ||
                    Directory.Exists(Path.Combine(rootPath, "App_Data")) ||
                    File.Exists(Path.Combine(rootPath, "Web.config")))
                {
                    detectionResults.Add(ProjectType.WebForms);
                }
                
                // Check for Python projects
                if (File.Exists(Path.Combine(rootPath, "requirements.txt")) ||
                    File.Exists(Path.Combine(rootPath, "setup.py")) ||
                    File.Exists(Path.Combine(rootPath, "Pipfile")))
                {
                    detectionResults.Add(ProjectType.Python);
                }
                
                // Check for Node.js projects
                if (File.Exists(Path.Combine(rootPath, "package.json")) &&
                    !File.Exists(Path.Combine(rootPath, "angular.json")) &&
                    !File.Exists(Path.Combine(rootPath, "vue.config.js")))
                {
                    detectionResults.Add(ProjectType.NodeJS);
                }
                
                // Check for Angular projects
                if (File.Exists(Path.Combine(rootPath, "angular.json")))
                {
                    detectionResults.Add(ProjectType.Angular);
                }
                
                // Check for React projects
                if (File.Exists(Path.Combine(rootPath, "package.json")))
                {
                    try
                    {
                        var packageJson = File.ReadAllText(Path.Combine(rootPath, "package.json"));
                        if (packageJson.Contains("react") || packageJson.Contains("react-dom"))
                        {
                            detectionResults.Add(ProjectType.React);
                        }
                    }
                    catch { }
                }
                
                // Check for Vue projects
                if (File.Exists(Path.Combine(rootPath, "vue.config.js")) ||
                    File.Exists(Path.Combine(rootPath, "nuxt.config.js")))
                {
                    detectionResults.Add(ProjectType.Vue);
                }
                
                // Check for Java projects
                if (File.Exists(Path.Combine(rootPath, "pom.xml")) ||
                    File.Exists(Path.Combine(rootPath, "build.gradle")))
                {
                    detectionResults.Add(ProjectType.Java);
                }
                
                // Check for SQL database projects
                if (Directory.GetFiles(rootPath, "*.sql", SearchOption.AllDirectories).Length > 5 ||
                    File.Exists(Path.Combine(rootPath, "database.sql")) ||
                    File.Exists(Path.Combine(rootPath, "schema.sql")))
                {
                    detectionResults.Add(ProjectType.SQLDatabase);
                }

                // Determine final project type
                if (detectionResults.Count == 0)
                {
                    return ProjectType.Unknown;
                }
                else if (detectionResults.Count == 1)
                {
                    return detectionResults[0];
                }
                else
                {
                    // For mixed projects, prioritize more specific types
                    if (detectionResults.Contains(ProjectType.WebForms)) return ProjectType.WebForms;
                    if (detectionResults.Contains(ProjectType.DotNetCore)) return ProjectType.DotNetCore;
                    if (detectionResults.Contains(ProjectType.Angular)) return ProjectType.Angular;
                    if (detectionResults.Contains(ProjectType.React)) return ProjectType.React;
                    
                    return ProjectType.Mixed;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting project type for path: {Path}", rootPath);
                return ProjectType.Unknown;
            }
        }

        public string GetProjectTypeDescription(ProjectType projectType)
        {
            return projectType switch
            {
                ProjectType.Unknown => "Unknown",
                ProjectType.DotNetCore => ".NET Core/.NET 5+",
                ProjectType.DotNetFramework => ".NET Framework",
                ProjectType.WebForms => "ASP.NET WebForms",
                ProjectType.Python => "Python",
                ProjectType.NodeJS => "Node.js",
                ProjectType.Angular => "Angular",
                ProjectType.React => "React",
                ProjectType.Vue => "Vue.js",
                ProjectType.Java => "Java",
                ProjectType.SQLDatabase => "SQL Database",
                ProjectType.Mixed => "Mixed Technology",
                _ => "Unknown"
            };
        }
    }
}