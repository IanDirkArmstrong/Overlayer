#!/bin/bash
# Mac development script - build and run with Wine + XQuartz
#
# Prerequisites (one-time setup):
#   brew install --cask xquartz
#   brew install wine-stable
#   brew install imagemagick  # For generating test images
#   # Log out and back in after installing XQuartz
#
# Usage:
#   ./scripts/dev-mac.sh                      # Run with default test images
#   ./scripts/dev-mac.sh /path/to/image.png   # Load specific image(s)
#   ./scripts/dev-mac.sh /path/to/images/     # Load all images from directory
#
# Note: Wine won't perfectly emulate Windows layered windows.
# Transparency and click-through may not work correctly.
# This is for quick iteration testing, not pixel-perfect verification.
#
# IMPORTANT: File dialogs don't work in Wine. Pass images via command line.

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
SRC_DIR="$PROJECT_ROOT/src"
TEST_DIR="$PROJECT_ROOT/test"
PUBLISH_DIR="$SRC_DIR/bin/Release/net8.0-windows/win-x64/publish"
EXE_PATH="$PUBLISH_DIR/Overlayer.exe"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}=== Overlayer Mac Dev Build ===${NC}"

# Check for Wine
if ! command -v wine &> /dev/null; then
    echo -e "${RED}Error: Wine is not installed.${NC}"
    echo "Install with: brew install wine-stable"
    exit 1
fi

# Check for XQuartz (look for X11 display)
if [[ -z "$DISPLAY" ]]; then
    export DISPLAY=:0
fi

# Build
echo -e "${YELLOW}Building...${NC}"
cd "$SRC_DIR"
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -verbosity:minimal

if [[ ! -f "$EXE_PATH" ]]; then
    echo -e "${RED}Build failed - executable not found${NC}"
    exit 1
fi

echo -e "${GREEN}Build complete!${NC}"
echo ""

# Determine what images to load
IMAGES_TO_LOAD=("$@")

# If no arguments provided, use test images
if [[ ${#IMAGES_TO_LOAD[@]} -eq 0 ]]; then
    # Generate test images if they don't exist
    if [[ ! -f "$TEST_DIR/test-square.png" ]]; then
        echo -e "${YELLOW}Generating test images...${NC}"
        "$SCRIPT_DIR/generate-test-images.sh"
        echo ""
    fi
    IMAGES_TO_LOAD=("$TEST_DIR/test-square.png" "$TEST_DIR/test-crosshair.png")
fi

# Clear any existing config for fresh test runs
CONFIG_PATH="$PUBLISH_DIR/overlayer-config.json"
if [[ -f "$CONFIG_PATH" ]]; then
    rm "$CONFIG_PATH"
fi

# Run with Wine
echo -e "${YELLOW}Starting with Wine...${NC}"
echo -e "${YELLOW}(Transparency/click-through may not work correctly)${NC}"
echo ""

cd "$PUBLISH_DIR"
wine Overlayer.exe "${IMAGES_TO_LOAD[@]}"
