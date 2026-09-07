namespace TrayApp.Shared.Interfaces;

public interface IMessageRouter
{
    Task PublishAsync(string topic, string payload, CancellationToken ct = default);
    Task SubscribeAsync(string topic, Func<string, Task> handler, CancellationToken ct = default);
}
