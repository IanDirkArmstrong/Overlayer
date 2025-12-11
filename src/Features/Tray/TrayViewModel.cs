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
