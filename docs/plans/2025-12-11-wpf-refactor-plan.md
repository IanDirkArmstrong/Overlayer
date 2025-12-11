# WPF Refactoring Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Refactor Overlayer from WinForms to WPF using MVVM architecture while preserving all existing functionality.

**Architecture:** Feature-sliced MVVM with DI. Tray feature manages lifecycle, Overlay feature handles individual windows. Shared services for config and native interop. WPF native transparency replaces UpdateLayeredWindow P/Invoke.

**Tech Stack:** .NET 8, WPF, CommunityToolkit.MVVM, Hardcodet.NotifyIcon.Wpf, Microsoft.Extensions.DependencyInjection/Hosting

---

## Phase 1: Project Setup & Infrastructure

### Task 1: Create WPF Project Structure

**Files:**
- Modify: `src/Overlayer.csproj`
- Create: `src/App.xaml`
- Create: `src/App.xaml.cs`

**Step 1: Update csproj to WPF**

Replace the entire `src/Overlayer.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>

    <!-- Single-file publish settings -->
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>

    <!-- Assembly info -->
    <AssemblyName>Overlayer</AssemblyName>
    <Version>2.0.0</Version>
    <Company>Overlayer</Company>
    <Product>Multi-Image Overlay Tool</Product>
    <Description>A portable multi-image overlay application for Windows</Description>

    <!-- Application icon -->
    <ApplicationIcon Condition="Exists('app.ico')">app.ico</ApplicationIcon>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <PackageReference Include="Hardcodet.NotifyIcon.Wpf" Version="1.1.0" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.1" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.1" />
    <PackageReference Include="System.Text.Json" Version="9.0.0" />
  </ItemGroup>

</Project>
```

**Step 2: Create App.xaml**

Create `src/App.xaml`:

```xml
<Application x:Class="Overlayer.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnExplicitShutdown">
    <Application.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVis"/>
    </Application.Resources>
</Application>
```

**Step 3: Create minimal App.xaml.cs**

Create `src/App.xaml.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace Overlayer;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Services will be registered here in later tasks
            })
            .Build();

        Services = _host.Services;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
```

**Step 4: Delete old Program.cs**

Delete `src/Program.cs` (WPF uses App.xaml as entry point).

**Step 5: Build to verify setup**

Run: `dotnet build src/Overlayer.csproj`
Expected: BUILD SUCCEEDED (warnings about unused WinForms code are OK for now)

**Step 6: Commit**

```bash
git add src/Overlayer.csproj src/App.xaml src/App.xaml.cs
git rm src/Program.cs
git commit -m "feat: convert project to WPF with DI infrastructure"
```

---

### Task 2: Create Folder Structure

**Files:**
- Create: `src/Features/Overlay/.gitkeep`
- Create: `src/Features/Tray/.gitkeep`
- Create: `src/Shared/Models/.gitkeep`
- Create: `src/Shared/Services/.gitkeep`
- Create: `src/Shared/Native/.gitkeep`
- Create: `src/Shared/Converters/.gitkeep`

**Step 1: Create directory structure**

```bash
mkdir -p src/Features/Overlay
mkdir -p src/Features/Tray
mkdir -p src/Shared/Models
mkdir -p src/Shared/Services
mkdir -p src/Shared/Native
mkdir -p src/Shared/Converters
```

**Step 2: Add .gitkeep files**

```bash
touch src/Features/Overlay/.gitkeep
touch src/Features/Tray/.gitkeep
touch src/Shared/Models/.gitkeep
touch src/Shared/Services/.gitkeep
touch src/Shared/Native/.gitkeep
touch src/Shared/Converters/.gitkeep
```

**Step 3: Commit**

```bash
git add src/Features src/Shared
git commit -m "chore: add feature-sliced folder structure"
```

---

## Phase 2: Shared Infrastructure

### Task 3: Port Configuration Models

**Files:**
- Create: `src/Shared/Models/OverlayConfig.cs`
- Create: `src/Shared/Models/AppConfig.cs`
- Test: Manual verification (models are data-only)

**Step 1: Create OverlayConfig.cs**

Create `src/Shared/Models/OverlayConfig.cs`:

```csharp
namespace Overlayer.Shared.Models;

public class OverlayConfig
{
    public string ImagePath { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public float Scale { get; set; } = 1.0f;
    public bool Locked { get; set; }
    public bool CropTransparency { get; set; } = true;
    public int Padding { get; set; } = 10;
    public bool SnapToEdges { get; set; } = true;
    public int SnapMargin { get; set; } = 10;
}
```

**Step 2: Create AppConfig.cs**

Create `src/Shared/Models/AppConfig.cs`:

```csharp
namespace Overlayer.Shared.Models;

public class AppConfig
{
    public List<OverlayConfig> Overlays { get; set; } = new();
}
```

**Step 3: Build to verify**

Run: `dotnet build src/Overlayer.csproj`
Expected: BUILD SUCCEEDED

**Step 4: Commit**

```bash
git add src/Shared/Models/OverlayConfig.cs src/Shared/Models/AppConfig.cs
git commit -m "feat: add configuration models"
```

---

### Task 4: Port ConfigurationService

**Files:**
- Create: `src/Shared/Services/ConfigurationService.cs`
- Delete: `src/ConfigManager.cs` (after porting)

**Step 1: Create ConfigurationService.cs**

Create `src/Shared/Services/ConfigurationService.cs`:

