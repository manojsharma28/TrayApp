using System.Text.Json;
using NetMQ;
using NetMQ.Sockets;
using Microsoft.Extensions.Logging;
using TrayApp.Shared.Models;

var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
var logger = loggerFactory.CreateLogger("ManagedApp.Template");

const string appId = "ManagedApp.Template";
var pubEndpoint = "tcp://127.0.0.1:5557"; // publishes status
var subEndpoint = "tcp://127.0.0.1:5556"; // subscribes controls

using var pub = new PublisherSocket();
using var sub = new SubscriberSocket();
sub.Connect(subEndpoint);
pub.Connect(pubEndpoint);
sub.Subscribe("control/");

_ = Task.Run(() =>
{
    while (true)
    {
        try
        {
            var topic = sub.ReceiveFrameString();
            var payload = sub.ReceiveFrameString();
            logger.LogInformation("Received {topic}: {payload}", topic, payload);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Subscriber error");
        }
    }
});

while (true)
{
    var status = new AppStatus(appId, "running", DateTime.UtcNow);
    var payload = JsonSerializer.Serialize(status);
    pub.SendMoreFrame($"status/{appId}").SendFrame(payload);
    logger.LogInformation("Published status");
    await Task.Delay(TimeSpan.FromSeconds(5));
}
