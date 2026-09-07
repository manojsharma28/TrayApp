namespace TrayApp.Shared.Models;

public record AppInfo(string Id, string Name, string? ExecutablePath = null, string? Args = null, string? StartCommand = null, string? ZmqEndpoint = null);
