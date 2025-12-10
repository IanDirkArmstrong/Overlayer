# Overlayer - Multi-Image Overlay Tool

A portable Windows WinForms application that displays multiple PNG images as transparent, always-on-top overlay windows.

## Features

- **Multiple Overlays**: Load any number of images, each displayed in its own window
- **Transparent Windows**: Full alpha transparency support for PNG images
- **Always on Top**: Overlays stay visible over all windows, including fullscreen games
- **Drag to Move**: Click and drag any overlay to reposition it
- **Resize Handle**: Drag the corner handle to scale images (aspect ratio preserved)
- **Scroll to Scale**: Use mouse wheel to resize overlays (10% - 1000%)
- **Click-through Mode**: Lock overlays so mouse clicks pass through to windows below
- **Auto-Crop Transparency**: Automatically removes transparent edges from images
- **Padding**: Add configurable padding around images for positioning flexibility
- **Screen Edge Snapping**: Overlays snap to screen edges when dragged near them
- **Quick Snap Positions**: Instantly snap to corners or center via right-click menu
- **Persistent Configuration**: All settings saved automatically
- **System Tray**: Control everything from the system tray icon

## Building

### Prerequisites

- .NET 8 SDK (or newer)
- Windows 10/11

### Build Commands

#### Debug Build
```bash
cd src
dotnet build
```

