using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace Overlayer;

/// <summary>
/// Generates the application icon programmatically.
/// </summary>
internal static class IconGenerator
{
    private static readonly Color BrandColor = Color.FromArgb(0x00, 0xB4, 0xEF); // #00B4EF

    /// <summary>
    /// Creates an icon with the stacked layers design at the specified size.
    /// </summary>
    public static Icon CreateIcon(int size)
    {
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        float scale = size / 16f;
        float penWidth = Math.Max(1f, 1.2f * scale);

        using var pen = new Pen(BrandColor, penWidth);
        pen.LineJoin = LineJoin.Round;

        // Top layer (smallest, at top) - just the top edge hint
        g.DrawLine(pen, 3 * scale, 2 * scale, 13 * scale, 2 * scale);

        // Middle layer - partial rectangle
        g.DrawLine(pen, 2 * scale, 4 * scale, 14 * scale, 4 * scale);
        g.DrawLine(pen, 2 * scale, 4 * scale, 2 * scale, 5 * scale);
        g.DrawLine(pen, 14 * scale, 4 * scale, 14 * scale, 5 * scale);

        // Bottom layer (main visible rectangle) - full rounded rect
        var mainRect = new RectangleF(1 * scale, 6 * scale, 14 * scale, 9 * scale);
        using var path = CreateRoundedRectPath(mainRect, 2 * scale);
        g.DrawPath(pen, path);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    /// <summary>
    /// Saves a multi-resolution .ico file to the specified path.
    /// </summary>
    public static void SaveIconFile(string path)
    {
        // Create icons at multiple sizes for best display
        var sizes = new[] { 16, 32, 48, 256 };

        using var fs = new FileStream(path, FileMode.Create);
        using var writer = new BinaryWriter(fs);

        // ICO header
        writer.Write((short)0);        // Reserved
        writer.Write((short)1);        // Type: 1 = ICO
        writer.Write((short)sizes.Length); // Number of images

        // Calculate offsets
        int headerSize = 6 + (sizes.Length * 16); // 6 byte header + 16 bytes per entry
        var imageData = new List<byte[]>();

        // Write directory entries
        int offset = headerSize;
        foreach (var size in sizes)
        {
            using var bitmap = CreateBitmap(size);
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            var data = ms.ToArray();
            imageData.Add(data);

            writer.Write((byte)(size == 256 ? 0 : size)); // Width (0 = 256)
            writer.Write((byte)(size == 256 ? 0 : size)); // Height (0 = 256)
            writer.Write((byte)0);      // Color palette
            writer.Write((byte)0);      // Reserved
            writer.Write((short)1);     // Color planes
            writer.Write((short)32);    // Bits per pixel
            writer.Write(data.Length);  // Image size
            writer.Write(offset);       // Offset to image data

            offset += data.Length;
        }

        // Write image data
        foreach (var data in imageData)
        {
            writer.Write(data);
        }
    }

    private static Bitmap CreateBitmap(int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bitmap);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        float scale = size / 16f;
        float penWidth = Math.Max(1f, 1.2f * scale);

        using var pen = new Pen(BrandColor, penWidth);
        pen.LineJoin = LineJoin.Round;

        // Top layer
        g.DrawLine(pen, 3 * scale, 2 * scale, 13 * scale, 2 * scale);

        // Middle layer
        g.DrawLine(pen, 2 * scale, 4 * scale, 14 * scale, 4 * scale);
        g.DrawLine(pen, 2 * scale, 4 * scale, 2 * scale, 5 * scale);
        g.DrawLine(pen, 14 * scale, 4 * scale, 14 * scale, 5 * scale);

        // Bottom layer
        var mainRect = new RectangleF(1 * scale, 6 * scale, 14 * scale, 9 * scale);
        using var path = CreateRoundedRectPath(mainRect, 2 * scale);
        g.DrawPath(pen, path);

        return bitmap;
    }

    private static GraphicsPath CreateRoundedRectPath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;

        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        return path;
    }
}
