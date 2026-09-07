using NetMQ;
using NetMQ.Sockets;
using TrayApp.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace TrayApp.Comms;

public class ZeroMqMessageRouter : IMessageRouter, IDisposable
{
    private readonly string _pubEndpoint;
    private readonly string _subEndpoint;
    private readonly ILogger<ZeroMqMessageRouter> _log;
    private readonly PublisherSocket _publisher;
    private readonly SubscriberSocket _subscriber;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _receiverTask;
    private readonly ConcurrentDictionary<string, List<Func<string, Task>>> _handlers = new();

    public ZeroMqMessageRouter(string pubEndpoint, string subEndpoint, ILogger<ZeroMqMessageRouter> log)
    {
        _pubEndpoint = pubEndpoint;
        _subEndpoint = subEndpoint;
        _log = log;

        _publisher = new PublisherSocket();
        _publisher.Connect(_pubEndpoint);

        _subscriber = new SubscriberSocket();
        _subscriber.Options.ReceiveHighWatermark = 1000;
        _subscriber.Connect(_subEndpoint);

        _receiverTask = Task.Run(ReceiveLoop);
    }

    public Task PublishAsync(string topic, string payload, CancellationToken ct = default)
    {
        try
        {
            _publisher.SendMoreFrame(topic).SendFrame(payload);
            _log.LogInformation("Published {topic}: {payload}", topic, payload);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Publish failed");
        }

        return Task.CompletedTask;
    }

    public Task SubscribeAsync(string topic, Func<string, Task> handler, CancellationToken ct = default)
    {
        _handlers.AddOrUpdate(topic, _ => new List<Func<string, Task>> { handler }, (_, list) => { list.Add(handler); return list; });
        try
        {
            _subscriber.Subscribe(topic);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Subscribe failed");
        }

        return Task.CompletedTask;
    }

    private void ReceiveLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var topic = _subscriber.ReceiveFrameString();
                var payload = _subscriber.ReceiveFrameString();
                if (_handlers.TryGetValue(topic, out var list))
                {
                    foreach (var h in list)
                    {
                        _ = h(payload);
                    }
                }
            }
            catch (TerminatingException) { break; }
            catch (Exception ex) { _log.LogError(ex, "Receive loop error"); Thread.Sleep(100); }
        }
    }

    public void Dispose()
    {
        try { _cts.Cancel(); } catch { }
        try { _receiverTask.Wait(500); } catch { }
        try { _subscriber.Dispose(); } catch { }
        try { _publisher.Dispose(); } catch { }
    }
}
