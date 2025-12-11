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
