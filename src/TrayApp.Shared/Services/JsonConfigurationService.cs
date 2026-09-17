using System.Text.Json;
using TrayApp.Shared.Interfaces;
using TrayApp.Shared.Models;

namespace TrayApp.Shared.Services;

public sealed class JsonConfigurationService : IConfigurationService
{
    private readonly string? _configPath;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public JsonConfigurationService()
        : this(ResolveConfigPath())
    {
    }

    public JsonConfigurationService(string? configPath)
    {
        _configPath = configPath;
    }

    public AppConfiguration Load()
    {
        if (string.IsNullOrWhiteSpace(_configPath) || !File.Exists(_configPath))
            return new AppConfiguration();

        try
        {
            var json = File.ReadAllText(_configPath);
            var configuration = string.IsNullOrWhiteSpace(json)
                ? new AppConfiguration()
                : JsonSerializer.Deserialize<AppConfiguration>(json, SerializerOptions) ?? new AppConfiguration();

            configuration.Apps = (configuration.Apps ?? new List<AppInfo>())
                .Where(app => !string.IsNullOrWhiteSpace(app.Id))
                .Select(app => app with
                {
                    Id = app.Id.Trim(),
                    Name = string.IsNullOrWhiteSpace(app.Name) ? app.Id.Trim() : app.Name,
                    Category = string.IsNullOrWhiteSpace(app.Category) ? "Services" : app.Category
                })
                .ToList();
            configuration.ZeroMq ??= new ZeroMqConfig("tcp://127.0.0.1:5556", "tcp://127.0.0.1:5557");
            return configuration;
        }
        catch
        {
            return new AppConfiguration();
        }
    }

    public void Save(AppConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(_configPath)) return;

        var directory = Path.GetDirectoryName(_configPath);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(_configPath, JsonSerializer.Serialize(configuration, SerializerOptions));
    }

    private static string? ResolveConfigPath()
    {
        var overridePath = Environment.GetEnvironmentVariable("TRAYAPP_CONFIG_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath)) return overridePath;

        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(start);
            while (current != null)
            {
                foreach (var path in new[]
                {
                    Path.Combine(current.FullName, "Config", "appRegistry.json"),
                    Path.Combine(current.FullName, "Config", "appregistry.json"),
                    Path.Combine(current.FullName, "src", "TrayApp.Shared", "Config", "appRegistry.json"),
                    Path.Combine(current.FullName, "src", "TrayApp.Shared", "Config", "appregistry.json")
                })
                {
                    if (File.Exists(path)) return Path.GetFullPath(path);
                }
                current = current.Parent;
            }
        }

        return null;
    }
}
