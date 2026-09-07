using TrayApp.Shared.Interfaces;
using TrayApp.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace TrayApp.ProcessControl.Adapters;

public class LinuxAdapter : IProcessManager
{
    private readonly ILogger<LinuxAdapter> _log;
    private readonly IAppRegistry _registry;

    public LinuxAdapter(ILogger<LinuxAdapter> log, IAppRegistry registry)
    {
        _log = log;
        _registry = registry;
    }

    public Task<bool> IsRunningAsync(string appId, CancellationToken ct = default)
    {
        var app = _registry.GetApp(appId);
        if (app == null) return Task.FromResult(false);
        try
        {
            var fileName = Path.GetFileName(app.ExecutablePath);
            var procs = Process.GetProcesses().Where(p => string.Equals(p.ProcessName, Path.GetFileNameWithoutExtension(fileName), StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(procs.Any());
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "IsRunning check failed for {appId}", appId);
            return Task.FromResult(false);
        }
    }

    public Task StartProcessAsync(string appId, string? startCommand = null, CancellationToken ct = default)
    {
        var app = _registry.GetApp(appId);
        if (app == null)
        {
            _log.LogWarning("No app registered with id {appId}", appId);
            return Task.CompletedTask;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(startCommand))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{startCommand}\"",
                    UseShellExecute = true,
                };
                Process.Start(psi);
            }
            else
            {
                var start = new ProcessStartInfo
                {
                    FileName = app.ExecutablePath,
                    Arguments = app.Args ?? string.Empty,
                    UseShellExecute = true,
                };
                Process.Start(start);
            }
            _log.LogInformation("Started process for {appId}", appId);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to start {appId}", appId);
        }

        return Task.CompletedTask;
    }

    public Task StopProcessAsync(string appId, CancellationToken ct = default)
    {
        var app = _registry.GetApp(appId);
        if (app == null)
        {
            _log.LogWarning("No app registered with id {appId}", appId);
            return Task.CompletedTask;
        }

        try
        {
            var target = !string.IsNullOrWhiteSpace(app.ExecutablePath)
                ? app.ExecutablePath
                : app.StartCommand?.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            var processName = Path.GetFileNameWithoutExtension(target);

            if (string.IsNullOrWhiteSpace(processName))
            {
                _log.LogWarning("No process name found for app {appId}", appId);
                return Task.CompletedTask;
            }

            foreach (var process in Process.GetProcessesByName(processName))
            {
                process.Kill(entireProcessTree: true);
                process.Dispose();
            }

            _log.LogInformation("Stopped process(es) for {appId}", appId);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to stop {appId}", appId);
        }

        return Task.CompletedTask;
    }
}
