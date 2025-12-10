# Margin Visualization and Draggable Adjustment Design

## Overview

Add visual indicators for padding and snap margin that can be adjusted by dragging, plus make the config file hidden and add "apply to all" functionality.

## Changes

### 1. Hidden Config File

- Rename `overlayer-config.json` → `.overlayer-config.json`
- Dot prefix hides the file on Windows (with hidden attribute), macOS, and Linux
- On Windows, also set the file's hidden attribute via `File.SetAttributes()`

### 2. Default Values

| Setting | Old Default | New Default |
|---------|-------------|-------------|
| Padding | 0px | 10px |
| Snap Margin | 10px | 10px (unchanged) |

### 3. Margin Visualization

When the overlay is **unlocked** and the value is **> 0**, draw visual indicators:

#### Padding Border (Inner)
- Dashed line drawn *inside* the overlay
- Position: At the padding distance from the image content edge
- Color: Semi-transparent blue (e.g., `Color.FromArgb(100, 0, 120, 215)`)
- Style: Dashed, 1px

#### Snap Margin Border (Outer)
- Dashed line drawn *outside* the overlay bounds
- Position: At the snap margin distance from the overlay edge
- Color: Semi-transparent green (e.g., `Color.FromArgb(100, 0, 180, 0)`)
- Style: Dashed, 1px
- Note: This requires drawing beyond the window bounds or expanding the window temporarily

#### Implementation Approach for Outer Border
Since we can't draw outside the window, expand the window size to include the snap margin visualization area:
- When unlocked and snap margin > 0, the window is larger by `snapMargin` on all sides
- The extra area is transparent except for the dashed border
- When locked, window returns to normal size (just the image + padding)

### 4. Draggable Margin Adjustment

#### Interaction
- **Normal drag** on edge/corner: Resize (existing behavior)
- **Alt + drag** on edge: Adjust margin values

#### Hit Zone Detection (when Alt held)
- If cursor is within the padding zone (between image content and padding border): adjust padding
- If cursor is within the snap margin zone (between overlay edge and snap margin border): adjust snap margin

#### Drag Behavior
- Dragging outward increases the margin value
- Dragging inward decreases the margin value (minimum 0)
- Value updates in real-time as you drag
- On mouse up, fire `ConfigChanged` event

#### Cursor Feedback
- When Alt is held and hovering over a margin zone, show a distinct cursor (e.g., `SizeAll` or custom)
- Consider showing a tooltip with the current value while dragging

### 5. Apply to All Overlays

#### Context Menu Changes
Add "Apply to all overlays" option in both submenus:

```
Padding (10px) >
  ├── 0px
  ├── 10px ✓
  ├── 20px
  ├── 50px
  ├── 100px
  ├── ─────────
  ├── Custom...
  ├── ─────────
  └── Apply to all overlays

Snap Margin (10px) >
  ├── 0px
  ├── 5px
  ├── 10px ✓
  ├── 20px
  ├── 50px
  ├── ─────────
  └── Apply to all overlays
```

#### Implementation
- `OverlayWindow` raises a new event: `ApplyPaddingToAll` / `ApplySnapMarginToAll`
- `TrayController` handles the event and calls `SetPadding()` / `SetSnapMargin()` on all overlays

## Implementation Tasks

### ConfigManager.cs
1. Change `ConfigPath` to use `.overlayer-config.json`
2. In `Save()`, set hidden attribute on Windows: `File.SetAttributes(path, FileAttributes.Hidden)`
3. Handle migration: if old config exists and new doesn't, rename it

### OverlayConfig (in ConfigManager.cs)
4. Change `Padding` default from `0` to `10`

### OverlayWindow.cs
5. Add `_showMarginVisualization` computed property (unlocked AND has margins)
6. Add `DrawMarginVisualization()` method for dashed borders
7. Modify window sizing logic to include snap margin area when unlocked
8. Add Alt-key detection in `OnMouseMove()` and `OnMouseDown()`
9. Add margin drag logic with hit zone detection
10. Add `ApplyPaddingToAll` and `ApplySnapMarginToAll` events
11. Update context menu with "Apply to all overlays" items

### TrayController.cs
12. Subscribe to new `ApplyPaddingToAll` / `ApplySnapMarginToAll` events
13. Implement handlers to propagate values to all overlays

## Visual Mockup

```
┌─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─┐  ← Snap margin border (green dashed)

  ┌───────────────────────┐   ← Overlay window edge
  │  ┌─ ─ ─ ─ ─ ─ ─ ─ ─┐  │   ← Padding border (blue dashed)
  │                       │
  │    [Image Content]    │
  │                       │
  │  └─ ─ ─ ─ ─ ─ ─ ─ ─┘  │
  └───────────────────────┘

└─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─┘
```

## Edge Cases

- **Both margins at 0**: No visualization shown
- **Only padding > 0**: Only inner border shown
- **Only snap margin > 0**: Only outer border shown, window expanded
- **Locked state**: No margin visualization (clean display)
- **Alt+drag when margin is 0**: Still allow dragging to increase from 0
