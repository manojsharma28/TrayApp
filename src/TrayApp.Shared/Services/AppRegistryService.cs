using System.Text.Json;
using TrayApp.Shared.Interfaces;
using TrayApp.Shared.Models;

namespace TrayApp.Shared.Services;

public class AppRegistryService : IAppRegistry
{
    private readonly AppRegistry _registry;
    private readonly string? _configPath;

    public AppRegistryService()
    {
        _configPath = ResolveConfigPath();
        if (!string.IsNullOrEmpty(_configPath) && File.Exists(_configPath))
        {
            _registry = LoadRegistry(_configPath);
        }
        else
        {
            _registry = new AppRegistry();
        }
    }

    public IReadOnlyList<AppInfo> Apps => _registry.Apps;
    public string PubEndpoint => _registry.ZeroMq.PubEndpoint;
    public string SubEndpoint => _registry.ZeroMq.SubEndpoint;
    public AppInfo? GetApp(string appId) => _registry.Apps.FirstOrDefault(a => string.Equals(a.Id, appId, StringComparison.OrdinalIgnoreCase));

    public void UpdateZeroMqEndpoints(string pubEndpoint, string subEndpoint)
    {
        var cleanPub = string.IsNullOrWhiteSpace(pubEndpoint) ? _registry.ZeroMq.PubEndpoint : pubEndpoint.Trim();
        var cleanSub = string.IsNullOrWhiteSpace(subEndpoint) ? _registry.ZeroMq.SubEndpoint : subEndpoint.Trim();

        _registry.ZeroMq = new ZeroMqConfig(cleanPub, cleanSub);

        if (!string.IsNullOrWhiteSpace(_configPath) && File.Exists(_configPath))
        {
            var json = new
            {
                apps = _registry.Apps,
                zeromq = new
                {
                    pubEndpoint = cleanPub,
                    subEndpoint = cleanSub
                }
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_configPath, JsonSerializer.Serialize(json, options));
        }
    }

    private static string? ResolveConfigPath()
    {
        var overridePath = Environment.GetEnvironmentVariable("TRAYAPP_CONFIG_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return overridePath;
        }

        var candidates = new List<string>();
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var current = new DirectoryInfo(start);
            while (current != null)
            {
                candidates.Add(Path.Combine(current.FullName, "Config", "appRegistry.json"));
                candidates.Add(Path.Combine(current.FullName, "Config", "appregistry.json"));
                candidates.Add(Path.Combine(current.FullName, "src", "TrayApp.Shared", "Config", "appRegistry.json"));
                candidates.Add(Path.Combine(current.FullName, "src", "TrayApp.Shared", "Config", "appregistry.json"));
                current = current.Parent;
            }
        }

        return candidates
            .Select(p => Path.GetFullPath(p))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(File.Exists);
    }

    private static AppRegistry LoadRegistry(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new AppRegistry();
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var apps = new List<AppInfo>();
            if (TryGetProperty(root, out var appsElement, "apps", "Apps") && appsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in appsElement.EnumerateArray())
                {
                    var appId = ReadString(item, "appId", "AppId", "id", "Id");
                    var name = ReadString(item, "name", "Name");
                    var executablePath = ReadString(item, "executablePath", "ExecutablePath", "executable", "Executable", "path", "Path");
                    var args = ReadString(item, "args", "Args");
                    var startCommand = ReadString(item, "startCommand", "StartCommand", "start", "Start", "command", "Command");
                    var zmqEndpoint = ReadString(item, "zmqEndpoint", "ZmqEndpoint", "zmq", "Zmq", "endpoint", "Endpoint");

                    if (string.IsNullOrWhiteSpace(appId))
                    {
                        continue;
                    }

                    apps.Add(new AppInfo(
                        appId,
                        string.IsNullOrWhiteSpace(name) ? appId : name,
                        executablePath,
                        args,
                        startCommand,
                        zmqEndpoint));
                }
            }

            var zeroMq = new ZeroMqConfig("tcp://127.0.0.1:5556", "tcp://127.0.0.1:5557");
            if (TryGetProperty(root, out var zmqElement, "zeromq", "ZeroMq"))
            {
                zeroMq = new ZeroMqConfig(
                    ReadString(zmqElement, "pubEndpoint", "PubEndpoint", "pub", "Pub") ?? zeroMq.PubEndpoint,
                    ReadString(zmqElement, "subEndpoint", "SubEndpoint", "sub", "Sub") ?? zeroMq.SubEndpoint);
            }

            return new AppRegistry { Apps = apps, ZeroMq = zeroMq };
        }
        catch
        {
            return new AppRegistry();
        }
    }

    private static bool TryGetProperty(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out _))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetProperty(JsonElement element, out JsonElement value, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null)
            {
                return value.GetString();
            }
        }

        return null;
    }
}
