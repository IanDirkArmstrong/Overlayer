using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Overlayer.Features.Overlay;
using Overlayer.Features.Tray;
using Overlayer.Shared.Services;
using System.Windows;

namespace Overlayer;

public partial class App : Application
{
    private IHost? _host;
    private TrayIconService? _trayIconService;
    private OverlayService? _overlayService;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Shared Services
                services.AddSingleton<ConfigurationService>();

                // Overlay Feature
                services.AddSingleton<OverlayService>();

                // Tray Feature
                services.AddSingleton<TrayViewModel>();
                services.AddSingleton<TrayIconService>();
            })
            .Build();

        Services = _host.Services;

        // Initialize tray icon
        _trayIconService = Services.GetRequiredService<TrayIconService>();
        _trayIconService.Initialize();

        // Load overlays from config
        _overlayService = Services.GetRequiredService<OverlayService>();
        _overlayService.LoadFromConfig();

        // Handle command-line arguments (image paths)
        if (e.Args.Length > 0)
        {
            LoadImagesFromArgs(e.Args);
        }
    }

    private void LoadImagesFromArgs(string[] args)
    {
        var supportedExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
        var offset = _overlayService!.Windows.Count * 30;

        foreach (var arg in args)
        {
            if (System.IO.File.Exists(arg))
            {
                var ext = System.IO.Path.GetExtension(arg).ToLowerInvariant();
                if (supportedExtensions.Contains(ext))
                {
                    _overlayService.CreateOverlay(arg, 100 + offset, 100 + offset);
                    offset += 30;
                }
            }
            else if (System.IO.Directory.Exists(arg))
            {
                var files = System.IO.Directory.GetFiles(arg)
                    .Where(f => supportedExtensions.Contains(
                        System.IO.Path.GetExtension(f).ToLowerInvariant()));

                foreach (var file in files)
                {
                    _overlayService.CreateOverlay(file, 100 + offset, 100 + offset);
                    offset += 30;
                }
            }
        }

        if (offset > 0)
        {
            _overlayService.SaveConfig();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _overlayService?.Dispose();
        _trayIconService?.Dispose();
        _host?.Dispose();
        base.OnExit(e);
    }
}
