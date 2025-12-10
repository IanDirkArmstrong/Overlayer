namespace Overlayer;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        // Enable visual styles for modern look
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        // Handle unhandled exceptions
        Application.ThreadException += (sender, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"Thread exception: {e.Exception}");
            MessageBox.Show($"An error occurred: {e.Exception.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            System.Diagnostics.Debug.WriteLine($"Unhandled exception: {ex}");
            MessageBox.Show($"A fatal error occurred: {ex?.Message}", "Fatal Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        };

        // Check for special commands
        if (args.Length > 0 && args[0].Equals("--generate-icon", StringComparison.OrdinalIgnoreCase))
        {
            var iconPath = args.Length > 1 ? args[1] : "app.ico";
            IconGenerator.SaveIconFile(iconPath);
            Console.WriteLine($"Icon generated: {iconPath}");
            return;
        }

        // Check for command line arguments (load images from files or directory)
        if (args.Length > 0)
        {
            LoadFromCommandLineArgs(args);
        }

        // Run the application with the tray controller
        Application.Run(new TrayController());
    }

    private static void LoadFromCommandLineArgs(string[] args)
    {
        var config = ConfigManager.Load();
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".bmp", ".gif"
        };

        var filesToAdd = new List<string>();

        foreach (var arg in args)
        {
            if (Directory.Exists(arg))
            {
                // It's a directory - load all images from it
                var patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif" };
                var dirFiles = patterns
                    .SelectMany(ext => Directory.GetFiles(arg, ext, SearchOption.TopDirectoryOnly));
                filesToAdd.AddRange(dirFiles);
            }
            else if (File.Exists(arg))
            {
                // It's a file - check if it's an image
                var ext = Path.GetExtension(arg);
                if (extensions.Contains(ext))
                {
                    filesToAdd.Add(arg);
                }
            }
        }

        // Only add files that aren't already in the config
        var existingPaths = new HashSet<string>(
            config.Overlays.Select(o => o.ImagePath),
            StringComparer.OrdinalIgnoreCase);

        int offset = config.Overlays.Count * 30;
        foreach (var file in filesToAdd)
        {
            var fullPath = Path.GetFullPath(file);
            if (!existingPaths.Contains(fullPath))
            {
                config.Overlays.Add(new OverlayConfig
                {
                    ImagePath = fullPath,
                    X = 100 + offset,
                    Y = 100 + offset,
                    Scale = 1.0f,
                    Locked = false,
                    CropTransparency = true,
                    Padding = 0,
                    SnapToEdges = true,
                    SnapMargin = 10
                });
                offset += 30;
            }
        }

        if (filesToAdd.Count > 0)
        {
            ConfigManager.Save(config);
        }
    }
}
