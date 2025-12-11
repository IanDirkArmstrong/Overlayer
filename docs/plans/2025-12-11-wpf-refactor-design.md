# WPF Refactoring Design

## Overview

Refactor Overlayer from WinForms to WPF using industry-standard MVVM architecture.

## Architecture Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| UI Pattern | MVVM | Industry standard for WPF |
| MVVM Framework | CommunityToolkit.MVVM | Lightweight, Microsoft-supported, source generators |
| System Tray | Hardcodet.NotifyIcon.Wpf | Most popular WPF tray library |
| Dependency Injection | Microsoft.Extensions.DependencyInjection | Industry standard, testable |
| Project Structure | Feature-sliced folders | Scales well for future features |
| Window Transparency | WPF native (`AllowsTransparency`) | Simpler than Win32 layered windows |

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────┐
│                      App.xaml.cs                        │
│              (DI Container Setup, App Lifecycle)        │
└─────────────────┬───────────────────────────────────────┘
                  │
    ┌─────────────┴─────────────┐
    ▼                           ▼
┌─────────────┐         ┌─────────────────┐
│   Tray      │         │    Overlay      │
│  Feature    │         │    Feature      │
├─────────────┤         ├─────────────────┤
│ TrayIcon    │ creates │ OverlayWindow   │
│ ViewModel   │────────▶│ ViewModel       │
│ Service     │         │ Service         │
└─────────────┘         └─────────────────┘
        │                       │
        └───────────┬───────────┘
                    ▼
          ┌─────────────────┐
          │     Shared      │
          ├─────────────────┤
          │ ConfigService   │
          │ OverlayConfig   │
          │ NativeMethods   │
          └─────────────────┘
```

## Project Structure

```
src/
├── App.xaml                    # Application resources, startup
├── App.xaml.cs                 # DI setup, lifecycle
├── Features/
│   ├── Overlay/
│   │   ├── OverlayWindow.xaml      # Transparent overlay view
│   │   ├── OverlayWindow.xaml.cs   # Minimal code-behind
│   │   ├── OverlayViewModel.cs     # Overlay state & commands
│   │   └── OverlayService.cs       # Manages overlay instances
│   └── Tray/
│       ├── TrayViewModel.cs        # Menu commands
│       └── TrayIconService.cs      # NotifyIcon setup
├── Shared/
│   ├── Models/
│   │   ├── OverlayConfig.cs        # Single overlay config
│   │   └── AppConfig.cs            # Root config container
│   ├── Services/
│   │   └── ConfigurationService.cs # JSON load/save
│   └── Native/
│       └── NativeMethods.cs        # P/Invoke (click-through)
└── Overlayer.csproj
```

## Dependencies

```xml
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
<PackageReference Include="Hardcodet.NotifyIcon.Wpf" Version="1.*" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.*" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.*" />
```

## Key Implementation Details

### OverlayWindow.xaml

```xml
<Window x:Class="Overlayer.Features.Overlay.OverlayWindow"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        ResizeMode="NoResize">
    <Grid>
        <!-- Resize border (visible when unlocked) -->
        <Border BorderBrush="#4000B4EF"
                BorderThickness="1"
                Visibility="{Binding IsLocked, Converter={StaticResource InverseBoolToVisibility}}"/>

        <!-- The overlay image -->
        <Image Source="{Binding CurrentImage}"
               Stretch="None"
               RenderOptions.BitmapScalingMode="HighQuality"/>
    </Grid>
</Window>
```

### OverlayViewModel.cs

```csharp
public partial class OverlayViewModel : ObservableObject
{
    [ObservableProperty] private string _imagePath;
    [ObservableProperty] private double _x, _y;
    [ObservableProperty] private double _scale = 1.0;
    [ObservableProperty] private bool _isLocked;
    [ObservableProperty] private bool _cropTransparency = true;
    [ObservableProperty] private int _padding = 10;
    [ObservableProperty] private bool _snapToEdges = true;
    [ObservableProperty] private int _snapMargin = 10;
    [ObservableProperty] private ImageSource? _currentImage;

    [RelayCommand] private void Lock() { ... }
    [RelayCommand] private void Unlock() { ... }
    [RelayCommand] private void SetScale(double scale) { ... }
    [RelayCommand] private void Close() { ... }
}
```

### App.xaml.cs

```csharp
public partial class App : Application
{
    private IHost _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Services
                services.AddSingleton<ConfigurationService>();
                services.AddSingleton<OverlayService>();
                services.AddSingleton<TrayIconService>();

                // ViewModels
                services.AddSingleton<TrayViewModel>();
                services.AddTransient<OverlayViewModel>();
            })
            .Build();

        var trayService = _host.Services.GetRequiredService<TrayIconService>();
        trayService.Initialize();

        var overlayService = _host.Services.GetRequiredService<OverlayService>();
        overlayService.LoadFromConfig();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host.Services.GetRequiredService<OverlayService>().SaveConfig();
        _host.Dispose();
    }
}
```

### App.xaml

```xml
<Application x:Class="Overlayer.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             ShutdownMode="OnExplicitShutdown">
    <Application.Resources>
        <!-- Converters, styles -->
    </Application.Resources>
</Application>
```

## Migration Strategy

### Code to port (with modifications)

| Current File | Destination | Notes |
|--------------|-------------|-------|
| `OverlayWindow.cs` | Split into `OverlayWindow.xaml`, `OverlayViewModel.cs`, code-behind | Logic stays, UI becomes XAML |
| `TrayController.cs` | Split into `TrayViewModel.cs`, `TrayIconService.cs`, `OverlayService.cs` | Cleaner separation |
| `ConfigManager.cs` | `ConfigurationService.cs` | Minor refactor |
| `NativeMethods.cs` | `Shared/Native/NativeMethods.cs` | Keep P/Invoke for click-through |
| `IconGenerator.cs` | Keep as utility or remove | Can regenerate icon once |

### Code to rewrite for WPF

- Window transparency setup (WPF native instead of `UpdateLayeredWindow`)
- Image rendering (`<Image>` control instead of GDI+)
- Context menus (WPF `ContextMenu` instead of WinForms)
- Mouse/keyboard handling (WPF events instead of WinForms)

### Code to keep as-is

- Image processing logic (crop transparency, padding) — works with `System.Drawing`
- Edge snapping calculations
- Resize hit detection logic
- Config JSON format

### Features preserved

- All current functionality (drag, resize, scale, lock, snap, padding, margins)
- Same keyboard shortcuts (L, U, +, -, 0, Esc)
- Same config file (seamless upgrade)
- Same command-line arguments
