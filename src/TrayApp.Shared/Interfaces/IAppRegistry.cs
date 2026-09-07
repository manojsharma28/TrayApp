using TrayApp.Shared.Models;

namespace TrayApp.Shared.Interfaces;

public interface IAppRegistry
{
    IReadOnlyList<AppInfo> Apps { get; }
    string PubEndpoint { get; }
    string SubEndpoint { get; }
    AppInfo? GetApp(string appId);
    void UpdateZeroMqEndpoints(string pubEndpoint, string subEndpoint);
}
