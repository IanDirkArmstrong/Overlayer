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
