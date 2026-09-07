namespace TrayApp.Shared.Interfaces;

public interface IStateTracker
{
    void UpdateStatus(string appId, string status);
    string? GetStatus(string appId);
    event Action<TrayApp.Shared.Models.AppStatus>? StatusUpdated;
}
