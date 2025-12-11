using Overlayer.Shared.Models;
using Overlayer.Shared.Services;

namespace Overlayer.Features.Overlay;

public class OverlayService
{
    private readonly ConfigurationService _configService;
    private readonly List<OverlayWindow> _windows = new();
    private bool _configDirty;
    private System.Timers.Timer? _autoSaveTimer;

    public event Action? OverlaysChanged;

    public IReadOnlyList<OverlayWindow> Windows => _windows;

    public OverlayService(ConfigurationService configService)
    {
        _configService = configService;
        SetupAutoSave();
    }

    private void SetupAutoSave()
    {
        _autoSaveTimer = new System.Timers.Timer(30000); // 30 seconds
        _autoSaveTimer.Elapsed += (s, e) =>
        {
            if (_configDirty)
            {
                SaveConfig();
                _configDirty = false;
            }
        };
        _autoSaveTimer.Start();
    }

    public void LoadFromConfig()
    {
        var config = _configService.Load();
        foreach (var overlayConfig in config.Overlays)
        {
            if (System.IO.File.Exists(overlayConfig.ImagePath))
            {
                CreateOverlay(overlayConfig);
            }
        }
    }

    public OverlayWindow CreateOverlay(string imagePath, int x = 100, int y = 100)
    {
        var config = new OverlayConfig
        {
            ImagePath = imagePath,
            X = x,
            Y = y
        };
        return CreateOverlay(config);
    }

    public OverlayWindow CreateOverlay(OverlayConfig config)
    {
        var viewModel = new OverlayViewModel(config);
        var window = new OverlayWindow(viewModel);

        viewModel.ConfigChanged += () => _configDirty = true;
        viewModel.CloseRequested += () =>
        {
            _windows.Remove(window);
            _configDirty = true;
            OverlaysChanged?.Invoke();
        };

        _windows.Add(window);
        window.Show();
        OverlaysChanged?.Invoke();

        return window;
    }

    public void SaveConfig()
    {
        var config = new AppConfig
        {
            Overlays = _windows
                .Select(w => ((OverlayViewModel)w.DataContext).ToConfig())
                .ToList()
        };
        _configService.Save(config);
    }

    public void CloseAll()
    {
        foreach (var window in _windows.ToList())
        {
            window.Close();
        }
        _windows.Clear();
    }

    public void Dispose()
    {
        _autoSaveTimer?.Stop();
        _autoSaveTimer?.Dispose();
        SaveConfig();
        CloseAll();
    }
}
