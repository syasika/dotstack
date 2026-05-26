using System.Text.Json;

namespace DotStack.Core.Configuration;

public record Config(string ContainerName, string ImageName, string Port, string EndpointUrl)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotstack");

    private static string ConfigFilePath => Path.Combine(ConfigDirectory, "config.json");

    public static Config? Load()
    {
        if (!File.Exists(ConfigFilePath))
            return null;

        var json = File.ReadAllText(ConfigFilePath);
        return JsonSerializer.Deserialize<Config>(json, JsonOptions);
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDirectory);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(ConfigFilePath, json);
    }

    public static void Remove()
    {
        if (File.Exists(ConfigFilePath))
            File.Delete(ConfigFilePath);
    }
}