```csharp
using System.IO;
using System.Text.Json;
using Overlayer.Shared.Models;

namespace Overlayer.Shared.Services;

public class ConfigurationService
{
    private const string ConfigFileName = ".overlayer-config.json";
    private const string OldConfigFileName = "overlayer-config.json";

    private readonly string _configPath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public ConfigurationService()
    {
        var baseDir = AppContext.BaseDirectory;
        _configPath = Path.Combine(baseDir, ConfigFileName);

        MigrateOldConfig(baseDir);
    }

    private void MigrateOldConfig(string baseDir)
    {
        var oldPath = Path.Combine(baseDir, OldConfigFileName);
        if (File.Exists(oldPath) && !File.Exists(_configPath))
        {
            File.Move(oldPath, _configPath);
            SetHiddenAttribute(_configPath);
        }
    }

    private static void SetHiddenAttribute(string path)
    {
        if (OperatingSystem.IsWindows() && File.Exists(path))
        {
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);
        }
    }

    public AppConfig Load()
    {
        if (!File.Exists(_configPath))
            return new AppConfig();

        try
        {
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions) ?? new AppConfig();
        }
        catch
        {
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config, _jsonOptions);
        File.WriteAllText(_configPath, json);
        SetHiddenAttribute(_configPath);
    }
}
```

**Step 2: Build to verify**

Run: `dotnet build src/Overlayer.csproj`
Expected: BUILD SUCCEEDED

**Step 3: Delete old ConfigManager.cs**

```bash
git rm src/ConfigManager.cs
```

**Step 4: Commit**

```bash
git add src/Shared/Services/ConfigurationService.cs
git commit -m "feat: port ConfigManager to ConfigurationService"
```

---

### Task 5: Port NativeMethods

**Files:**
- Move: `src/NativeMethods.cs` → `src/Shared/Native/NativeMethods.cs`
- Modify: Update namespace

**Step 1: Move and update NativeMethods.cs**

```bash
git mv src/NativeMethods.cs src/Shared/Native/NativeMethods.cs
```

**Step 2: Update namespace**

In `src/Shared/Native/NativeMethods.cs`, change the namespace from `Overlayer` to:

```csharp
namespace Overlayer.Shared.Native;
```

**Step 3: Build to verify**

Run: `dotnet build src/Overlayer.csproj`
Expected: BUILD SUCCEEDED (with warnings about unused code)

**Step 4: Commit**

```bash
git add src/Shared/Native/NativeMethods.cs
git commit -m "refactor: move NativeMethods to Shared/Native"
```

---

### Task 6: Create InverseBooleanToVisibilityConverter

**Files:**
- Create: `src/Shared/Converters/InverseBooleanToVisibilityConverter.cs`
- Modify: `src/App.xaml` (register converter)

**Step 1: Create the converter**

Create `src/Shared/Converters/InverseBooleanToVisibilityConverter.cs`:

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Overlayer.Shared.Converters;

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
            return visibility != Visibility.Visible;
        return false;
    }
}
```

**Step 2: Register in App.xaml**

Update `src/App.xaml`:

```xml
<Application x:Class="Overlayer.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:converters="clr-namespace:Overlayer.Shared.Converters"
             ShutdownMode="OnExplicitShutdown">
    <Application.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVis"/>
        <converters:InverseBooleanToVisibilityConverter x:Key="InverseBoolToVis"/>
    </Application.Resources>
</Application>
```

**Step 3: Build to verify**

Run: `dotnet build src/Overlayer.csproj`
Expected: BUILD SUCCEEDED

**Step 4: Commit**

```bash
git add src/Shared/Converters/InverseBooleanToVisibilityConverter.cs src/App.xaml
git commit -m "feat: add InverseBooleanToVisibilityConverter"
```

---

## Phase 3: Overlay Feature

### Task 7: Create OverlayViewModel

**Files:**
- Create: `src/Features/Overlay/OverlayViewModel.cs`

**Step 1: Create OverlayViewModel.cs**

Create `src/Features/Overlay/OverlayViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Overlayer.Shared.Models;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Overlayer.Features.Overlay;

public partial class OverlayViewModel : ObservableObject
{
    [ObservableProperty] private string _imagePath = string.Empty;
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _scale = 1.0;
    [ObservableProperty] private bool _isLocked;
    [ObservableProperty] private bool _cropTransparency = true;
    [ObservableProperty] private int _padding = 10;
    [ObservableProperty] private bool _snapToEdges = true;
    [ObservableProperty] private int _snapMargin = 10;
    [ObservableProperty] private ImageSource? _currentImage;
    [ObservableProperty] private double _imageWidth;
    [ObservableProperty] private double _imageHeight;

    public event Action? ConfigChanged;
    public event Action? CloseRequested;

    public OverlayViewModel()
    {
    }

    public OverlayViewModel(OverlayConfig config)
    {
        _imagePath = config.ImagePath;
        _x = config.X;
        _y = config.Y;
        _scale = config.Scale;
        _isLocked = config.Locked;
        _cropTransparency = config.CropTransparency;
        _padding = config.Padding;
        _snapToEdges = config.SnapToEdges;
        _snapMargin = config.SnapMargin;
    }

    public OverlayConfig ToConfig() => new()
    {
        ImagePath = ImagePath,
        X = (int)X,
        Y = (int)Y,
        Scale = (float)Scale,
        Locked = IsLocked,
        CropTransparency = CropTransparency,
        Padding = Padding,
        SnapToEdges = SnapToEdges,
        SnapMargin = SnapMargin
    };

    partial void OnImagePathChanged(string value)
    {
        LoadImage();
        RaiseConfigChanged();
    }

    partial void OnXChanged(double value) => RaiseConfigChanged();
    partial void OnYChanged(double value) => RaiseConfigChanged();
    partial void OnScaleChanged(double value) => RaiseConfigChanged();
    partial void OnIsLockedChanged(bool value) => RaiseConfigChanged();
    partial void OnCropTransparencyChanged(bool value)
    {
        LoadImage();
        RaiseConfigChanged();
    }
    partial void OnPaddingChanged(int value)
    {
        LoadImage();
        RaiseConfigChanged();
    }
    partial void OnSnapToEdgesChanged(bool value) => RaiseConfigChanged();
    partial void OnSnapMarginChanged(int value) => RaiseConfigChanged();

