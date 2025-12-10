# Edge/Corner Resize Design

## Overview

Replace the single bottom-right resize handle with a full border-based resize system that allows scaling from any edge or corner with 1:1 cursor tracking.

## Visual Design

### Border Appearance (Unlocked Only)
- Solid 1px contrasting line (white inner, dark outer outline)
- Idle state: ~15% opacity
- Hover state: ~50% opacity when cursor is within hit zone

### Hit Zones
- **Edge zones**: 10px wide strips along each edge
- **Corner zones**: 16x16px squares at each corner (larger target area)
- Corners take priority when cursor is in overlapping zone

### Cursor Feedback
| Zone | Cursor |
|------|--------|
| Top-left / Bottom-right corners | `SizeNWSE` (↘) |
| Top-right / Bottom-left corners | `SizeNESW` (↙) |
| Top / Bottom edges | `SizeNS` (↕) |
| Left / Right edges | `SizeWE` (↔) |
| Interior | `SizeAll` (move) |

## Scaling Behavior

### Anchor Point
- Opposite corner/edge stays fixed in screen position
- Example: Dragging top-left corner → bottom-right corner anchored

### 1:1 Cursor Tracking
- The dragged edge/corner follows cursor exactly
- Scale calculated: `newScale = originalScale * (newDistance / originalDistance)`

### Aspect Ratio
- Always preserved (proportional scaling)
- Edge drags scale both dimensions to maintain ratio

### Bounds
- Minimum: 0.1 (10%)
- Maximum: 10.0 (1000%)

## Implementation Changes

### Remove
- `HandleSize`, `HandleMargin` constants
- `DrawResizeHandle()` method
- `GetResizeHandleRect()` method
- `IsInResizeHandle()` method

### Add
```csharp
enum ResizeEdge
{
    None,
    Top, Right, Bottom, Left,
    TopLeft, TopRight, BottomLeft, BottomRight
}
```

#### New Fields
- `_hoveredEdge` - tracks edge/corner under cursor
- `_activeEdge` - tracks edge/corner being dragged
- `_anchorPoint` - fixed screen point during resize
- `_originalSize` - window size at resize start

#### New Methods
- `DrawBorder()` - draws contrasting border with opacity based on hover state
- `GetEdgeAtPoint(Point)` - returns `ResizeEdge` for hit testing

### Modified Methods
- `OnMouseMove()` - edge detection, hover state, proportional scaling with anchor
- `OnMouseDown()` - capture anchor point and original size
- `OnMouseUp()` - clear active edge
- `UpdateScaledImage()` - call `DrawBorder()` instead of `DrawResizeHandle()`

### Rendering
- Border drawn onto `_currentImage` bitmap
- Two-pass drawing: dark outer (2px offset), white inner (1px)
- Opacity controlled by `_hoveredEdge != ResizeEdge.None`
