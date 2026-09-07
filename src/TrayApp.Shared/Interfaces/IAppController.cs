namespace TrayApp.Shared.Interfaces;

public interface IAppController
{
    Task StartAsync(string appId, CancellationToken ct = default);
    Task StopAsync(string appId, CancellationToken ct = default);
}