    private void RaiseConfigChanged() => ConfigChanged?.Invoke();

    [RelayCommand]
    private void Lock() => IsLocked = true;

    [RelayCommand]
    private void Unlock() => IsLocked = false;

    [RelayCommand]
    private void ToggleLock() => IsLocked = !IsLocked;

    [RelayCommand]
    private void SetScale(double scale) => Scale = Math.Clamp(scale, 0.1, 10.0);

    [RelayCommand]
    private void ScaleUp() => Scale = Math.Clamp(Scale + 0.1, 0.1, 10.0);

    [RelayCommand]
    private void ScaleDown() => Scale = Math.Clamp(Scale - 0.1, 0.1, 10.0);

    [RelayCommand]
    private void ResetScale() => Scale = 1.0;

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke();

    public void LoadImage()
    {
        if (string.IsNullOrEmpty(ImagePath) || !System.IO.File.Exists(ImagePath))
        {
            CurrentImage = null;
            ImageWidth = 0;
            ImageHeight = 0;
            return;
        }

        try
        {
            // Load bitmap for processing
            using var original = new System.Drawing.Bitmap(ImagePath);
            var processed = ProcessImage(original);

            // Convert to WPF ImageSource
            CurrentImage = ConvertToImageSource(processed);
            ImageWidth = processed.Width;
            ImageHeight = processed.Height;

            if (processed != original)
                processed.Dispose();
        }
        catch
        {
            CurrentImage = null;
            ImageWidth = 0;
            ImageHeight = 0;
        }
    }

    private System.Drawing.Bitmap ProcessImage(System.Drawing.Bitmap original)
    {
        var result = original;

        if (CropTransparency)
        {
            var cropped = CropTransparentEdges(result);
            if (cropped != result && result != original)
                result.Dispose();
            result = cropped;
        }

        if (Padding > 0)
        {
            var padded = ApplyPadding(result, Padding);
            if (result != original)
                result.Dispose();
            result = padded;
        }

        return result;
    }

    private static System.Drawing.Bitmap CropTransparentEdges(System.Drawing.Bitmap source)
    {
        var bounds = GetNonTransparentBounds(source);
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return source;

        var cropped = new System.Drawing.Bitmap(bounds.Width, bounds.Height,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = System.Drawing.Graphics.FromImage(cropped))
        {
            g.DrawImage(source,
                new System.Drawing.Rectangle(0, 0, bounds.Width, bounds.Height),
                bounds,
                System.Drawing.GraphicsUnit.Pixel);
        }
        return cropped;
    }

