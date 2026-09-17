using System.Text.Json.Serialization;

namespace TrayApp.Shared.Models;

public class AppConfiguration
{
    [JsonPropertyName("apps")]
    public List<AppInfo> Apps { get; set; } = new();

    [JsonPropertyName("zeromq")]
    public ZeroMqConfig ZeroMq { get; set; } = new("tcp://127.0.0.1:5556", "tcp://127.0.0.1:5557");
}
