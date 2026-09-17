using System.Text.Json.Serialization;

namespace TrayApp.Shared.Models;

public record AppInfo(
	[property: JsonPropertyName("appId")]
	string Id,
	[property: JsonPropertyName("name")]
	string Name,
	[property: JsonPropertyName("executablePath")]
	string? ExecutablePath = null,
	[property: JsonPropertyName("args")]
	string? Args = null,
	[property: JsonPropertyName("startCommand")]
	string? StartCommand = null,
	[property: JsonPropertyName("zmqEndpoint")]
	string? ZmqEndpoint = null,
	[property: JsonPropertyName("category")]
	string Category = "Services",
	[property: JsonPropertyName("hidden")]
	bool IsHidden = false);
