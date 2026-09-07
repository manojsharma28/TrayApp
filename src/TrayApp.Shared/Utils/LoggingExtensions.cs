using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TrayApp.Shared.Utils;

public static class LoggingExtensions
{
    public static IServiceCollection AddDefaultLogging(this IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
        return services;
    }
}
