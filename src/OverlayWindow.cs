using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using static Overlayer.NativeMethods;

namespace Overlayer;

internal static class DebugLog
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "debug.log");

    public static void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        Console.WriteLine(line);
        try
        {
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch { }
    }
}

/// <summary>
/// A borderless, transparent overlay window that displays a single image.
/// Supports dragging, scaling, transparency cropping, padding, and screen edge snapping.
/// </summary>
public class OverlayWindow : Form
{
    private readonly string _imagePath;
    private Bitmap? _originalImage;
    private Bitmap? _croppedImage;
    private Bitmap? _currentImage;
    private float _scale;
    private bool _isLocked;
    private bool _isDragging;
    private Point _dragStart;

    // Edge/corner resize state
    private ResizeEdge _hoveredEdge;
    private ResizeEdge _activeEdge;
    private Point _anchorPoint;
    private Size _originalSize;
    private float _scaleAtResizeStart;

    // New settings
    private bool _cropTransparency;
    private int _padding;
    private bool _snapToEdges;
    private int _snapMargin;

    private const int EdgeHitZone = 10;    // Pixels from edge to detect resize
    private const int CornerHitZone = 16;  // Larger hit zone for corners
    private const int SnapThreshold = 20;  // Pixels within which snapping activates

    /// <summary>
    /// Gets the configuration for this overlay.
    /// </summary>
    public OverlayConfig Config => new()
    {
        ImagePath = _imagePath,
        X = Location.X,
        Y = Location.Y,
        Scale = _scale,
        Locked = _isLocked,
        CropTransparency = _cropTransparency,
        Padding = _padding,
        SnapToEdges = _snapToEdges,
        SnapMargin = _snapMargin
    };

    /// <summary>
    /// Event raised when the overlay is closed.
    /// </summary>
    public event EventHandler? OverlayClosed;

    /// <summary>
    /// Event raised when configuration changes (position, scale, lock state).
    /// </summary>
    public event EventHandler? ConfigChanged;

    public OverlayWindow(OverlayConfig config)
        : this(config.ImagePath, config.X, config.Y, config.Scale, config.Locked,
               config.CropTransparency, config.Padding, config.SnapToEdges, config.SnapMargin)
    {
    }

    public OverlayWindow(string imagePath, int x = 100, int y = 100, float scale = 1.0f, bool locked = false,
                         bool cropTransparency = true, int padding = 0, bool snapToEdges = true, int snapMargin = 10)
    {
        _imagePath = imagePath;
        _scale = Math.Max(0.1f, Math.Min(scale, 10.0f));
        _isLocked = locked;
        _cropTransparency = cropTransparency;
        _padding = Math.Max(0, padding);
        _snapToEdges = snapToEdges;
        _snapMargin = Math.Max(0, snapMargin);

        InitializeWindow();
        LoadImage();
        Location = new Point(x, y);

        if (_isLocked)
        {
            ApplyClickThrough();
        }
    }

