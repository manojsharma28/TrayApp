using TrayApp.Shared.Interfaces;
using System.Collections.Concurrent;

namespace TrayApp.Core.Services;

public class InMemoryStateTracker : IStateTracker
{
    private readonly ConcurrentDictionary<string, string> _states = new();
    public event Action<TrayApp.Shared.Models.AppStatus>? StatusUpdated;

    public string? GetStatus(string appId) => _states.TryGetValue(appId, out var s) ? s : null;

    public void UpdateStatus(string appId, string status)
    {
        _states[appId] = status;
        StatusUpdated?.Invoke(new TrayApp.Shared.Models.AppStatus(appId, status, DateTime.UtcNow));
    }
}
