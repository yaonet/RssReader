using Serilog;

namespace RssReader.Tray;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Configure Serilog
        var logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(logFolder);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logFolder, "rssreader-tray-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            Log.Information("Starting RssReader Tray application");

            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());

            Log.Information("RssReader Tray application exited normally");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal error in RssReader Tray application");
            MessageBox.Show($"Fatal error: {ex.Message}", "RssReader Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
