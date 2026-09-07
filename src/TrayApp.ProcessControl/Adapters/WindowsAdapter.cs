using TrayApp.Shared.Interfaces;
using TrayApp.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace TrayApp.ProcessControl.Adapters;

public class WindowsAdapter : IProcessManager
{
    private readonly ILogger<WindowsAdapter> _log;
    private readonly IAppRegistry _registry;

    public WindowsAdapter(ILogger<WindowsAdapter> log, IAppRegistry registry)
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
            var target = ResolveProcessName(app);
            if (string.IsNullOrWhiteSpace(target))
            {
                return Task.FromResult(false);
            }

            var procs = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(target));
            return Task.FromResult(procs.Length > 0);
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
            var commandToRun = !string.IsNullOrWhiteSpace(startCommand) ? startCommand : app.StartCommand ?? app.ExecutablePath;

            if (!string.IsNullOrWhiteSpace(commandToRun))
            {
                var psi = new ProcessStartInfo
                {
                    FileName =$"{commandToRun}",
                  //  Arguments = $"/C \"{commandToRun}\"",
                    UseShellExecute = false,
                };
                Process.Start(psi);
            }
            else if (!string.IsNullOrWhiteSpace(app.ExecutablePath))
            {
                var start = new ProcessStartInfo
                {
                    FileName = app.ExecutablePath,
                    Arguments = app.Args ?? string.Empty,
                    UseShellExecute = true,
                };
                Process.Start(start);
            }
            else
            {
                _log.LogWarning("No valid command found for app {appId}", appId);
                return Task.CompletedTask;
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
            var target = ResolveProcessName(app);
            if (string.IsNullOrWhiteSpace(target))
            {
                _log.LogWarning("No process name found for app {appId}", appId);
                return Task.CompletedTask;
            }

            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(target));
            foreach (var process in processes)
            {
                using (process)
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
            }

            _log.LogInformation("Stopped {count} process(es) for {appId}", processes.Length, appId);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to stop {appId}", appId);
        }

        return Task.CompletedTask;
    }

    private static string? ResolveProcessName(AppInfo app)
    {
        if (!string.IsNullOrWhiteSpace(app.ExecutablePath))
        {
            return app.ExecutablePath;
        }

        if (!string.IsNullOrWhiteSpace(app.StartCommand))
        {
            var tokens = app.StartCommand.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return tokens.FirstOrDefault();
        }

        return null;
    }
}
