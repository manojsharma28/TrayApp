using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Avalonia;
using TrayApp.Shared.Utils;
using TrayApp.Shared.Interfaces;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        services.AddDefaultLogging();
        services.AddSingleton<IAppController, TrayApp.Core.Services.AppController>();
        services.AddSingleton<TrayApp.Shared.Interfaces.IConfigurationService, TrayApp.Shared.Services.JsonConfigurationService>();
        services.AddSingleton<TrayApp.Shared.Interfaces.IAppRegistry, TrayApp.Shared.Services.AppRegistryService>();
        services.AddSingleton<TrayApp.Shared.Interfaces.IStateTracker, TrayApp.Core.Services.InMemoryStateTracker>();

        services.AddSingleton<TrayApp.Shared.Interfaces.IMessageRouter>(sp =>
        {
            var reg = sp.GetRequiredService<TrayApp.Shared.Interfaces.IAppRegistry>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<TrayApp.Comms.ZeroMqMessageRouter>>();
            return new TrayApp.Comms.ZeroMqMessageRouter(reg.PubEndpoint, reg.SubEndpoint, logger);
        });

        services.AddSingleton<TrayApp.Shared.Interfaces.IProcessManager, TrayApp.ProcessControl.ProcessManager>();
        services.AddHostedService<TrayApp.Core.Services.Orchestrator>();
        services.AddSingleton<TrayApp.UI.Services.TrayService>();
    })
    .Build();

    // Resolve TrayService to initialize tray (best-effort)
    var services = host.Services;
    // Start host services and then run Avalonia desktop lifetime so tray can be shown
    await host.StartAsync();
    TrayApp.UI.App.Host = host;
    // Create Avalonia AppBuilder and start classic desktop lifetime (this will show tray via TrayService)
    Avalonia.AppBuilder.Configure<TrayApp.UI.App>()
        .UsePlatformDetect()
        .LogToTrace()
        .StartWithClassicDesktopLifetime(args);

    // After UI exits, stop host
    await host.StopAsync();