    private void InitializeWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;

        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);

        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        MouseLeave += OnMouseLeave;
        MouseWheel += OnMouseWheel;
        KeyDown += OnKeyDown;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    private void LoadImage()
    {
        DebugLog.Log($"[LoadImage] Starting to load: {_imagePath}");
        try
        {
            if (!File.Exists(_imagePath))
            {
                throw new FileNotFoundException($"Image not found: {_imagePath}");
            }
            DebugLog.Log($"[LoadImage] File exists, loading bitmap...");

            using var temp = new Bitmap(_imagePath);
            DebugLog.Log($"[LoadImage] Temp bitmap created: {temp.Width}x{temp.Height}");
            _originalImage = new Bitmap(temp);
            DebugLog.Log($"[LoadImage] Original image copied: {_originalImage.Width}x{_originalImage.Height}");

            ProcessImage();
            UpdateScaledImage();
            DebugLog.Log($"[LoadImage] Complete!");
        }
        catch (Exception ex)
        {
            DebugLog.Log($"[LoadImage] ERROR: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Failed to load image: {ex.Message}");
            MessageBox.Show($"Failed to load image: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }

    /// <summary>
    /// Processes the image: crops transparency and applies padding.
    /// </summary>
    private void ProcessImage()
    {
        if (_originalImage == null) return;

        _croppedImage?.Dispose();

        var sourceImage = _originalImage;

        // Crop transparent edges if enabled
        if (_cropTransparency)
        {
            var cropped = CropTransparentEdges(sourceImage);
            if (cropped != null)
            {
                sourceImage = cropped;
            }
        }

        // Apply padding if specified
        if (_padding > 0)
        {
            var padded = ApplyPadding(sourceImage, _padding);
            if (sourceImage != _originalImage)
            {
                sourceImage.Dispose();
            }
            _croppedImage = padded;
        }
        else
        {
            if (sourceImage != _originalImage)
            {
                _croppedImage = sourceImage;
            }
            else
            {
                _croppedImage = new Bitmap(sourceImage);
            }
        }
    }

    /// <summary>
    /// Crops fully transparent edges from an image.
    /// </summary>
    private static Bitmap? CropTransparentEdges(Bitmap source)
    {
        var rect = GetNonTransparentBounds(source);
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return null;
        }

        if (rect.X == 0 && rect.Y == 0 &&
            rect.Width == source.Width && rect.Height == source.Height)
        {
            return null; // No cropping needed
        }

        var cropped = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(cropped);
        g.DrawImage(source, new Rectangle(0, 0, rect.Width, rect.Height), rect, GraphicsUnit.Pixel);
        return cropped;
    }

    /// <summary>
    /// Finds the bounding rectangle of non-transparent pixels.
    /// </summary>
    private static Rectangle GetNonTransparentBounds(Bitmap bitmap)
    {
        int minX = bitmap.Width, minY = bitmap.Height;
        int maxX = 0, maxY = 0;

        var data = bitmap.LockBits(
            new Rectangle(0, 0, bitmap.Width, bitmap.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        unsafe
        {
            byte* ptr = (byte*)data.Scan0;
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    int offset = y * data.Stride + x * 4;
                    byte alpha = ptr[offset + 3];

                    if (alpha > 0)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
        }

        bitmap.UnlockBits(data);

        if (maxX < minX || maxY < minY)
        {
            return Rectangle.Empty;
        }

        return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    /// <summary>
    /// Applies transparent padding around an image.
    /// </summary>
    private static Bitmap ApplyPadding(Bitmap source, int padding)
    {
        var newWidth = source.Width + padding * 2;
        var newHeight = source.Height + padding * 2;

        var padded = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(padded);
        g.Clear(Color.Transparent);
        g.DrawImage(source, padding, padding);
        return padded;
    }

    private void UpdateScaledImage()
    {
        DebugLog.Log($"[UpdateScaledImage] Starting...");
        var sourceImage = _croppedImage ?? _originalImage;
        if (sourceImage == null)
        {
            DebugLog.Log($"[UpdateScaledImage] ERROR: sourceImage is null!");
            return;
        }
        DebugLog.Log($"[UpdateScaledImage] Source image: {sourceImage.Width}x{sourceImage.Height}, scale: {_scale}");

        _currentImage?.Dispose();

        var newWidth = Math.Max(1, (int)(sourceImage.Width * _scale));
        var newHeight = Math.Max(1, (int)(sourceImage.Height * _scale));
        DebugLog.Log($"[UpdateScaledImage] New dimensions: {newWidth}x{newHeight}");

        _currentImage = new Bitmap(newWidth, newHeight, PixelFormat.Format32bppArgb);

        using (var g = Graphics.FromImage(_currentImage))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.DrawImage(sourceImage, 0, 0, newWidth, newHeight);

            if (!_isLocked)
            {
                DrawBorder(g, newWidth, newHeight);
            }
        }

        Size = new Size(newWidth, newHeight);
        DebugLog.Log($"[UpdateScaledImage] Window size set to: {Size.Width}x{Size.Height}");
        UpdateLayeredWindowImage();
    }

    private void DrawBorder(Graphics g, int width, int height)
    {
        // Determine opacity based on hover state
        int opacity = _hoveredEdge != ResizeEdge.None ? 128 : 38; // ~50% or ~15%

        // Draw contrasting border: dark outer, white inner
        using var outerPen = new Pen(Color.FromArgb(opacity, 0, 0, 0), 3);
        using var innerPen = new Pen(Color.FromArgb(opacity, 255, 255, 255), 1);

        // Draw outer (dark) border - slightly inset to stay within bounds
        g.DrawRectangle(outerPen, 1, 1, width - 3, height - 3);
        // Draw inner (white) border
        g.DrawRectangle(innerPen, 1, 1, width - 3, height - 3);
    }

    private void UpdateLayeredWindowImage()
    {
        DebugLog.Log($"[UpdateLayeredWindowImage] Starting...");
        if (_currentImage == null)
        {
            DebugLog.Log($"[UpdateLayeredWindowImage] ERROR: _currentImage is null!");
            return;
        }
        if (!IsHandleCreated)
        {
            DebugLog.Log($"[UpdateLayeredWindowImage] ERROR: Handle not created!");
            return;
        }
        DebugLog.Log($"[UpdateLayeredWindowImage] Image: {_currentImage.Width}x{_currentImage.Height}, Handle: {Handle}");

        var screenDc = GetDC(IntPtr.Zero);
        DebugLog.Log($"[UpdateLayeredWindowImage] screenDc: {screenDc}");
        var memDc = CreateCompatibleDC(screenDc);
        DebugLog.Log($"[UpdateLayeredWindowImage] memDc: {memDc}");
        var hBitmap = _currentImage.GetHbitmap(Color.FromArgb(0));
        DebugLog.Log($"[UpdateLayeredWindowImage] hBitmap: {hBitmap}");
        var oldBitmap = SelectObject(memDc, hBitmap);

        var size = new SIZE(_currentImage.Width, _currentImage.Height);
        var pointSource = new POINT(0, 0);
        var topPos = new POINT(Left, Top);
        DebugLog.Log($"[UpdateLayeredWindowImage] Position: ({Left}, {Top})");
        var blend = new BLENDFUNCTION
        {
            BlendOp = AC_SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = 255,
            AlphaFormat = AC_SRC_ALPHA
        };

        var result = UpdateLayeredWindow(Handle, screenDc, ref topPos, ref size, memDc, ref pointSource, 0, ref blend, ULW_ALPHA);
        DebugLog.Log($"[UpdateLayeredWindowImage] UpdateLayeredWindow result: {result}");

        SelectObject(memDc, oldBitmap);
        DeleteObject(hBitmap);
        DeleteDC(memDc);
        ReleaseDC(IntPtr.Zero, screenDc);
        DebugLog.Log($"[UpdateLayeredWindowImage] Complete!");
    }

    protected override void OnMove(EventArgs e)
    {
        base.OnMove(e);
        UpdateLayeredWindowImage();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        DebugLog.Log($"[OnShown] Window shown, calling UpdateLayeredWindowImage...");
        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        UpdateLayeredWindowImage();
    }

    private ResizeEdge GetEdgeAtPoint(Point p)
    {
        bool nearLeft = p.X < CornerHitZone;
        bool nearRight = p.X > Width - CornerHitZone;
        bool nearTop = p.Y < CornerHitZone;
        bool nearBottom = p.Y > Height - CornerHitZone;

        // Check corners first (they have priority)
        if (nearTop && nearLeft) return ResizeEdge.TopLeft;
        if (nearTop && nearRight) return ResizeEdge.TopRight;
        if (nearBottom && nearLeft) return ResizeEdge.BottomLeft;
        if (nearBottom && nearRight) return ResizeEdge.BottomRight;

        // Check edges
        if (p.X < EdgeHitZone) return ResizeEdge.Left;
        if (p.X > Width - EdgeHitZone) return ResizeEdge.Right;
        if (p.Y < EdgeHitZone) return ResizeEdge.Top;
        if (p.Y > Height - EdgeHitZone) return ResizeEdge.Bottom;

        return ResizeEdge.None;
    }

    private Cursor GetCursorForEdge(ResizeEdge edge)
    {
        return edge switch
        {
            ResizeEdge.TopLeft or ResizeEdge.BottomRight => Cursors.SizeNWSE,
            ResizeEdge.TopRight or ResizeEdge.BottomLeft => Cursors.SizeNESW,
            ResizeEdge.Top or ResizeEdge.Bottom => Cursors.SizeNS,
            ResizeEdge.Left or ResizeEdge.Right => Cursors.SizeWE,
            _ => Cursors.SizeAll
        };
    }

    private Point GetAnchorPoint(ResizeEdge edge)
    {
        // Return the screen position of the opposite corner/edge
        return edge switch
        {
            ResizeEdge.TopLeft => PointToScreen(new Point(Width, Height)),
            ResizeEdge.TopRight => PointToScreen(new Point(0, Height)),
            ResizeEdge.BottomLeft => PointToScreen(new Point(Width, 0)),
            ResizeEdge.BottomRight => PointToScreen(new Point(0, 0)),
            ResizeEdge.Top => PointToScreen(new Point(Width / 2, Height)),
            ResizeEdge.Bottom => PointToScreen(new Point(Width / 2, 0)),
            ResizeEdge.Left => PointToScreen(new Point(Width, Height / 2)),
            ResizeEdge.Right => PointToScreen(new Point(0, Height / 2)),
            _ => PointToScreen(new Point(Width / 2, Height / 2))
        };
    }

    /// <summary>
    /// Applies edge snapping to a position.
    /// </summary>
    private Point ApplyEdgeSnapping(Point position)
    {
        if (!_snapToEdges) return position;

        var screen = Screen.FromPoint(position);
        var workArea = screen.WorkingArea;

        int x = position.X;
        int y = position.Y;

        // Snap left edge
        if (Math.Abs(x - workArea.Left - _snapMargin) < SnapThreshold)
        {
            x = workArea.Left + _snapMargin;
        }
        // Snap right edge
        else if (Math.Abs(x + Width - (workArea.Right - _snapMargin)) < SnapThreshold)
        {
            x = workArea.Right - _snapMargin - Width;
        }

        // Snap top edge
        if (Math.Abs(y - workArea.Top - _snapMargin) < SnapThreshold)
        {
            y = workArea.Top + _snapMargin;
        }
        // Snap bottom edge
        else if (Math.Abs(y + Height - (workArea.Bottom - _snapMargin)) < SnapThreshold)
        {
            y = workArea.Bottom - _snapMargin - Height;
        }

        return new Point(x, y);
    }

    /// <summary>
    /// Snaps the window to a specific screen edge.
    /// </summary>
    public void SnapTo(SnapPosition position)
    {
        var screen = Screen.FromControl(this);
        var workArea = screen.WorkingArea;

        int x = Location.X;
        int y = Location.Y;

        switch (position)
        {
            case SnapPosition.TopLeft:
                x = workArea.Left + _snapMargin;
                y = workArea.Top + _snapMargin;
                break;
            case SnapPosition.TopRight:
                x = workArea.Right - _snapMargin - Width;
                y = workArea.Top + _snapMargin;
                break;
            case SnapPosition.BottomLeft:
                x = workArea.Left + _snapMargin;
                y = workArea.Bottom - _snapMargin - Height;
                break;
            case SnapPosition.BottomRight:
                x = workArea.Right - _snapMargin - Width;
                y = workArea.Bottom - _snapMargin - Height;
                break;
            case SnapPosition.Center:
                x = workArea.Left + (workArea.Width - Width) / 2;
                y = workArea.Top + (workArea.Height - Height) / 2;
                break;
        }

        Location = new Point(x, y);
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (_isLocked) return;

        if (e.Button == MouseButtons.Left)
        {
            var edge = GetEdgeAtPoint(e.Location);
            if (edge != ResizeEdge.None)
            {
                _activeEdge = edge;
                _anchorPoint = GetAnchorPoint(edge);
                _originalSize = Size;
                _scaleAtResizeStart = _scale;
            }
            else
            {
                _isDragging = true;
                _dragStart = e.Location;
            }
        }
        else if (e.Button == MouseButtons.Right)
        {
            ShowContextMenu(e.Location);
        }
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (_isLocked) return;

        if (_activeEdge != ResizeEdge.None)
        {
            // Resizing: calculate new scale based on distance from anchor
            var currentPos = PointToScreen(e.Location);

            // Calculate distances from anchor point
            var originalDistance = GetDistanceForEdge(_activeEdge, _anchorPoint, _originalSize);
            var currentDistance = GetDistanceFromAnchor(_activeEdge, _anchorPoint, currentPos);

            if (originalDistance > 0)
            {
                var newScale = Math.Max(0.1f, Math.Min(_scaleAtResizeStart * (currentDistance / originalDistance), 10.0f));

                if (Math.Abs(newScale - _scale) > 0.001f)
                {
                    var oldSize = Size;
                    _scale = newScale;
                    UpdateScaledImage();

                    // Reposition window so anchor point stays fixed
                    RepositionForAnchor(_activeEdge, _anchorPoint);
                }
            }
        }
        else if (_isDragging)
        {
            var newX = Location.X + (e.X - _dragStart.X);
            var newY = Location.Y + (e.Y - _dragStart.Y);
            var newPos = ApplyEdgeSnapping(new Point(newX, newY));
            Location = newPos;
        }
        else
        {
            // Hover detection
            var edge = GetEdgeAtPoint(e.Location);
            if (edge != _hoveredEdge)
            {
                _hoveredEdge = edge;
                UpdateScaledImage(); // Redraw border with new opacity
            }
            Cursor = GetCursorForEdge(edge);
        }
    }

    private float GetDistanceForEdge(ResizeEdge edge, Point anchor, Size size)
    {
        // Get the original distance from anchor to the dragged edge/corner
        return edge switch
        {
            ResizeEdge.TopLeft or ResizeEdge.TopRight or
            ResizeEdge.BottomLeft or ResizeEdge.BottomRight =>
                (float)Math.Sqrt(size.Width * size.Width + size.Height * size.Height),
            ResizeEdge.Top or ResizeEdge.Bottom => size.Height,
            ResizeEdge.Left or ResizeEdge.Right => size.Width,
            _ => 1
        };
    }

    private float GetDistanceFromAnchor(ResizeEdge edge, Point anchor, Point current)
    {
        // Calculate distance based on which edge/corner is being dragged
        return edge switch
        {
            ResizeEdge.TopLeft or ResizeEdge.TopRight or
            ResizeEdge.BottomLeft or ResizeEdge.BottomRight =>
                (float)Math.Sqrt(Math.Pow(current.X - anchor.X, 2) + Math.Pow(current.Y - anchor.Y, 2)),
            ResizeEdge.Top or ResizeEdge.Bottom => Math.Abs(current.Y - anchor.Y),
            ResizeEdge.Left or ResizeEdge.Right => Math.Abs(current.X - anchor.X),
            _ => 1
        };
    }

    private void RepositionForAnchor(ResizeEdge edge, Point anchor)
    {
        // Reposition window so the anchor point stays at its original screen position
        int newX = Location.X;
        int newY = Location.Y;

        switch (edge)
        {
            case ResizeEdge.TopLeft:
                newX = anchor.X - Width;
                newY = anchor.Y - Height;
                break;
            case ResizeEdge.TopRight:
                newY = anchor.Y - Height;
                break;
            case ResizeEdge.BottomLeft:
                newX = anchor.X - Width;
                break;
            case ResizeEdge.BottomRight:
                // Anchor is top-left, no repositioning needed
                break;
            case ResizeEdge.Top:
                newX = anchor.X - Width / 2;
                newY = anchor.Y - Height;
                break;
            case ResizeEdge.Bottom:
                newX = anchor.X - Width / 2;
                break;
            case ResizeEdge.Left:
                newX = anchor.X - Width;
                newY = anchor.Y - Height / 2;
                break;
            case ResizeEdge.Right:
                newY = anchor.Y - Height / 2;
                break;
        }

        Location = new Point(newX, newY);
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            if (_activeEdge != ResizeEdge.None)
            {
                _activeEdge = ResizeEdge.None;
                ConfigChanged?.Invoke(this, EventArgs.Empty);
            }
            else if (_isDragging)
            {
                _isDragging = false;
                ConfigChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void OnMouseLeave(object? sender, EventArgs e)
    {
        if (_hoveredEdge != ResizeEdge.None)
        {
            _hoveredEdge = ResizeEdge.None;
            UpdateScaledImage();
        }
    }

    private void OnMouseWheel(object? sender, MouseEventArgs e)
    {
        if (_isLocked) return;

        // Use proportional scaling for natural feel
        // e.Delta is typically 120 per notch; this gives ~3% per notch
        var notches = e.Delta / 120.0f;
        var factor = (float)Math.Pow(1.03, notches);
        var newScale = Math.Max(0.1f, Math.Min(_scale * factor, 10.0f));

        if (newScale != _scale)
        {
            _scale = newScale;
            UpdateScaledImage();
            ConfigChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.L:
                Lock();
                break;
            case Keys.U:
                Unlock();
                break;
            case Keys.Escape:
                Close();
                break;
            case Keys.Oemplus:
            case Keys.Add:
                AdjustScale(0.1f);
                break;
            case Keys.OemMinus:
            case Keys.Subtract:
                AdjustScale(-0.1f);
                break;
            case Keys.D0:
            case Keys.NumPad0:
                ResetScale();
                break;
        }
    }

    private void AdjustScale(float delta)
    {
        if (_isLocked) return;

        var newScale = Math.Max(0.1f, Math.Min(_scale + delta, 10.0f));
        if (Math.Abs(newScale - _scale) > 0.001f)
        {
            _scale = newScale;
            UpdateScaledImage();
            ConfigChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ResetScale()
    {
        if (_isLocked) return;

        _scale = 1.0f;
        UpdateScaledImage();
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetCropTransparency(bool value)
    {
        if (_cropTransparency == value) return;
        _cropTransparency = value;
        ProcessImage();
        UpdateScaledImage();
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetPadding(int value)
    {
        value = Math.Max(0, value);
        if (_padding == value) return;
        _padding = value;
        ProcessImage();
        UpdateScaledImage();
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetSnapToEdges(bool value)
    {
        _snapToEdges = value;
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetSnapMargin(int value)
    {
        _snapMargin = Math.Max(0, value);
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Lock()
    {
        if (_isLocked) return;

        _isLocked = true;
        ApplyClickThrough();
        UpdateScaledImage();
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Unlock()
    {
        if (!_isLocked) return;

        _isLocked = false;
        RemoveClickThrough();
        UpdateScaledImage();
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyClickThrough()
    {
        var exStyle = GetWindowLongW(Handle, GWL_EXSTYLE);
        SetWindowLongW(Handle, GWL_EXSTYLE, exStyle | WS_EX_TRANSPARENT);
    }

    private void RemoveClickThrough()
    {
        var exStyle = GetWindowLongW(Handle, GWL_EXSTYLE);
        SetWindowLongW(Handle, GWL_EXSTYLE, exStyle & ~WS_EX_TRANSPARENT);
        Activate();
    }

    private void ShowContextMenu(Point location)
    {
        var menu = new ContextMenuStrip();

        // Lock/Unlock
        var lockItem = new ToolStripMenuItem(_isLocked ? "Unlock (U)" : "Lock (L)");
        lockItem.Click += (s, e) =>
        {
            if (_isLocked) Unlock();
            else Lock();
        };
        menu.Items.Add(lockItem);

        menu.Items.Add(new ToolStripSeparator());

        // Scale submenu
        var scaleMenu = new ToolStripMenuItem($"Scale ({_scale:P0})");

        var scaleUpItem = new ToolStripMenuItem("Increase (+)");
        scaleUpItem.Click += (s, e) => AdjustScale(0.1f);
        scaleMenu.DropDownItems.Add(scaleUpItem);

        var scaleDownItem = new ToolStripMenuItem("Decrease (-)");
        scaleDownItem.Click += (s, e) => AdjustScale(-0.1f);
        scaleMenu.DropDownItems.Add(scaleDownItem);

        scaleMenu.DropDownItems.Add(new ToolStripSeparator());

        foreach (var preset in new[] { 0.25f, 0.5f, 0.75f, 1.0f, 1.5f, 2.0f, 3.0f })
        {
            var presetItem = new ToolStripMenuItem($"{preset:P0}");
            var scale = preset;
            presetItem.Click += (s, e) =>
            {
                _scale = scale;
                UpdateScaledImage();
                ConfigChanged?.Invoke(this, EventArgs.Empty);
            };
            if (Math.Abs(_scale - preset) < 0.01f)
            {
                presetItem.Checked = true;
            }
            scaleMenu.DropDownItems.Add(presetItem);
        }

        menu.Items.Add(scaleMenu);

        var resetScaleItem = new ToolStripMenuItem("Reset Scale (0)");
        resetScaleItem.Click += (s, e) => ResetScale();
        menu.Items.Add(resetScaleItem);

        menu.Items.Add(new ToolStripSeparator());

        // Image processing options
        var cropItem = new ToolStripMenuItem("Crop Transparency")
        {
            Checked = _cropTransparency
        };
        cropItem.Click += (s, e) => SetCropTransparency(!_cropTransparency);
        menu.Items.Add(cropItem);

        // Padding submenu
        var paddingMenu = new ToolStripMenuItem($"Padding ({_padding}px)");
        foreach (var p in new[] { 0, 10, 20, 50, 100 })
        {
            var paddingItem = new ToolStripMenuItem($"{p}px");
            var padding = p;
            paddingItem.Click += (s, e) => SetPadding(padding);
            if (_padding == p)
            {
                paddingItem.Checked = true;
            }
            paddingMenu.DropDownItems.Add(paddingItem);
        }
        paddingMenu.DropDownItems.Add(new ToolStripSeparator());
        var customPaddingItem = new ToolStripMenuItem("Custom...");
        customPaddingItem.Click += (s, e) => ShowPaddingDialog();
        paddingMenu.DropDownItems.Add(customPaddingItem);
        menu.Items.Add(paddingMenu);

        menu.Items.Add(new ToolStripSeparator());

        // Snapping options
        var snapItem = new ToolStripMenuItem("Snap to Edges")
        {
            Checked = _snapToEdges
        };
        snapItem.Click += (s, e) => SetSnapToEdges(!_snapToEdges);
        menu.Items.Add(snapItem);

        // Snap margin submenu
        var snapMarginMenu = new ToolStripMenuItem($"Snap Margin ({_snapMargin}px)");
        foreach (var m in new[] { 0, 5, 10, 20, 50 })
        {
            var marginItem = new ToolStripMenuItem($"{m}px");
            var margin = m;
            marginItem.Click += (s, e) => SetSnapMargin(margin);
            if (_snapMargin == m)
            {
                marginItem.Checked = true;
            }
            snapMarginMenu.DropDownItems.Add(marginItem);
        }
        menu.Items.Add(snapMarginMenu);

        // Snap to position submenu
        var snapToMenu = new ToolStripMenuItem("Snap To...");
        var positions = new (string Name, SnapPosition Pos)[]
        {
            ("Top-Left", SnapPosition.TopLeft),
            ("Top-Right", SnapPosition.TopRight),
            ("Bottom-Left", SnapPosition.BottomLeft),
            ("Bottom-Right", SnapPosition.BottomRight),
            ("Center", SnapPosition.Center)
        };
        foreach (var (name, pos) in positions)
        {
            var posItem = new ToolStripMenuItem(name);
            var position = pos;
            posItem.Click += (s, e) => SnapTo(position);
            snapToMenu.DropDownItems.Add(posItem);
        }
        menu.Items.Add(snapToMenu);

        menu.Items.Add(new ToolStripSeparator());

        var closeItem = new ToolStripMenuItem("Close Overlay");
        closeItem.Click += (s, e) => Close();
        menu.Items.Add(closeItem);

        menu.Show(this, location);
    }

    private void ShowPaddingDialog()
    {
        using var form = new Form
        {
            Text = "Set Padding",
            Size = new Size(250, 120),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            StartPosition = FormStartPosition.CenterScreen,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var label = new Label { Text = "Padding (pixels):", Location = new Point(10, 15), AutoSize = true };
        var numericUpDown = new NumericUpDown
        {
            Location = new Point(110, 12),
            Size = new Size(80, 23),
            Minimum = 0,
            Maximum = 500,
            Value = _padding
        };
        var okButton = new Button { Text = "OK", DialogResult = DialogResult.OK, Location = new Point(50, 50), Size = new Size(70, 25) };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(130, 50), Size = new Size(70, 25) };

        form.Controls.AddRange(new Control[] { label, numericUpDown, okButton, cancelButton });
        form.AcceptButton = okButton;
        form.CancelButton = cancelButton;

        if (form.ShowDialog() == DialogResult.OK)
        {
            SetPadding((int)numericUpDown.Value);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        OverlayClosed?.Invoke(this, EventArgs.Empty);

        _currentImage?.Dispose();
        _croppedImage?.Dispose();
        _originalImage?.Dispose();
    }

    protected override bool ShowWithoutActivation => true;
}

public enum SnapPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Center
}

public enum ResizeEdge
{
    None,
    Top,
    Right,
    Bottom,
    Left,
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}
