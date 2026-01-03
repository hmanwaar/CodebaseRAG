using System;
using CodebaseRAG.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CodebaseRAG.Infrastructure.Services
{
    public class CrawlerFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CrawlerFactory> _logger;
        private readonly ProjectTypeDetector _projectTypeDetector;

        public CrawlerFactory(
            IServiceProvider serviceProvider,
            ILogger<CrawlerFactory> logger,
            ProjectTypeDetector projectTypeDetector)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _projectTypeDetector = projectTypeDetector;
        }

        public ICodeCrawler CreateCrawler(string rootPath)
        {
            try
            {
                // Detect project type
                var projectType = _projectTypeDetector.DetectProjectType(rootPath);
                _logger.LogInformation("Detected project type: {ProjectType} for path: {Path}", 
                    _projectTypeDetector.GetProjectTypeDescription(projectType), rootPath);

                // Create appropriate crawler based on project type
                return projectType switch
                {
                    ProjectType.SQLDatabase => CreateSqlDatabaseCrawler(),
                    ProjectType.DotNetCore => CreateDotNetCrawler(),
                    ProjectType.DotNetFramework => CreateDotNetCrawler(),
                    ProjectType.WebForms => CreateWebFormsCrawler(),
                    ProjectType.Python => CreatePythonCrawler(),
                    ProjectType.NodeJS => CreateNodeJSCrawler(),
                    ProjectType.Angular => CreateAngularCrawler(),
                    ProjectType.React => CreateReactCrawler(),
                    ProjectType.Vue => CreateVueCrawler(),
                    ProjectType.Java => CreateJavaCrawler(),
                    ProjectType.Mixed => CreateMixedTechCrawler(),
                    _ => CreateGenericCrawler()
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating crawler for path: {Path}. Falling back to generic crawler", rootPath);
                return CreateGenericCrawler();
            }
        }

        private ICodeCrawler CreateSqlDatabaseCrawler()
        {
            _logger.LogInformation("Creating SQL Database Crawler");
            return _serviceProvider.GetRequiredService<SqlDatabaseCrawler>();
        }

        private ICodeCrawler CreateDotNetCrawler()
        {
            _logger.LogInformation("Creating .NET Crawler");
            return _serviceProvider.GetRequiredService<FileSystemCrawler>(); // Can be enhanced
        }

        private ICodeCrawler CreateWebFormsCrawler()
        {
            _logger.LogInformation("Creating WebForms Crawler");
            return _serviceProvider.GetRequiredService<FileSystemCrawler>(); // Can be enhanced
        }

        private ICodeCrawler CreatePythonCrawler()
        {
            _logger.LogInformation("Creating Python Crawler");
            return _serviceProvider.GetRequiredService<FileSystemCrawler>(); // Can be enhanced
        }

        private ICodeCrawler CreateNodeJSCrawler()
        {
            _logger.LogInformation("Creating Node.js Crawler");
            return _serviceProvider.GetRequiredService<FileSystemCrawler>(); // Can be enhanced
        }

        private ICodeCrawler CreateAngularCrawler()
        {
            _logger.LogInformation("Creating Angular Crawler");
            return _serviceProvider.GetRequiredService<FileSystemCrawler>(); // Can be enhanced
        }

        private ICodeCrawler CreateReactCrawler()
        {
            _logger.LogInformation("Creating React Crawler");
            return _serviceProvider.GetRequiredService<FileSystemCrawler>(); // Can be enhanced
        }

        private ICodeCrawler CreateVueCrawler()
        {
            _logger.LogInformation("Creating Vue Crawler");
            return _serviceProvider.GetRequiredService<FileSystemCrawler>(); // Can be enhanced
        }

        private ICodeCrawler CreateJavaCrawler()
        {
            _logger.LogInformation("Creating Java Crawler");
            return _serviceProvider.GetRequiredService<FileSystemCrawler>(); // Can be enhanced
        }

        private ICodeCrawler CreateMixedTechCrawler()
        {
            _logger.LogInformation("Creating Mixed Technology Crawler");
            return _serviceProvider.GetRequiredService<FileSystemCrawler>(); // Can be enhanced
        }

        private ICodeCrawler CreateGenericCrawler()
        {
            _logger.LogInformation("Creating Generic Crawler");
            return _serviceProvider.GetRequiredService<FileSystemCrawler>();
        }
    }
}