    private static unsafe System.Drawing.Rectangle GetNonTransparentBounds(System.Drawing.Bitmap bitmap)
    {
        var rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        try
        {
            int minX = bitmap.Width, minY = bitmap.Height, maxX = 0, maxY = 0;
            byte* scan0 = (byte*)data.Scan0;
            int stride = data.Stride;

            for (int y = 0; y < bitmap.Height; y++)
            {
                byte* row = scan0 + (y * stride);
                for (int x = 0; x < bitmap.Width; x++)
                {
                    byte alpha = row[x * 4 + 3];
                    if (alpha > 0)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            if (maxX < minX || maxY < minY)
                return System.Drawing.Rectangle.Empty;

            return new System.Drawing.Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
    }

    private static System.Drawing.Bitmap ApplyPadding(System.Drawing.Bitmap source, int padding)
    {
        var newWidth = source.Width + (padding * 2);
        var newHeight = source.Height + (padding * 2);
        var padded = new System.Drawing.Bitmap(newWidth, newHeight,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        using (var g = System.Drawing.Graphics.FromImage(padded))
        {
            g.Clear(System.Drawing.Color.Transparent);
            g.DrawImage(source, padding, padding);
        }

        return padded;
    }

    private static BitmapSource ConvertToImageSource(System.Drawing.Bitmap bitmap)
    {
        var handle = bitmap.GetHbitmap();
        try
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                handle,
                IntPtr.Zero,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            Overlayer.Shared.Native.NativeMethods.DeleteObject(handle);
        }
    }
}
```

**Step 2: Add DeleteObject to NativeMethods if missing**

Check `src/Shared/Native/NativeMethods.cs` for `DeleteObject`. If not present, add:

```csharp
[DllImport("gdi32.dll")]
[return: MarshalAs(UnmanagedType.Bool)]
public static extern bool DeleteObject(IntPtr hObject);
```

**Step 3: Build to verify**

Run: `dotnet build src/Overlayer.csproj`
Expected: BUILD SUCCEEDED

**Step 4: Commit**

```bash
git add src/Features/Overlay/OverlayViewModel.cs src/Shared/Native/NativeMethods.cs
git commit -m "feat: add OverlayViewModel with image processing"
```

---

### Task 8: Create OverlayWindow XAML

**Files:**
- Create: `src/Features/Overlay/OverlayWindow.xaml`
- Create: `src/Features/Overlay/OverlayWindow.xaml.cs`

**Step 1: Create OverlayWindow.xaml**

Create `src/Features/Overlay/OverlayWindow.xaml`:

```xml
<Window x:Class="Overlayer.Features.Overlay.OverlayWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        Topmost="True"
        ShowInTaskbar="False"
        ResizeMode="NoResize"
        SizeToContent="WidthAndHeight">

    <Window.Resources>
        <Style x:Key="MenuItemStyle" TargetType="MenuItem">
            <Setter Property="Padding" Value="8,4"/>
        </Style>
    </Window.Resources>

    <Window.ContextMenu>
        <ContextMenu>
            <MenuItem Header="Lock (L)" Command="{Binding LockCommand}"
                      Visibility="{Binding IsLocked, Converter={StaticResource InverseBoolToVis}}"/>
            <MenuItem Header="Unlock (U)" Command="{Binding UnlockCommand}"
                      Visibility="{Binding IsLocked, Converter={StaticResource BoolToVis}}"/>
            <Separator/>
            <MenuItem Header="Scale">
                <MenuItem Header="25%" Command="{Binding SetScaleCommand}" CommandParameter="0.25"/>
                <MenuItem Header="50%" Command="{Binding SetScaleCommand}" CommandParameter="0.5"/>
                <MenuItem Header="75%" Command="{Binding SetScaleCommand}" CommandParameter="0.75"/>
                <MenuItem Header="100% (0)" Command="{Binding SetScaleCommand}" CommandParameter="1.0"/>
                <MenuItem Header="150%" Command="{Binding SetScaleCommand}" CommandParameter="1.5"/>
                <MenuItem Header="200%" Command="{Binding SetScaleCommand}" CommandParameter="2.0"/>
                <MenuItem Header="300%" Command="{Binding SetScaleCommand}" CommandParameter="3.0"/>
                <Separator/>
                <MenuItem Header="Increase (+)" Command="{Binding ScaleUpCommand}"/>
                <MenuItem Header="Decrease (-)" Command="{Binding ScaleDownCommand}"/>
            </MenuItem>
            <Separator/>
            <MenuItem Header="Close (Esc)" Command="{Binding CloseCommand}"/>
        </ContextMenu>
    </Window.ContextMenu>

    <Grid x:Name="RootGrid">
        <!-- Selection border (visible when unlocked) -->
        <Border x:Name="SelectionBorder"
                BorderBrush="#4000B4EF"
                BorderThickness="1"
                Visibility="{Binding IsLocked, Converter={StaticResource InverseBoolToVis}}"
                IsHitTestVisible="False"/>

        <!-- The overlay image -->
        <Image x:Name="OverlayImage"
               Source="{Binding CurrentImage}"
               Stretch="None"
               RenderOptions.BitmapScalingMode="HighQuality">
            <Image.LayoutTransform>
                <ScaleTransform ScaleX="{Binding Scale}" ScaleY="{Binding Scale}"/>
            </Image.LayoutTransform>
        </Image>
    </Grid>
</Window>
```

**Step 2: Create OverlayWindow.xaml.cs**

Create `src/Features/Overlay/OverlayWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Input;
using Overlayer.Shared.Native;

namespace Overlayer.Features.Overlay;

public partial class OverlayWindow : Window
{
    private OverlayViewModel ViewModel => (OverlayViewModel)DataContext;

    private bool _isDragging;
    private Point _dragStartPoint;
    private Point _windowStartPosition;

    public OverlayWindow()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseMove += OnMouseMove;
        MouseWheel += OnMouseWheel;
        KeyDown += OnKeyDown;
    }

    public OverlayWindow(OverlayViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.CloseRequested += Close;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Set initial position
        Left = ViewModel.X;
        Top = ViewModel.Y;

        // Load the image
        ViewModel.LoadImage();

        // Apply click-through if locked
        if (ViewModel.IsLocked)
            ApplyClickThrough();

        // Subscribe to lock changes
        ViewModel.PropertyChanged += (s, args) =>
        {
            if (args.PropertyName == nameof(OverlayViewModel.IsLocked))
            {
                if (ViewModel.IsLocked)
                    ApplyClickThrough();
                else
                    RemoveClickThrough();
            }
        };
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.IsLocked) return;

        _isDragging = true;
        _dragStartPoint = e.GetPosition(null);
        _windowStartPosition = new Point(Left, Top);
        CaptureMouse();
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            ReleaseMouseCapture();

            // Update ViewModel position
            ViewModel.X = Left;
            ViewModel.Y = Top;
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        var currentPoint = e.GetPosition(null);
        var offset = currentPoint - _dragStartPoint;

        var newX = _windowStartPosition.X + offset.X;
        var newY = _windowStartPosition.Y + offset.Y;

        // Apply edge snapping if enabled
        if (ViewModel.SnapToEdges)
        {
            var snapResult = ApplyEdgeSnapping(newX, newY);
            newX = snapResult.X;
            newY = snapResult.Y;
        }

        Left = newX;
        Top = newY;
    }

    private Point ApplyEdgeSnapping(double x, double y)
    {
        var screen = SystemParameters.WorkArea;
        var snapDistance = 20.0;

        // Snap to left edge
        if (Math.Abs(x) < snapDistance)
            x = 0;

        // Snap to top edge
        if (Math.Abs(y) < snapDistance)
            y = 0;

        // Snap to right edge
        var rightEdge = screen.Width - ActualWidth;
        if (Math.Abs(x - rightEdge) < snapDistance)
            x = rightEdge;

        // Snap to bottom edge
        var bottomEdge = screen.Height - ActualHeight;
        if (Math.Abs(y - bottomEdge) < snapDistance)
            y = bottomEdge;

        return new Point(x, y);
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (ViewModel.IsLocked) return;

        var notches = e.Delta / 120.0;
        var factor = Math.Pow(1.03, notches);
        ViewModel.Scale = Math.Clamp(ViewModel.Scale * factor, 0.1, 10.0);
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.L:
                ViewModel.LockCommand.Execute(null);
                break;
            case Key.U:
                ViewModel.UnlockCommand.Execute(null);
                break;
            case Key.Escape:
                ViewModel.CloseCommand.Execute(null);
                break;
            case Key.Add:
            case Key.OemPlus:
                ViewModel.ScaleUpCommand.Execute(null);
                break;
            case Key.Subtract:
            case Key.OemMinus:
                ViewModel.ScaleDownCommand.Execute(null);
                break;
            case Key.D0:
            case Key.NumPad0:
                ViewModel.ResetScaleCommand.Execute(null);
                break;
        }
    }

