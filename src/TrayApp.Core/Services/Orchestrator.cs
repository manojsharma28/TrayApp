using Microsoft.Extensions.Hosting;
using System.Text.Json;
using TrayApp.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace TrayApp.Core.Services;

public class Orchestrator : IHostedService
{
    private readonly IAppRegistry _registry;
    private readonly IProcessManager _processManager;
    private readonly IMessageRouter _router;
    private readonly IStateTracker _tracker;
    private readonly ILogger<Orchestrator> _log;

    public Orchestrator(IAppRegistry registry, IProcessManager processManager, IMessageRouter router, IStateTracker tracker, ILogger<Orchestrator> log)
    {
        _registry = registry;
        _processManager = processManager;
        _router = router;
        _tracker = tracker;
        _log = log;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var app in _registry.Apps)
        {
            try
            {
                var running = await _processManager.IsRunningAsync(app.Id, cancellationToken);
                if (!running)
                {
                    _log.LogInformation("Orchestrator starting {appId} via {cmd}", app.Id, app.StartCommand ?? app.ExecutablePath);
                    await _processManager.StartProcessAsync(app.Id, app.StartCommand, cancellationToken);
                }

                var topic = $"status/{app.Id}";
                await _router.SubscribeAsync(topic, payload =>
                {
                    try
                    {
                        var status = JsonSerializer.Deserialize<TrayApp.Shared.Models.AppStatus>(payload);
                        _tracker.UpdateStatus(app.Id, status?.Status ?? payload);
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Failed to update status for {appId}", app.Id);
                    }
                    return Task.CompletedTask;
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Orchestrator error for {appId}", app.Id);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // graceful shutdown handled by DI disposables
        return Task.CompletedTask;
    }
}
