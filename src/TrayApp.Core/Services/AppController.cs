using TrayApp.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace TrayApp.Core.Services;

public class AppController : IAppController
{
    private readonly IAppRegistry _registry;
    private readonly IProcessManager _processManager;
    private readonly IMessageRouter _router;
    private readonly ILogger<AppController> _log;

    public AppController(IAppRegistry registry, IProcessManager processManager, IMessageRouter router, ILogger<AppController> log)
    {
        _registry = registry;
        _processManager = processManager;
        _router = router;
        _log = log;
    }

    public async Task StartAsync(string appId, CancellationToken ct = default)
    {
        _log.LogInformation("Starting {appId}", appId);

        var app = _registry.GetApp(appId);
        var command = app?.StartCommand ?? app?.ExecutablePath;

        if (!await _processManager.IsRunningAsync(appId, ct))
        {
            await _processManager.StartProcessAsync(appId, command, ct);
        }

        await _router.PublishAsync("control/start", appId, ct);
    }

    public async Task StopAsync(string appId, CancellationToken ct = default)
    {
        _log.LogInformation("Stopping {appId}", appId);
        await _processManager.StopProcessAsync(appId, ct);
        await _router.PublishAsync("control/stop", appId, ct);
    }
}
