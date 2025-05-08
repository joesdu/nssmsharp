using System.Text.Json;
using NssmSharp.Interop;

namespace NssmSharp.Core;

public class ConfigManager
{
    private const string ConfigDir = "configs";
    private readonly JsonSerializerOptions options = new() { WriteIndented = true };

    public ConfigManager()
    {
        if (!Directory.Exists(ConfigDir))
            Directory.CreateDirectory(ConfigDir);
    }

    public void SaveServiceConfig(NssmService config)
    {
        var file = Path.Combine(ConfigDir, config.Name + ".json");
        File.WriteAllText(file, JsonSerializer.Serialize(config, options));
    }

    public static NssmService? LoadServiceConfig(string serviceName)
    {
        var file = Path.Combine(ConfigDir, serviceName + ".json");
        return !File.Exists(file) ? null : JsonSerializer.Deserialize<NssmService>(File.ReadAllText(file));
    }

    public static void DeleteServiceConfig(string serviceName)
    {
        var file = Path.Combine(ConfigDir, serviceName + ".json");
        if (File.Exists(file)) File.Delete(file);
    }
}