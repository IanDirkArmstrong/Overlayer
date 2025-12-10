#!/bin/bash
# Cross-platform build script
# Works on Mac, Linux, and Windows (Git Bash/WSL)

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
SRC_DIR="$PROJECT_ROOT/src"

CONFIG="${1:-Release}"

echo "=== Building Overlayer ($CONFIG) ==="

cd "$SRC_DIR"

if [[ "$CONFIG" == "Release" ]]; then
    dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
    echo ""
    echo "Output: src/bin/Release/net8.0-windows/win-x64/publish/Overlayer.exe"
else
    dotnet build -c "$CONFIG"
    echo ""
    echo "Output: src/bin/$CONFIG/net8.0-windows/"
fi

echo "Done!"
