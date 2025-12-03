using System.Diagnostics;
using Serilog;

namespace RssReader.Tray;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly RssReaderService _service;
    private readonly ContextMenuStrip _contextMenu;
    private readonly ToolStripMenuItem _startStopMenuItem;
    private readonly ToolStripMenuItem _openWebUIMenuItem;
    private readonly ToolStripMenuItem _openLogFolderMenuItem;
    private readonly ToolStripMenuItem _exitMenuItem;

    private const string WebUIUrl = "http://localhost:8099";
    private const string LogFolderPath = "logs";

    public TrayApplicationContext()
    {
        Log.Information("Initializing TrayApplicationContext");

        // Initialize service
        _service = new RssReaderService();
        _service.StatusChanged += OnServiceStatusChanged;

        // Create context menu
        _startStopMenuItem = new ToolStripMenuItem("Start RssReader", null, OnStartStopClick);
        _openWebUIMenuItem = new ToolStripMenuItem("Open Web UI", null, OnOpenWebUIClick);
        _openLogFolderMenuItem = new ToolStripMenuItem("Open Log Folder", null, OnOpenLogFolderClick);
        _exitMenuItem = new ToolStripMenuItem("Exit", null, OnExitClick);

        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.AddRange(new ToolStripItem[]
        {
            _startStopMenuItem,
            new ToolStripSeparator(),
            _openWebUIMenuItem,
            _openLogFolderMenuItem,
            new ToolStripSeparator(),
            _exitMenuItem
        });

        // Create notify icon
        _notifyIcon = new NotifyIcon
        {
            Icon = new Icon("rssreader.ico"),
            ContextMenuStrip = _contextMenu,
            Text = "RssReader - Stopped",
            Visible = true
        };

        _notifyIcon.DoubleClick += OnNotifyIconDoubleClick;

        UpdateMenuItems();

        // Auto-start the service
        try
        {
            Log.Information("Auto-starting RssReader service");
            _service.Start();
            UpdateMenuItems();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error auto-starting RssReader service");
        }

        Log.Information("TrayApplicationContext initialized");
    }

    private void OnStartStopClick(object? sender, EventArgs e)
    {
        try
        {
            if (_service.IsRunning)
            {
                _service.Stop();
            }
            else
            {
                _service.Start();
            }
            UpdateMenuItems();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error toggling service state");
            MessageBox.Show($"Error: {ex.Message}", "RssReader Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnOpenWebUIClick(object? sender, EventArgs e)
    {
        try
        {
            if (!_service.IsRunning)
            {
                var result = MessageBox.Show(
                    "RssReader is not running. Do you want to start it?",
                    "RssReader Tray",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    _service.Start();
                    UpdateMenuItems();
                    
                    // Wait a moment for the service to start
                    Thread.Sleep(2000);
                }
                else
                {
                    return;
                }
            }

            Log.Information("Opening Web UI at {Url}", WebUIUrl);
            Process.Start(new ProcessStartInfo
            {
                FileName = WebUIUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error opening Web UI");
            MessageBox.Show($"Error opening Web UI: {ex.Message}", "RssReader Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnOpenLogFolderClick(object? sender, EventArgs e)
    {
        try
        {
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var rssReaderDirectory = Path.GetFullPath(Path.Combine(baseDirectory, "..", "RssReader"));
            var logFolder = Path.Combine(rssReaderDirectory, LogFolderPath);

            if (!Directory.Exists(logFolder))
            {
                Log.Warning("Log folder does not exist, creating: {Path}", logFolder);
                Directory.CreateDirectory(logFolder);
            }

            Log.Information("Opening log folder: {Path}", logFolder);
            Process.Start(new ProcessStartInfo
            {
                FileName = logFolder,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error opening log folder");
            MessageBox.Show($"Error opening log folder: {ex.Message}", "RssReader Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnExitClick(object? sender, EventArgs e)
    {
        Log.Information("Exit requested");

        if (_service.IsRunning)
        {
            var result = MessageBox.Show(
                "RssReader is still running. Do you want to stop it before exiting?",
                "RssReader Tray",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Cancel)
            {
                return;
            }

            if (result == DialogResult.Yes)
            {
                _service.Stop();
            }
        }

        ExitApplication();
    }

    private void OnNotifyIconDoubleClick(object? sender, EventArgs e)
    {
        OnOpenWebUIClick(sender, e);
    }

    private void OnServiceStatusChanged(object? sender, ServiceStatusChangedEventArgs e)
    {
        if (_notifyIcon.ContextMenuStrip?.InvokeRequired == true)
        {
            _notifyIcon.ContextMenuStrip.Invoke(() =>
            {
                UpdateMenuItems();
                ShowNotification(e.Message);
            });
        }
        else
        {
            UpdateMenuItems();
            ShowNotification(e.Message);
        }
    }

    private void UpdateMenuItems()
    {
        if (_service.IsRunning)
        {
            _startStopMenuItem.Text = "Stop RssReader";
            _notifyIcon.Text = "RssReader - Running";
            _openWebUIMenuItem.Enabled = true;
        }
        else
        {
            _startStopMenuItem.Text = "Start RssReader";
            _notifyIcon.Text = "RssReader - Stopped";
            _openWebUIMenuItem.Enabled = true; // Keep enabled to prompt user to start
        }
    }

    private void ShowNotification(string message)
    {
        _notifyIcon.ShowBalloonTip(2000, "RssReader", message, ToolTipIcon.Info);
    }

    private void ExitApplication()
    {
        Log.Information("Shutting down application");

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();
        _service.Dispose();

        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon?.Dispose();
            _contextMenu?.Dispose();
            _service?.Dispose();
        }
        base.Dispose(disposing);
    }
}
