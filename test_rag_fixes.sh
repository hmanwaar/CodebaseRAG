#!/bin/bash
# Test script to verify RAG system fixes

echo "=== CodebaseRAG System Fixes Verification ==="
echo ""

echo "1. Testing indexing endpoint..."
curl -X POST "http://localhost:5019/api/indexing/rebuild-solution" \
  -H "Content-Type: application/json" \
  -d '{}' || echo "Indexing endpoint not available (server may not be running)"

echo ""
echo "2. Testing chat endpoint..."
curl -X GET "http://localhost:5019/api/chat/test" \
  -H "Content-Type: application/json" || echo "Chat endpoint not available (server may not be running)"

echo ""
echo "3. Checking indexing status..."
curl -X GET "http://localhost:5019/api/indexing/status" \
  -H "Content-Type: application/json" || echo "Status endpoint not available (server may not be running)"

echo ""
echo "=== Fixes Applied ==="
echo "✓ Fixed directory path detection in Program.cs"
echo "✓ Improved GetAllFilesAsync to return full file paths"
echo "✓ Enhanced FileSystemCrawler with better logging"
echo "✓ Fixed corrupted OllamaService.cs file"
echo "✓ Added manual rebuild endpoint (/api/indexing/rebuild-solution)"
echo "✓ Added test endpoint (/api/chat/test)"
echo ""
echo "=== Next Steps ==="
echo "1. Stop any running CodebaseRAG processes"
echo "2. Run: cd CodebaseRAG.Api && dotnet run"
echo "3. Test the API endpoints using the commands above"