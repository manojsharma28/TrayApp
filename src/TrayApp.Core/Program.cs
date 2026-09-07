using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrayApp.Shared.Interfaces;
using TrayApp.Shared.Utils;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        services.AddDefaultLogging();
        services.AddSingleton<IAppController, TrayApp.Core.Services.AppController>();
        services.AddSingleton<IStateTracker, TrayApp.Core.Services.InMemoryStateTracker>();

        // Registry
        services.AddSingleton<TrayApp.Shared.Interfaces.IAppRegistry, TrayApp.Shared.Services.AppRegistryService>();

        // Comms
        services.AddSingleton<TrayApp.Shared.Interfaces.IMessageRouter>(sp =>
        {
            var reg = sp.GetRequiredService<TrayApp.Shared.Interfaces.IAppRegistry>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TrayApp.Comms.ZeroMqMessageRouter>>();
            return new TrayApp.Comms.ZeroMqMessageRouter(reg.PubEndpoint, reg.SubEndpoint, logger);
        });

        // Process manager
        services.AddSingleton<TrayApp.Shared.Interfaces.IProcessManager, TrayApp.ProcessControl.ProcessManager>();
        services.AddHostedService<TrayApp.Core.Services.Orchestrator>();
    })
    .Build();

// Add file logger
var services = host.Services;
var loggerFactory = services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();
loggerFactory.AddProvider(new TrayApp.Shared.Utils.FileLoggerProvider(Path.Combine(AppContext.BaseDirectory, "logs", "trayapp.log")));

// Start a minimal HttpListener-based health endpoint
_ = Task.Run(() =>
{
    try
    {
        using var listener = new System.Net.HttpListener();
        listener.Prefixes.Add("http://127.0.0.1:5005/");
        listener.Start();
        var registry = services.GetRequiredService<TrayApp.Shared.Interfaces.IAppRegistry>();
        var tracker = services.GetRequiredService<TrayApp.Shared.Interfaces.IStateTracker>();
        while (true)
        {
            var ctx = listener.GetContext();
            var req = ctx.Request;
            var res = ctx.Response;
            if (req.RawUrl != null && req.RawUrl.StartsWith("/health"))
            {
                var buf = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { status = "ok" });
                res.ContentType = "application/json";
                res.OutputStream.Write(buf, 0, buf.Length);
                res.Close();
                continue;
            }

            if (req.RawUrl != null && req.RawUrl.StartsWith("/status"))
            {
                var list = registry.Apps.Select(a => new { a.Id, a.Name, status = tracker.GetStatus(a.Id) });
                var buf = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(list);
                res.ContentType = "application/json";
                res.OutputStream.Write(buf, 0, buf.Length);
                res.Close();
                continue;
            }

            res.StatusCode = 404;
            res.Close();
        }
    }
    catch (Exception ex)
    {
        var log = services.GetService<Microsoft.Extensions.Logging.ILoggerFactory>()?.CreateLogger("HealthEndpoint");
        log?.LogError(ex, "Health endpoint failed");
    }
});

await host.RunAsync();
