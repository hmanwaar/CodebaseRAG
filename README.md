# CodebaseRAG

A Retrieval-Augmented Generation (RAG) system for codebase analysis and search.

## Overview

CodebaseRAG is a solution that combines code crawling, embedding, and vector search capabilities to enable intelligent codebase analysis and retrieval. It's designed to help developers quickly find relevant code snippets, understand codebase structure, and improve code discovery.

## Project Structure

```
CodebaseRAG/
├── CodebaseRAG.Api/          # API layer and controllers
├── CodebaseRAG.Core/         # Core interfaces and models
├── CodebaseRAG.Infrastructure/ # Implementation services
├── CodebaseRAG.Tests/        # Unit and integration tests
├── CodebaseRAG.UI/           # User interface components
├── .gitignore                # Git ignore rules
└── README.md                 # Project documentation
```

## Key Features

- **Code Crawling**: Traverse and analyze codebase structure
- **Embedding Service**: Convert code into vector representations
- **Vector Database**: Store and retrieve code embeddings efficiently
- **Intelligent Search**: Find relevant code based on semantic meaning
- **API Endpoints**: RESTful interface for integration

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Node.js (for frontend components)
- PostgreSQL (for vector database storage)
- Ollama with codellama:7b

### Installation

1. Clone the repository:
   ```bash
   git clone https://github.com/hmanwaar/CodebaseRAG.git
   cd CodebaseRAG
   ```

2. Restore NuGet packages:
   ```bash
   dotnet restore
   ```

3. Set up your database connection in `appsettings.json`

4. Build the solution:
   ```bash
   dotnet build
   ```

5. Run the API:
   ```bash
   cd CodebaseRAG.Api
   dotnet run
   ```

## Configuration

The main configuration files are:

- `CodebaseRAG.Api/appsettings.json` - Main application settings
- `CodebaseRAG.Api/appsettings.Development.json` - Development-specific settings

## API Endpoints

- `GET /api/health` - Health check endpoint
- `POST /api/index` - Index codebase content
- `GET /api/search` - Search for code snippets
- `POST /api/chat` - Interactive code analysis chat

## Development

### Running Tests

```bash
cd CodebaseRAG.Tests
dotnet test
```

### Code Style

- Follow standard C# coding conventions
- Use async/await for I/O operations
- Document public APIs with XML comments

## Contributing

Contributions are welcome! Please follow these steps:

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to your branch
5. Open a pull request

## License

This project is licensed under the MIT License.