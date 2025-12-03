using System.Diagnostics;
using Serilog;

namespace RssReader.Tray;

public class RssReaderService
{
    private Process? _process;
    private readonly string _rssReaderPath;
    private readonly string _workingDirectory;

    public event EventHandler<ServiceStatusChangedEventArgs>? StatusChanged;

    public bool IsRunning => _process != null && !_process.HasExited;

    public RssReaderService()
    {
        // Get the path to the RssReader executable
        var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _workingDirectory = Path.GetFullPath(Path.Combine(baseDirectory, "..", "RssReader"));
        _rssReaderPath = Path.Combine(_workingDirectory, "RssReader.exe");

        Log.Information("RssReader path: {Path}", _rssReaderPath);
        Log.Information("Working directory: {Directory}", _workingDirectory);
    }

    public bool Start()
    {
        try
        {
            if (IsRunning)
            {
                Log.Warning("RssReader is already running");
                return false;
            }

            if (!File.Exists(_rssReaderPath))
            {
                Log.Error("RssReader.exe not found at {Path}", _rssReaderPath);
                throw new FileNotFoundException($"RssReader.exe not found at {_rssReaderPath}");
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = _rssReaderPath,
                WorkingDirectory = _workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            _process = Process.Start(startInfo);
            
            if (_process != null)
            {
                _process.EnableRaisingEvents = true;
                _process.Exited += OnProcessExited;
                
                Log.Information("RssReader started successfully (PID: {ProcessId})", _process.Id);
                OnStatusChanged(true, "RssReader started successfully");
                return true;
            }

            Log.Error("Failed to start RssReader process");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error starting RssReader");
            throw;
        }
    }

    public bool Stop()
    {
        try
        {
            if (!IsRunning)
            {
                Log.Warning("RssReader is not running");
                return false;
            }

            var process = _process;
            if (process != null && !process.HasExited)
            {
                var processId = process.Id;
                Log.Information("Stopping RssReader (PID: {ProcessId})", processId);
                
                try
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
                catch (InvalidOperationException)
                {
                    // Process already exited, ignore
                    Log.Information("Process already exited");
                }
                
                process.Dispose();
                _process = null;
                
                Log.Information("RssReader stopped successfully");
                OnStatusChanged(false, "RssReader stopped successfully");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error stopping RssReader");
            throw;
        }
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        Log.Information("RssReader process exited");
        OnStatusChanged(false, "RssReader process exited");
        
        if (_process != null)
        {
            _process.Dispose();
            _process = null;
        }
    }

    protected virtual void OnStatusChanged(bool isRunning, string message)
    {
        StatusChanged?.Invoke(this, new ServiceStatusChangedEventArgs(isRunning, message));
    }

    public void Dispose()
    {
        if (_process != null && !_process.HasExited)
        {
            Stop();
        }
    }
}

public class ServiceStatusChangedEventArgs : EventArgs
{
    public bool IsRunning { get; }
    public string Message { get; }

    public ServiceStatusChangedEventArgs(bool isRunning, string message)
    {
        IsRunning = isRunning;
        Message = message;
    }
}
