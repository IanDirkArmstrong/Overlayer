using Hardcodet.Wpf.TaskbarNotification;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;

namespace Overlayer.Features.Tray;

public class TrayIconService : IDisposable
{
    private TaskbarIcon? _trayIcon;
    private readonly TrayViewModel _viewModel;

    public TrayIconService(TrayViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public void Initialize()
    {
        _trayIcon = new TaskbarIcon
        {
            Icon = CreateTrayIcon(),
            ToolTipText = "Overlayer",
            ContextMenu = CreateContextMenu()
        };

        _trayIcon.TrayMouseDoubleClick += (s, e) => _viewModel.LoadImageCommand.Execute(null);
    }

    private System.Drawing.Icon CreateTrayIcon()
    {
        // Create a simple 16x16 icon programmatically
        using var bitmap = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bitmap);

        g.Clear(Color.Transparent);

        var brandColor = ColorTranslator.FromHtml("#00B4EF");
        using var brush = new SolidBrush(brandColor);
        using var pen = new Pen(brandColor, 1);

        // Draw stacked layers design
        g.FillRectangle(brush, 2, 10, 12, 4);  // Bottom layer
        g.DrawRectangle(pen, 3, 6, 10, 3);      // Middle layer outline
        g.DrawLine(pen, 4, 3, 12, 3);           // Top layer line

        var handle = bitmap.GetHicon();
        return System.Drawing.Icon.FromHandle(handle);
    }

    private ContextMenu CreateContextMenu()
    {
        var menu = new ContextMenu();

        var loadItem = new MenuItem { Header = "Load Image(s)..." };
        loadItem.Click += (s, e) => _viewModel.LoadImageCommand.Execute(null);
        menu.Items.Add(loadItem);

        var loadDirItem = new MenuItem { Header = "Load from Directory..." };
        loadDirItem.Click += (s, e) => _viewModel.LoadFromDirectoryCommand.Execute(null);
        menu.Items.Add(loadDirItem);

        menu.Items.Add(new Separator());

        var reloadItem = new MenuItem { Header = "Reload Configuration" };
        reloadItem.Click += (s, e) => _viewModel.ReloadConfigCommand.Execute(null);
        menu.Items.Add(reloadItem);

        menu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (s, e) => _viewModel.ExitCommand.Execute(null);
        menu.Items.Add(exitItem);

        return menu;
    }

    public void Dispose()
    {
        _trayIcon?.Dispose();
    }
}
