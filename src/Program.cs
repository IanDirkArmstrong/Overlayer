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

        // Check for command line arguments (load images from directory)
        if (args.Length > 0)
        {
            var dirPath = args[0];
            if (Directory.Exists(dirPath))
            {
                LoadImagesFromDirectoryOnStartup(dirPath);
            }
        }

        // Run the application with the tray controller
        Application.Run(new TrayController());
    }

    private static void LoadImagesFromDirectoryOnStartup(string directoryPath)
    {
        // This will be handled by creating a config if none exists
        // and the TrayController will load it
        var config = ConfigManager.Load();

        if (config.Overlays.Count == 0)
        {
            var extensions = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif" };
            var files = extensions
                .SelectMany(ext => Directory.GetFiles(directoryPath, ext, SearchOption.TopDirectoryOnly))
                .ToList();

            int offset = 0;
            foreach (var file in files)
            {
                config.Overlays.Add(new OverlayConfig
                {
                    ImagePath = file,
                    X = 100 + offset,
                    Y = 100 + offset,
                    Scale = 1.0f,
                    Locked = false
                });
                offset += 30;
            }

            ConfigManager.Save(config);
        }
    }
}