    private void ApplyClickThrough()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var extendedStyle = NativeMethods.GetWindowLongW(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLongW(hwnd, NativeMethods.GWL_EXSTYLE,
            extendedStyle | NativeMethods.WS_EX_TRANSPARENT);
    }

    private void RemoveClickThrough()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var extendedStyle = NativeMethods.GetWindowLongW(hwnd, NativeMethods.GWL_EXSTYLE);
        NativeMethods.SetWindowLongW(hwnd, NativeMethods.GWL_EXSTYLE,
            extendedStyle & ~NativeMethods.WS_EX_TRANSPARENT);
    }
}
```

**Step 3: Build to verify**

Run: `dotnet build src/Overlayer.csproj`
Expected: BUILD SUCCEEDED

**Step 4: Commit**

```bash
git add src/Features/Overlay/OverlayWindow.xaml src/Features/Overlay/OverlayWindow.xaml.cs
git commit -m "feat: add OverlayWindow with drag, scale, and keyboard support"
```

---

### Task 9: Create OverlayService

**Files:**
- Create: `src/Features/Overlay/OverlayService.cs`

**Step 1: Create OverlayService.cs**

Create `src/Features/Overlay/OverlayService.cs`:

```csharp
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
```

**Step 2: Build to verify**

Run: `dotnet build src/Overlayer.csproj`
Expected: BUILD SUCCEEDED

**Step 3: Commit**

```bash
git add src/Features/Overlay/OverlayService.cs
git commit -m "feat: add OverlayService for overlay lifecycle management"
```

---

## Phase 4: Tray Feature

### Task 10: Create TrayViewModel

**Files:**
- Create: `src/Features/Tray/TrayViewModel.cs`

**Step 1: Create TrayViewModel.cs**

Create `src/Features/Tray/TrayViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Overlayer.Features.Overlay;
using System.IO;
using System.Windows;

namespace Overlayer.Features.Tray;

public partial class TrayViewModel : ObservableObject
{
    private readonly OverlayService _overlayService;
    private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

    public TrayViewModel(OverlayService overlayService)
    {
        _overlayService = overlayService;
    }

    [RelayCommand]
    private void LoadImage()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All Files|*.*",
            Multiselect = true,
            Title = "Select Image(s) to Overlay"
        };

        if (dialog.ShowDialog() == true)
        {
            var offset = _overlayService.Windows.Count * 30;
            foreach (var file in dialog.FileNames)
            {
                _overlayService.CreateOverlay(file, 100 + offset, 100 + offset);
                offset += 30;
            }
        }
    }

    [RelayCommand]
    private void LoadFromDirectory()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Directory with Images"
        };

        if (dialog.ShowDialog() == true)
        {
            var files = Directory.GetFiles(dialog.FolderName)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            var offset = _overlayService.Windows.Count * 30;
            foreach (var file in files)
            {
                _overlayService.CreateOverlay(file, 100 + offset, 100 + offset);
                offset += 30;
            }
        }
    }

    [RelayCommand]
    private void ReloadConfig()
    {
        _overlayService.CloseAll();
        _overlayService.LoadFromConfig();
    }

    [RelayCommand]
    private void Exit()
    {
        _overlayService.SaveConfig();
        Application.Current.Shutdown();
    }
}
```

**Step 2: Build to verify**

Run: `dotnet build src/Overlayer.csproj`
Expected: BUILD SUCCEEDED

**Step 3: Commit**

```bash
git add src/Features/Tray/TrayViewModel.cs
git commit -m "feat: add TrayViewModel with load/reload/exit commands"
```

---

### Task 11: Create TrayIconService

**Files:**
- Create: `src/Features/Tray/TrayIconService.cs`

**Step 1: Create TrayIconService.cs**

Create `src/Features/Tray/TrayIconService.cs`:

```csharp
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
```

**Step 2: Build to verify**

Run: `dotnet build src/Overlayer.csproj`
Expected: BUILD SUCCEEDED

**Step 3: Commit**

```bash
git add src/Features/Tray/TrayIconService.cs
git commit -m "feat: add TrayIconService with NotifyIcon setup"
```

---

## Phase 5: Wire Everything Together

### Task 12: Complete App.xaml.cs DI Setup

**Files:**
- Modify: `src/App.xaml.cs`

**Step 1: Update App.xaml.cs with full DI registration**

Replace `src/App.xaml.cs` with:

```csharp
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
```

**Step 2: Build to verify**

Run: `dotnet build src/Overlayer.csproj`
Expected: BUILD SUCCEEDED

**Step 3: Commit**

```bash
git add src/App.xaml.cs
git commit -m "feat: wire up DI container and app lifecycle"
```

---

### Task 13: Delete Old WinForms Files

**Files:**
- Delete: `src/OverlayWindow.cs`
- Delete: `src/TrayController.cs`
- Delete: `src/IconGenerator.cs`

**Step 1: Delete old files**

```bash
git rm src/OverlayWindow.cs
git rm src/TrayController.cs
git rm src/IconGenerator.cs
```

**Step 2: Build to verify**

Run: `dotnet build src/Overlayer.csproj`
Expected: BUILD SUCCEEDED

**Step 3: Commit**

```bash
git commit -m "chore: remove old WinForms implementation files"
```

---

### Task 14: Final Build and Test

**Step 1: Clean build**

Run: `dotnet clean src/Overlayer.csproj && dotnet build src/Overlayer.csproj`
Expected: BUILD SUCCEEDED with no errors

**Step 2: Run the application**

Run: `dotnet run --project src/Overlayer.csproj`

**Step 3: Manual verification checklist**

- [ ] Tray icon appears in system tray
- [ ] Double-click tray icon opens file dialog
- [ ] Can load an image and see it as overlay
- [ ] Can drag overlay to move it
- [ ] Mouse wheel scales overlay
- [ ] L key locks overlay (click-through enabled)
- [ ] U key unlocks overlay
- [ ] Escape key closes overlay
- [ ] Right-click shows context menu
- [ ] Exit menu item closes application
- [ ] Config file saves on exit

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: complete WPF refactor - MVP functional"
```

