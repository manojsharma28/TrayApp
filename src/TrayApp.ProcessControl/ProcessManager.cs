using TrayApp.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace TrayApp.ProcessControl;

public class ProcessManager : IProcessManager
{

    private readonly IProcessManager _impl;

    public ProcessManager(Microsoft.Extensions.Logging.ILoggerFactory loggerFactory, TrayApp.Shared.Interfaces.IAppRegistry registry)
    {
        if (OperatingSystem.IsWindows())
            _impl = new Adapters.WindowsAdapter(loggerFactory.CreateLogger<Adapters.WindowsAdapter>(), registry);
        else
            _impl = new Adapters.LinuxAdapter(loggerFactory.CreateLogger<Adapters.LinuxAdapter>(), registry);
    }

    public Task<bool> IsRunningAsync(string appId, CancellationToken ct = default) => _impl.IsRunningAsync(appId, ct);
    public Task StartProcessAsync(string appId, string? startCommand = null, CancellationToken ct = default) => _impl.StartProcessAsync(appId, startCommand, ct);
    public Task StopProcessAsync(string appId, CancellationToken ct = default) => _impl.StopProcessAsync(appId, ct);
}