#### Release Build (Single-File Portable EXE)
```bash
cd src
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The output will be in `src/bin/Release/net8.0-windows/win-x64/publish/Overlayer.exe`

### Alternative: Build with Compression
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

## Usage

### Starting the Application

1. Run `Overlayer.exe`
2. The application starts minimized to the system tray
3. Double-click the tray icon or use the context menu to load images

### Command Line

Load images from a directory on startup:
```bash
Overlayer.exe "C:\Path\To\Images"
```

### Keyboard Controls

| Key | Action |
|-----|--------|
| **L** | Lock overlay (click-through mode) |
| **U** | Unlock overlay (interactive mode) |
| **Esc** | Close the focused overlay |
| **+** | Increase scale |
| **-** | Decrease scale |
| **0** | Reset scale to 100% |

### Mouse Controls

| Action | Effect |
|--------|--------|
| **Left-click + Drag** | Move the overlay (snaps to edges) |
| **Drag resize handle** | Scale the overlay (bottom-right corner) |
| **Scroll Wheel** | Scale the overlay up/down |
| **Right-click** | Show context menu |

A small resize handle appears in the bottom-right corner of each unlocked overlay. Drag it to resize.

### Right-Click Context Menu

- **Lock/Unlock** - Toggle click-through mode
- **Scale** - Adjust scale with presets (25%, 50%, 75%, 100%, 150%, 200%, 300%)
- **Reset Scale** - Return to 100%
- **Crop Transparency** - Toggle automatic transparent edge removal
- **Padding** - Add transparent padding around the image (0-500px)
- **Snap to Edges** - Toggle automatic edge snapping while dragging
- **Snap Margin** - Distance from screen edge when snapped (0-50px)
- **Snap To...** - Instantly position to Top-Left, Top-Right, Bottom-Left, Bottom-Right, or Center
- **Close Overlay** - Remove this overlay

### System Tray Menu

- **Load Image...** - Open file dialog to add overlay(s)
- **Load from Directory...** - Load all images from a folder
- **Unlock All Overlays** - Make all overlays interactive
- **Lock All Overlays** - Make all overlays click-through
- **Save Configuration** - Manually save current state
- **Reload Configuration** - Reload from saved config
- **Close All Overlays** - Close all overlay windows
- **Exit** - Save and exit application

### Click-Through Mode

When an overlay is **locked** (press `L`):
- Mouse clicks pass through to windows below
- The overlay cannot be moved or resized
- The resize handle is hidden
- Use tray menu "Unlock All Overlays" or press `U` while hovering

When an overlay is **unlocked** (press `U`):
- You can drag to move
- You can drag the resize handle to scale
- You can scroll to resize
- Right-click for context menu

### Transparency Cropping & Padding

**Crop Transparency** (enabled by default):
- Automatically removes fully transparent edges from images
- Useful for images with extra canvas space around the actual content
- Toggle via right-click menu

**Padding**:
- Adds transparent padding around the (cropped) image
- Useful for creating a "bounding box" or safe zone
- Example: A 128x128 image with 50px padding becomes 228x228
- The image stays centered, with padding on all sides

### Screen Edge Snapping

**Snap to Edges** (enabled by default):
- When dragging near a screen edge, the overlay snaps to that edge
- Snap threshold is 20 pixels from the target position

**Snap Margin**:
- The distance from the screen edge when snapped
- Default is 10px (so snapping to bottom-right puts the overlay 10px from each edge)
- Set to 0 for flush-to-edge positioning

**Quick Snap Positions**:
- Right-click → "Snap To..." → Choose corner or center
- Instantly positions the overlay accounting for current snap margin

## Configuration

Configuration is automatically saved to `overlayer-config.json` next to the executable.

### Sample Configuration

```json
{
  "overlays": [
    {
      "imagePath": "C:\\Images\\crosshair.png",
      "x": 960,
      "y": 540,
      "scale": 1.0,
      "locked": true,
      "cropTransparency": true,
      "padding": 0,
      "snapToEdges": true,
      "snapMargin": 10
    },
    {
      "imagePath": "C:\\Images\\minimap.png",
      "x": 50,
      "y": 50,
      "scale": 0.75,
      "locked": false,
      "cropTransparency": true,
      "padding": 20,
      "snapToEdges": true,
      "snapMargin": 10
    }
  ]
}
```

### Configuration Fields

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `imagePath` | string | - | Full path to the image file |
| `x` | int | 100 | X position (pixels from left) |
| `y` | int | 100 | Y position (pixels from top) |
| `scale` | float | 1.0 | Scale factor (0.1 to 10.0) |
| `locked` | bool | false | Click-through mode enabled |
| `cropTransparency` | bool | true | Auto-crop transparent edges |
| `padding` | int | 0 | Transparent padding in pixels |
| `snapToEdges` | bool | true | Enable edge snapping while dragging |
| `snapMargin` | int | 10 | Distance from edge when snapped |

## Technical Details

### Window Behavior

- Uses `WS_EX_LAYERED` for per-pixel alpha transparency
- Uses `WS_EX_TRANSPARENT` for click-through when locked
- Uses `WS_EX_TOPMOST` for always-on-top behavior
- Uses `WS_EX_TOOLWINDOW` to hide from taskbar/alt-tab
- Uses `WS_EX_NOACTIVATE` to prevent stealing focus

### Image Processing

1. Load original image
2. If `cropTransparency` enabled: scan for non-transparent pixels and crop to bounding box
3. If `padding` > 0: add transparent border around the image
4. Scale result by `scale` factor
5. Render with per-pixel alpha via `UpdateLayeredWindow`

### Supported Image Formats

- PNG (recommended - full alpha transparency)
- JPG/JPEG
- BMP
- GIF

### System Requirements

- Windows 10 or Windows 11
- No additional runtime required (self-contained build)

## Troubleshooting

### Overlay doesn't appear on top of fullscreen games

Some games with exclusive fullscreen may block overlays. Try:
1. Using borderless windowed mode in the game
2. Running the game in windowed mode

### Images appear with wrong colors

Ensure your PNG files use standard RGBA format. Some image editors may export with unusual color profiles.

### Can't interact with locked overlay

Use the system tray menu → "Unlock All Overlays" to regain control.

### Image looks different than expected

Check "Crop Transparency" setting - if your image has intentional transparent areas at the edges, disable this option.

## License

MIT License - Feel free to use and modify as needed.
