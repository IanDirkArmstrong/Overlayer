namespace Overlayer;

/// <summary>
/// Manages the system tray icon and all overlay windows.
/// </summary>
public class TrayController : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly List<OverlayWindow> _overlays = [];
    private bool _configDirty;

    public TrayController()
    {
        // Create tray icon
        _trayIcon = new NotifyIcon
        {
            Icon = CreateDefaultIcon(),
            Text = "Overlayer - Multi-Image Overlay Tool",
            Visible = true,
            ContextMenuStrip = CreateContextMenu()
        };

        _trayIcon.DoubleClick += (s, e) => LoadImage();

        // Load saved configuration
        LoadConfiguration();

        // Auto-save timer (save every 30 seconds if dirty)
        var saveTimer = new System.Windows.Forms.Timer { Interval = 30000 };
        saveTimer.Tick += (s, e) =>
        {
            if (_configDirty)
            {
                SaveConfiguration();
                _configDirty = false;
            }
        };
        saveTimer.Start();
    }

    private static Icon CreateDefaultIcon()
    {
        // Create icon based on stacked layers design (similar to Heroicons square-stack)
        using var bitmap = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        using var pen = new Pen(Color.White, 1.2f);
        pen.LineJoin = System.Drawing.Drawing2D.LineJoin.Round;

        // Top layer (smallest, at top) - just the top edge hint
        g.DrawLine(pen, 3, 2, 13, 2);

        // Middle layer - partial rectangle
        g.DrawLine(pen, 2, 4, 14, 4);
        g.DrawLine(pen, 2, 4, 2, 5);
        g.DrawLine(pen, 14, 4, 14, 5);

        // Bottom layer (main visible rectangle) - full rounded rect
        var mainRect = new Rectangle(1, 6, 14, 9);
        using var path = CreateRoundedRectPath(mainRect, 2);
        g.DrawPath(pen, path);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = radius * 2;

        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        var loadItem = new ToolStripMenuItem("Load Image...");
        loadItem.Click += (s, e) => LoadImage();
        menu.Items.Add(loadItem);

        var loadDirItem = new ToolStripMenuItem("Load from Directory...");
        loadDirItem.Click += (s, e) => LoadFromDirectory();
        menu.Items.Add(loadDirItem);

        menu.Items.Add(new ToolStripSeparator());

        var unlockAllItem = new ToolStripMenuItem("Unlock All Overlays");
        unlockAllItem.Click += (s, e) => UnlockAllOverlays();
        menu.Items.Add(unlockAllItem);

        var lockAllItem = new ToolStripMenuItem("Lock All Overlays");
        lockAllItem.Click += (s, e) => LockAllOverlays();
        menu.Items.Add(lockAllItem);

        menu.Items.Add(new ToolStripSeparator());

        var saveItem = new ToolStripMenuItem("Save Configuration");
        saveItem.Click += (s, e) => SaveConfiguration();
        menu.Items.Add(saveItem);

        var reloadItem = new ToolStripMenuItem("Reload Configuration");
        reloadItem.Click += (s, e) => ReloadConfiguration();
        menu.Items.Add(reloadItem);

        menu.Items.Add(new ToolStripSeparator());

        var closeAllItem = new ToolStripMenuItem("Close All Overlays");
        closeAllItem.Click += (s, e) => CloseAllOverlays();
        menu.Items.Add(closeAllItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => Exit();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void LoadImage()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Select Image to Overlay",
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|PNG Files|*.png|All Files|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            foreach (var file in dialog.FileNames)
            {
                CreateOverlay(file);
            }
            _configDirty = true;
        }
    }

    private void LoadFromDirectory()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select folder containing images",
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            var extensions = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif" };
            var files = extensions
                .SelectMany(ext => Directory.GetFiles(dialog.SelectedPath, ext, SearchOption.TopDirectoryOnly))
                .ToList();

            if (files.Count == 0)
            {
                MessageBox.Show("No image files found in the selected directory.", "No Images",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            foreach (var file in files)
            {
                CreateOverlay(file);
            }
            _configDirty = true;
        }
    }

    private void CreateOverlay(OverlayConfig config)
    {
        try
        {
            var overlay = new OverlayWindow(config);

            overlay.OverlayClosed += OnOverlayClosed;
            overlay.ConfigChanged += (s, e) => _configDirty = true;

            _overlays.Add(overlay);
            overlay.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to create overlay: {ex.Message}");
        }
    }

    private void CreateOverlay(string imagePath)
    {
        // Create with default settings, offset position based on existing overlays
        var offset = _overlays.Count * 30;
        var config = new OverlayConfig
        {
            ImagePath = imagePath,
            X = 100 + offset,
            Y = 100 + offset,
            Scale = 1.0f,
            Locked = false,
            CropTransparency = true,
            Padding = 0,
            SnapToEdges = true,
            SnapMargin = 10
        };
        CreateOverlay(config);
    }

    private void OnOverlayClosed(object? sender, EventArgs e)
    {
        if (sender is OverlayWindow overlay)
        {
            _overlays.Remove(overlay);
            _configDirty = true;
        }
    }

    private void UnlockAllOverlays()
    {
        foreach (var overlay in _overlays)
        {
            overlay.Unlock();
        }
    }

    private void LockAllOverlays()
    {
        foreach (var overlay in _overlays)
        {
            overlay.Lock();
        }
    }

    private void CloseAllOverlays()
    {
        // Close in reverse order to avoid collection modification issues
        for (int i = _overlays.Count - 1; i >= 0; i--)
        {
            _overlays[i].Close();
        }
    }

    private void LoadConfiguration()
    {
        var config = ConfigManager.Load();

        foreach (var overlayConfig in config.Overlays)
        {
            if (!File.Exists(overlayConfig.ImagePath))
            {
                System.Diagnostics.Debug.WriteLine($"Skipping missing image: {overlayConfig.ImagePath}");
                continue;
            }

            CreateOverlay(overlayConfig);
        }
    }

    private void SaveConfiguration()
    {
        var configs = _overlays.Select(o => o.Config).ToList();
        ConfigManager.Save(configs);
        _trayIcon.ShowBalloonTip(1000, "Overlayer", "Configuration saved.", ToolTipIcon.Info);
    }

    private void ReloadConfiguration()
    {
        CloseAllOverlays();
        LoadConfiguration();
        _trayIcon.ShowBalloonTip(1000, "Overlayer", "Configuration reloaded.", ToolTipIcon.Info);
    }

    private void Exit()
    {
        // Save before exit
        if (_configDirty || _overlays.Count > 0)
        {
            SaveConfiguration();
        }

        CloseAllOverlays();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
