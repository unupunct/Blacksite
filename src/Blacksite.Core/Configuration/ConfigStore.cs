using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Blacksite.Core.Configuration;

/// <summary>Versioned envelope written to BLACKSITE_CONFIG.json.</summary>
public sealed class ConfigEnvelope
{
    public string Format { get; set; } = ConfigStore.FormatName;
    public int Version { get; set; } = ConfigStore.CurrentVersion;
    public DateTime ExportedUtc { get; set; } = DateTime.UtcNow;
    public string? SourcePlatform { get; set; }
    public AppSettings Settings { get; set; } = new();
}

public sealed class PresetPackEnvelope
{
    public string Format { get; set; } = "BLACKSITE_PRESETS";
    public int Version { get; set; } = 1;
    public List<CustomPreset> Presets { get; set; } = new();
    public List<CinematicSequence> Sequences { get; set; } = new();
}

public sealed class ConfigStore
{
    public const string FormatName = "BLACKSITE_CONFIG";
    public const int CurrentVersion = 1;

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() },
    };

    readonly string _dir;
    public string SettingsPath => Path.Combine(_dir, "BLACKSITE_CONFIG.json");
    public string BackupPath => Path.Combine(_dir, "BLACKSITE_CONFIG.backup.json");

    public ConfigStore(string directory)
    {
        _dir = directory;
        Directory.CreateDirectory(_dir);
    }

    public AppSettings Load()
    {
        try { if (File.Exists(SettingsPath)) return Parse(File.ReadAllText(SettingsPath)); } catch { }
        try { if (File.Exists(BackupPath)) return Parse(File.ReadAllText(BackupPath)); } catch { }
        return new AppSettings();
    }

    public void Save(AppSettings s)
    {
        try
        {
            if (File.Exists(SettingsPath)) File.Copy(SettingsPath, BackupPath, true);
            var tmp = SettingsPath + ".tmp";
            File.WriteAllText(tmp, Serialize(s));
            File.Move(tmp, SettingsPath, true);
        }
        catch { /* never crash on persistence */ }
    }

    public static string Serialize(AppSettings s, string? platform = null) =>
        JsonSerializer.Serialize(new ConfigEnvelope { Settings = s, SourcePlatform = platform }, Json);

    /// <summary>Parses any known version and upgrades it to the current one.</summary>
    public static AppSettings Parse(string json)
    {
        var node = JsonNode.Parse(json) ?? throw new InvalidDataException("Empty config");
        var fmt = (node["format"] ?? node["Format"])?.GetValue<string>();
        if (fmt != null && fmt != FormatName) throw new InvalidDataException("Not a BLACKSITE config file");
        int ver = (node["version"] ?? node["Version"])?.GetValue<int>() ?? 1;
        if (ver > CurrentVersion)
            throw new InvalidDataException($"Config version {ver} is newer than this app supports ({CurrentVersion}).");
        // Future upgrades: while (ver < CurrentVersion) node = Migrate(node, ver++);
        var env = node.Deserialize<ConfigEnvelope>(Json) ?? throw new InvalidDataException("Bad config");
        var s = env.Settings ?? new AppSettings();
        if (s.Shortcuts.Count == 0) s.Shortcuts = AppSettings.DefaultShortcuts();
        foreach (var kv in AppSettings.DefaultShortcuts()) s.Shortcuts.TryAdd(kv.Key, kv.Value);
        return s;
    }

    public static string ExportPresets(AppSettings s) =>
        JsonSerializer.Serialize(new PresetPackEnvelope { Presets = s.CustomPresets, Sequences = s.Sequences }, Json);

    public static int ImportPresets(AppSettings s, string json)
    {
        var pack = JsonSerializer.Deserialize<PresetPackEnvelope>(json, Json) ?? throw new InvalidDataException("Bad preset pack");
        if (pack.Format != "BLACKSITE_PRESETS") throw new InvalidDataException("Not a BLACKSITE preset pack");
        int n = 0;
        foreach (var p in pack.Presets) { s.CustomPresets.RemoveAll(x => x.Id == p.Id); s.CustomPresets.Add(p); n++; }
        foreach (var q in pack.Sequences) { s.Sequences.RemoveAll(x => x.Name == q.Name); s.Sequences.Add(q); n++; }
        return n;
    }
}