---

## Phase 6: Advanced Features (Post-MVP)

### Task 15: Add Resize Handles

**Files:**
- Modify: `src/Features/Overlay/OverlayWindow.xaml`
- Modify: `src/Features/Overlay/OverlayWindow.xaml.cs`

**Step 1: Add resize adorner logic to OverlayWindow.xaml.cs**

Add these fields and methods to `OverlayWindow.xaml.cs`:

```csharp
// Add to class fields
private ResizeEdge _activeEdge = ResizeEdge.None;
private Point _resizeStartPoint;
private double _scaleAtResizeStart;
private Point _anchorPoint;

private enum ResizeEdge
{
    None, Left, Right, Top, Bottom,
    TopLeft, TopRight, BottomLeft, BottomRight
}

// Add these methods
private ResizeEdge GetEdgeAtPoint(Point point)
{
    const double edgeSize = 10;
    const double cornerSize = 16;

    var width = ActualWidth;
    var height = ActualHeight;

    var nearLeft = point.X < edgeSize;
    var nearRight = point.X > width - edgeSize;
    var nearTop = point.Y < edgeSize;
    var nearBottom = point.Y > height - edgeSize;

    var inCornerX = point.X < cornerSize || point.X > width - cornerSize;
    var inCornerY = point.Y < cornerSize || point.Y > height - cornerSize;

    if (nearTop && nearLeft && inCornerX && inCornerY) return ResizeEdge.TopLeft;
    if (nearTop && nearRight && inCornerX && inCornerY) return ResizeEdge.TopRight;
    if (nearBottom && nearLeft && inCornerX && inCornerY) return ResizeEdge.BottomLeft;
    if (nearBottom && nearRight && inCornerX && inCornerY) return ResizeEdge.BottomRight;
    if (nearLeft) return ResizeEdge.Left;
    if (nearRight) return ResizeEdge.Right;
    if (nearTop) return ResizeEdge.Top;
    if (nearBottom) return ResizeEdge.Bottom;

    return ResizeEdge.None;
}

private Cursor GetCursorForEdge(ResizeEdge edge) => edge switch
{
    ResizeEdge.Left or ResizeEdge.Right => Cursors.SizeWE,
    ResizeEdge.Top or ResizeEdge.Bottom => Cursors.SizeNS,
    ResizeEdge.TopLeft or ResizeEdge.BottomRight => Cursors.SizeNWSE,
    ResizeEdge.TopRight or ResizeEdge.BottomLeft => Cursors.SizeNESW,
    _ => Cursors.Arrow
};
```

**Step 2: Update mouse handling**

Update `OnMouseLeftButtonDown`:

```csharp
private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (ViewModel.IsLocked) return;

    var point = e.GetPosition(this);
    _activeEdge = GetEdgeAtPoint(point);

    if (_activeEdge != ResizeEdge.None)
    {
        // Start resize
        _resizeStartPoint = PointToScreen(point);
        _scaleAtResizeStart = ViewModel.Scale;
        _anchorPoint = GetAnchorPoint(_activeEdge);
        CaptureMouse();
    }
    else
    {
        // Start drag
        _isDragging = true;
        _dragStartPoint = e.GetPosition(null);
        _windowStartPosition = new Point(Left, Top);
        CaptureMouse();
    }
}

private Point GetAnchorPoint(ResizeEdge edge)
{
    var width = ActualWidth;
    var height = ActualHeight;

    return edge switch
    {
        ResizeEdge.TopLeft => new Point(Left + width, Top + height),
        ResizeEdge.TopRight => new Point(Left, Top + height),
        ResizeEdge.BottomLeft => new Point(Left + width, Top),
        ResizeEdge.BottomRight => new Point(Left, Top),
        ResizeEdge.Left => new Point(Left + width, Top + height / 2),
        ResizeEdge.Right => new Point(Left, Top + height / 2),
        ResizeEdge.Top => new Point(Left + width / 2, Top + height),
        ResizeEdge.Bottom => new Point(Left + width / 2, Top),
        _ => new Point(Left, Top)
    };
}
```

**Step 3: Update mouse move for resize**

Update `OnMouseMove`:

