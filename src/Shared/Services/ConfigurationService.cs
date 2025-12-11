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
