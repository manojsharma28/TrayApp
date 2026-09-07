 namespace TrayApp.Shared.Models;

public record ZeroMqConfig(string PubEndpoint, string SubEndpoint);

public class AppRegistry
{
    public List<AppInfo> Apps { get; set; } = new();
    public ZeroMqConfig ZeroMq { get; set; } = new ZeroMqConfig("tcp://127.0.0.1:5556", "tcp://127.0.0.1:5557");
}
