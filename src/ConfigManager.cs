using System.Text.Json;
using System.Text.Json.Serialization;

namespace Overlayer;

/// <summary>
/// Configuration for a single overlay.
/// </summary>
public class OverlayConfig
{
    [JsonPropertyName("imagePath")]
    public string ImagePath { get; set; } = string.Empty;

    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    [JsonPropertyName("scale")]
    public float Scale { get; set; } = 1.0f;

    [JsonPropertyName("locked")]
    public bool Locked { get; set; }

    [JsonPropertyName("cropTransparency")]
    public bool CropTransparency { get; set; } = true;

    [JsonPropertyName("padding")]
    public int Padding { get; set; } = 0;

    [JsonPropertyName("snapToEdges")]
    public bool SnapToEdges { get; set; } = true;

    [JsonPropertyName("snapMargin")]
    public int SnapMargin { get; set; } = 10;
}

/// <summary>
/// Root configuration containing all overlays.
/// </summary>
public class AppConfig
{
    [JsonPropertyName("overlays")]
    public List<OverlayConfig> Overlays { get; set; } = [];
}

/// <summary>
/// Manages loading and saving of overlay configuration.
/// </summary>
public static class ConfigManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Gets the path to the configuration file (next to the executable).
    /// </summary>
    public static string ConfigPath
    {
        get
        {
            var exePath = AppContext.BaseDirectory;
            return Path.Combine(exePath, "overlayer-config.json");
        }
    }

    /// <summary>
    /// Loads configuration from disk. Returns empty config if file doesn't exist.
    /// </summary>
    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                return new AppConfig();
            }

            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            return config ?? new AppConfig();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load config: {ex.Message}");
            return new AppConfig();
        }
    }

    /// <summary>
    /// Saves configuration to disk.
    /// </summary>
    public static void Save(AppConfig config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
            MessageBox.Show($"Failed to save configuration: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Saves configuration to disk with specified overlays.
    /// </summary>
    public static void Save(IEnumerable<OverlayConfig> overlays)
    {
        var config = new AppConfig
        {
            Overlays = overlays.ToList()
        };
        Save(config);
    }
}
