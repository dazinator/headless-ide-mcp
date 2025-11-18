#!/bin/bash

# Publish script for the MCP Bridge
# Creates self-contained executables for all supported platforms

set -e

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR="$PROJECT_DIR/publish"

echo "Publishing MCP Bridge for all platforms..."
echo "Output directory: $OUTPUT_DIR"

# Clean previous builds
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Publish for each platform
PLATFORMS=(
    "win-x64"
    "win-arm64"
    "linux-x64"
    "linux-arm64"
    "osx-x64"
    "osx-arm64"
)

for PLATFORM in "${PLATFORMS[@]}"; do
    echo ""
    echo "Publishing for $PLATFORM..."
    
    dotnet publish "$PROJECT_DIR/HeadlessIdeMcp.Bridge.csproj" \
        -c Release \
        -r "$PLATFORM" \
        --self-contained \
        -p:PublishSingleFile=true \
        -p:PublishTrimmed=false \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -o "$OUTPUT_DIR/$PLATFORM"
    
    echo "✓ Published to $OUTPUT_DIR/$PLATFORM"
done

echo ""
echo "Creating archives..."

cd "$OUTPUT_DIR"

for PLATFORM in "${PLATFORMS[@]}"; do
    if [[ "$PLATFORM" == win-* ]]; then
        # Windows: create .zip
        echo "Creating $PLATFORM.zip..."
        cd "$PLATFORM"
        zip -q "../headless-ide-mcp-bridge-$PLATFORM.zip" headless-ide-mcp-bridge.exe
        cd ..
    else
        # Unix: create .tar.gz
        echo "Creating $PLATFORM.tar.gz..."
        cd "$PLATFORM"
        tar -czf "../headless-ide-mcp-bridge-$PLATFORM.tar.gz" headless-ide-mcp-bridge
        cd ..
    fi
done

echo ""
echo "✓ All platforms published successfully!"
echo ""
echo "Archives created in: $OUTPUT_DIR"
ls -lh "$OUTPUT_DIR"/*.{zip,tar.gz} 2>/dev/null || true