```csharp
private void OnMouseMove(object sender, MouseEventArgs e)
{
    if (ViewModel.IsLocked)
    {
        Cursor = Cursors.Arrow;
        return;
    }

    var point = e.GetPosition(this);

    if (_activeEdge != ResizeEdge.None && e.LeftButton == MouseButtonState.Pressed)
    {
        // Handle resize
        var currentPoint = PointToScreen(point);
        var delta = currentPoint - _resizeStartPoint;

        var distance = _activeEdge switch
        {
            ResizeEdge.TopLeft or ResizeEdge.BottomRight =>
                Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y),
            ResizeEdge.TopRight or ResizeEdge.BottomLeft =>
                Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y),
            ResizeEdge.Left or ResizeEdge.Right => Math.Abs(delta.X),
            ResizeEdge.Top or ResizeEdge.Bottom => Math.Abs(delta.Y),
            _ => 0
        };

        var growing = _activeEdge switch
        {
            ResizeEdge.Right or ResizeEdge.Bottom or ResizeEdge.BottomRight =>
                delta.X > 0 || delta.Y > 0,
            ResizeEdge.Left or ResizeEdge.Top or ResizeEdge.TopLeft =>
                delta.X < 0 || delta.Y < 0,
            ResizeEdge.TopRight => delta.X > 0 || delta.Y < 0,
            ResizeEdge.BottomLeft => delta.X < 0 || delta.Y > 0,
            _ => false
        };

        var factor = growing ? 1 + (distance / 200) : 1 / (1 + (distance / 200));
        ViewModel.Scale = Math.Clamp(_scaleAtResizeStart * factor, 0.1, 10.0);

        // Reposition to keep anchor fixed
        RepositionForAnchor();
    }
    else if (_isDragging)
    {
        // Existing drag logic...
        var currentPoint = e.GetPosition(null);
        var offset = currentPoint - _dragStartPoint;

        var newX = _windowStartPosition.X + offset.X;
        var newY = _windowStartPosition.Y + offset.Y;

        if (ViewModel.SnapToEdges)
        {
            var snapResult = ApplyEdgeSnapping(newX, newY);
            newX = snapResult.X;
            newY = snapResult.Y;
        }

        Left = newX;
        Top = newY;
    }
    else
    {
        // Update cursor based on edge hover
        var edge = GetEdgeAtPoint(point);
        Cursor = GetCursorForEdge(edge);
    }
}

private void RepositionForAnchor()
{
    var width = ActualWidth;
    var height = ActualHeight;

    switch (_activeEdge)
    {
        case ResizeEdge.TopLeft:
            Left = _anchorPoint.X - width;
            Top = _anchorPoint.Y - height;
            break;
        case ResizeEdge.TopRight:
            Top = _anchorPoint.Y - height;
            break;
        case ResizeEdge.BottomLeft:
            Left = _anchorPoint.X - width;
            break;
        case ResizeEdge.Top:
            Left = _anchorPoint.X - width / 2;
            Top = _anchorPoint.Y - height;
            break;
        case ResizeEdge.Bottom:
            Left = _anchorPoint.X - width / 2;
            break;
        case ResizeEdge.Left:
            Left = _anchorPoint.X - width;
            Top = _anchorPoint.Y - height / 2;
            break;
        case ResizeEdge.Right:
            Top = _anchorPoint.Y - height / 2;
            break;
    }
}
```

**Step 4: Update mouse up**

Update `OnMouseLeftButtonUp`:

```csharp
private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
{
    if (_activeEdge != ResizeEdge.None)
    {
        _activeEdge = ResizeEdge.None;
        ReleaseMouseCapture();
        ViewModel.X = Left;
        ViewModel.Y = Top;
    }
    else if (_isDragging)
    {
        _isDragging = false;
        ReleaseMouseCapture();
        ViewModel.X = Left;
        ViewModel.Y = Top;
    }
}
```

**Step 5: Build and test**

Run: `dotnet build src/Overlayer.csproj && dotnet run --project src/Overlayer.csproj`

Test:
- [ ] Cursor changes when hovering edges/corners
- [ ] Dragging edges resizes (scales) the overlay
- [ ] Opposite edge stays anchored during resize

**Step 6: Commit**

```bash
git add src/Features/Overlay/OverlayWindow.xaml.cs
git commit -m "feat: add edge/corner resize handles"
```

---

### Task 16: Add Margin Adjustment (Alt+Drag)

**Files:**
- Modify: `src/Features/Overlay/OverlayWindow.xaml`
- Modify: `src/Features/Overlay/OverlayWindow.xaml.cs`

**Step 1: Add margin visualization to XAML**

Update `src/Features/Overlay/OverlayWindow.xaml` Grid contents:

```xml
<Grid x:Name="RootGrid">
    <!-- Snap margin visualization (outer, green dashed) -->
    <Border x:Name="SnapMarginBorder"
            Margin="{Binding SnapMargin, Converter={StaticResource NegativeMarginConverter}}"
            BorderBrush="#4000FF00"
            BorderThickness="1"
            Visibility="{Binding IsLocked, Converter={StaticResource InverseBoolToVis}}"
            IsHitTestVisible="False">
        <Border.BorderBrush>
            <DrawingBrush TileMode="Tile" Viewport="0,0,8,8" ViewportUnits="Absolute">
                <DrawingBrush.Drawing>
                    <GeometryDrawing Brush="#4000FF00">
                        <GeometryDrawing.Geometry>
                            <GeometryGroup>
                                <RectangleGeometry Rect="0,0,4,1"/>
                                <RectangleGeometry Rect="4,1,4,1"/>
                            </GeometryGroup>
                        </GeometryDrawing.Geometry>
                    </GeometryDrawing>
                </DrawingBrush.Drawing>
            </DrawingBrush>
        </Border.BorderBrush>
    </Border>

    <!-- Padding visualization (inner, blue dashed) -->
    <Border x:Name="PaddingBorder"
            Margin="{Binding Padding}"
            BorderBrush="#4000B4EF"
            BorderThickness="1"
            Visibility="{Binding IsLocked, Converter={StaticResource InverseBoolToVis}}"
            IsHitTestVisible="False">
        <Border.BorderBrush>
            <DrawingBrush TileMode="Tile" Viewport="0,0,6,6" ViewportUnits="Absolute">
                <DrawingBrush.Drawing>
                    <GeometryDrawing Brush="#4000B4EF">
                        <GeometryDrawing.Geometry>
                            <GeometryGroup>
                                <RectangleGeometry Rect="0,0,3,1"/>
                                <RectangleGeometry Rect="3,1,3,1"/>
                            </GeometryGroup>
                        </GeometryDrawing.Geometry>
                    </GeometryDrawing>
                </DrawingBrush.Drawing>
            </DrawingBrush>
        </Border.BorderBrush>
    </Border>

    <!-- Selection border -->
    <Border x:Name="SelectionBorder"
            BorderBrush="#4000B4EF"
            BorderThickness="1"
            Visibility="{Binding IsLocked, Converter={StaticResource InverseBoolToVis}}"
            IsHitTestVisible="False"/>

    <!-- The overlay image -->
    <Image x:Name="OverlayImage"
           Source="{Binding CurrentImage}"
           Stretch="None"
           RenderOptions.BitmapScalingMode="HighQuality">
        <Image.LayoutTransform>
            <ScaleTransform ScaleX="{Binding Scale}" ScaleY="{Binding Scale}"/>
        </Image.LayoutTransform>
    </Image>
</Grid>
```

