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
- Ollama with codellama:7b
- Optional: PostgreSQL (if you prefer persistent storage instead of in-memory)

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

3. (Optional) Set up PostgreSQL database for persistent storage:
   - **Option 1: Using Docker (recommended for development)**
     - Install Docker if not already installed
     - Run the database:
       ```bash
       docker-compose up -d
       ```
     - The database will be available at `localhost:5432` with the connection string already configured in `appsettings.json`

   - **Option 2: Using a cloud service like Neon (no installation required)**
     - Sign up at [neon.tech](https://neon.tech)
     - Create a new project
     - Copy the connection string and update `appsettings.json`:
       ```json
       "ConnectionStrings": {
         "PostgreSQL": "your-neon-connection-string-here"
       }
       ```
     - Enable the pgvector extension in your Neon database

   - **Option 3: Install PostgreSQL locally**
     - Download and install from [postgresql.org](https://www.postgresql.org/download/)
     - Create a database named `codebaserag`
     - Install the pgvector extension
     - Update `appsettings.json` with your local connection details

   **Note:** If you skip this step, the application will use in-memory storage, which means indexed data will be lost when the application restarts.

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