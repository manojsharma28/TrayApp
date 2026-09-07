namespace TrayApp.Shared.Interfaces;

public interface IProcessManager
{
    Task<bool> IsRunningAsync(string appId, CancellationToken ct = default);
    Task StartProcessAsync(string appId, string? startCommand = null, CancellationToken ct = default);
    Task StopProcessAsync(string appId, CancellationToken ct = default);
}
