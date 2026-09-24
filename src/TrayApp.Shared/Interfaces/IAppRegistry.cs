using TrayApp.Shared.Models;

namespace TrayApp.Shared.Interfaces;

public interface IAppRegistry
{
    IReadOnlyList<AppInfo> Apps { get; }
    string PubEndpoint { get; }
    string SubEndpoint { get; }
    bool NotificationsEnabled { get; }
    AppInfo? GetApp(string appId);
    void AddApp(AppInfo app);
    void UpdateApp(AppInfo app);
    bool RemoveApp(string appId);
    void SetHidden(string appId, bool hidden);
    void UpdateZeroMqEndpoints(string pubEndpoint, string subEndpoint);
    void SetNotificationsEnabled(bool enabled);
}
