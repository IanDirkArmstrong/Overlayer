#!/bin/bash
# Generate test images for development
# Requires ImageMagick (brew install imagemagick)

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
TEST_DIR="$PROJECT_ROOT/test"

mkdir -p "$TEST_DIR"

# Check for ImageMagick
if ! command -v magick &> /dev/null && ! command -v convert &> /dev/null; then
    echo "ImageMagick not found. Install with: brew install imagemagick"
    exit 1
fi

# Use 'magick' on newer ImageMagick, 'convert' on older versions
CONVERT="magick"
if ! command -v magick &> /dev/null; then
    CONVERT="convert"
fi

echo "Generating test images..."

# Test image 1: Blue rounded square with transparency
$CONVERT -size 200x200 xc:transparent \
    -fill 'rgba(65,105,225,0.7)' -stroke 'rgba(30,60,180,1)' -strokewidth 3 \
    -draw "roundrectangle 10,10 190,190 15,15" \
    -fill white -font Helvetica-Bold -pointsize 24 \
    -gravity center -annotate 0 "TEST" \
    "$TEST_DIR/test-square.png"
echo "Created: $TEST_DIR/test-square.png"

# Test image 2: Red crosshair
$CONVERT -size 64x64 xc:transparent \
    -stroke 'rgba(255,50,50,0.85)' -strokewidth 2 \
    -draw "line 32,8 32,24" \
    -draw "line 32,40 32,56" \
    -draw "line 8,32 24,32" \
    -draw "line 40,32 56,32" \
    -fill 'rgba(255,50,50,0.85)' -stroke none \
    -draw "circle 32,32 32,35" \
    "$TEST_DIR/test-crosshair.png"
echo "Created: $TEST_DIR/test-crosshair.png"

# Test image 3: Corner markers for positioning tests
$CONVERT -size 300x200 xc:transparent \
    -stroke 'rgba(0,200,100,0.8)' -strokewidth 2 \
    -draw "line 0,0 20,0" -draw "line 0,0 0,20" \
    -draw "line 299,0 279,0" -draw "line 299,0 299,20" \
    -draw "line 0,199 20,199" -draw "line 0,199 0,179" \
    -draw "line 299,199 279,199" -draw "line 299,199 299,179" \
    -draw "line 140,100 160,100" -draw "line 150,90 150,110" \
    "$TEST_DIR/test-corners.png"
echo "Created: $TEST_DIR/test-corners.png"

echo "Done! Test images are in: $TEST_DIR/"