**Step 2: Create NegativeMarginConverter**

Create `src/Shared/Converters/NegativeMarginConverter.cs`:

```csharp
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Overlayer.Shared.Converters;

public class NegativeMarginConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int margin)
            return new Thickness(-margin);
        if (value is double marginD)
            return new Thickness(-marginD);
        return new Thickness(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
```

**Step 3: Register converter in App.xaml**

Add to App.xaml Resources:

```xml
<converters:NegativeMarginConverter x:Key="NegativeMarginConverter"/>
```

**Step 4: Add Alt+drag handling to code-behind**

Add to `OverlayWindow.xaml.cs`:

```csharp
// Add fields
private bool _isAdjustingMargin;
private MarginType _marginBeingAdjusted;
private Point _marginAdjustStart;
private int _marginStartValue;

private enum MarginType { None, Padding, SnapMargin }

// Update OnMouseLeftButtonDown
private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
{
    if (ViewModel.IsLocked) return;

    var point = e.GetPosition(this);

    // Check for Alt+drag margin adjustment
    if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
    {
        _marginBeingAdjusted = GetMarginTypeAtPoint(point);
        if (_marginBeingAdjusted != MarginType.None)
        {
            _isAdjustingMargin = true;
            _marginAdjustStart = PointToScreen(point);
            _marginStartValue = _marginBeingAdjusted == MarginType.Padding
                ? ViewModel.Padding
                : ViewModel.SnapMargin;
            CaptureMouse();
            return;
        }
    }

    // ... rest of existing logic
}

private MarginType GetMarginTypeAtPoint(Point point)
{
    var snapMargin = ViewModel.SnapMargin;
    var padding = ViewModel.Padding;
    var width = ActualWidth;
    var height = ActualHeight;

    // Check if in snap margin zone (outer expansion area)
    if (point.X < snapMargin || point.X > width - snapMargin ||
        point.Y < snapMargin || point.Y > height - snapMargin)
    {
        return MarginType.SnapMargin;
    }

    // Check if in padding zone (inner area)
    var innerLeft = snapMargin + padding;
    var innerTop = snapMargin + padding;
    var innerRight = width - snapMargin - padding;
    var innerBottom = height - snapMargin - padding;

    if (point.X < innerLeft || point.X > innerRight ||
        point.Y < innerTop || point.Y > innerBottom)
    {
        return MarginType.Padding;
    }

    return MarginType.None;
}

// Update OnMouseMove
private void OnMouseMove(object sender, MouseEventArgs e)
{
    // ... existing code ...

    if (_isAdjustingMargin && e.LeftButton == MouseButtonState.Pressed)
    {
        var currentPoint = PointToScreen(e.GetPosition(this));
        var deltaX = currentPoint.X - _marginAdjustStart.X;
        var newValue = Math.Max(0, _marginStartValue + (int)(deltaX / 2));

        if (_marginBeingAdjusted == MarginType.Padding)
            ViewModel.Padding = newValue;
        else
            ViewModel.SnapMargin = newValue;

        return;
    }

    // ... rest of existing logic ...
}

// Update OnMouseLeftButtonUp
private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
{
    if (_isAdjustingMargin)
    {
        _isAdjustingMargin = false;
        _marginBeingAdjusted = MarginType.None;
        ReleaseMouseCapture();
        return;
    }

    // ... rest of existing logic ...
}
```

**Step 5: Build and test**

Run: `dotnet build src/Overlayer.csproj && dotnet run --project src/Overlayer.csproj`

Test:
- [ ] Alt+drag on outer edge adjusts snap margin
- [ ] Alt+drag on inner edge adjusts padding
- [ ] Margin visualization updates in real-time

**Step 6: Commit**

```bash
git add src/Features/Overlay/OverlayWindow.xaml src/Features/Overlay/OverlayWindow.xaml.cs \
    src/Shared/Converters/NegativeMarginConverter.cs src/App.xaml
git commit -m "feat: add margin adjustment with Alt+drag"
```

---

## Summary

This plan converts Overlayer from WinForms to WPF in 16 incremental tasks:

| Phase | Tasks | Description |
|-------|-------|-------------|
| 1 | 1-2 | Project setup, folder structure |
| 2 | 3-6 | Shared infrastructure (models, services, converters) |
| 3 | 7-9 | Overlay feature (ViewModel, Window, Service) |
| 4 | 10-11 | Tray feature (ViewModel, Service) |
| 5 | 12-14 | Wire DI, cleanup, final test |
| 6 | 15-16 | Advanced features (resize, margins) |

Each task follows TDD principles where applicable and includes exact file paths, complete code, and verification steps.
