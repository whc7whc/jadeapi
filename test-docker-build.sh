#!/bin/bash

# Railway Docker Build Test Script
echo "?? Starting Railway Docker build test..."

# Check if Docker is available
if ! command -v docker &> /dev/null; then
    echo "? Docker is not installed or not in PATH"
    exit 1
fi

echo "? Docker is available"

# Build the Docker image
echo "?? Building Docker image..."
docker build -t team-api-test .

if [ $? -eq 0 ]; then
    echo "? Docker build successful!"
    
    # Test the image
    echo "?? Testing the image..."
    docker run --rm -d -p 8080:8080 --name team-api-test-container team-api-test
    
    # Wait a moment for the container to start
    sleep 5
    
    # Test health endpoint
    echo "?? Testing health endpoint..."
    if curl -f http://localhost:8080/health > /dev/null 2>&1; then
        echo "? Health check passed!"
    else
        echo "?? Health check failed or container not ready"
    fi
    
    # Clean up
    docker stop team-api-test-container
    docker rmi team-api-test
    
    echo "?? Build test completed successfully!"
else
    echo "? Docker build failed!"
    exit 1
fi