using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using PocketRoulette.Models;

namespace PocketRoulette.Server;

public static class ConfigFile
{
    public static string SourcePath(string path)
    {
        if (!path.EndsWith(".jsonc", StringComparison.OrdinalIgnoreCase)) return path;
        var legacy = path[..^1];
        if (!File.Exists(legacy) || File.Exists(path + ".legacy-imported")) return path;
        if (!File.Exists(path)) return legacy;
        try
        {
            // A stock file supplied by the new ZIP must not mask an older user's settings.
            return JsonNode.DeepEquals(CommentedConfig.Parse(File.ReadAllText(path)), CommentedConfig.Parse(CommentedConfig.Template()))
                ? legacy : path;
        }
        catch (JsonException) { return path; }
    }
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static PocketRouletteConfig Load(string path, Action<string>? warn = null)
    {
        var defaults = PocketRouletteConfig.CreateDefault();
        var sourcePath = SourcePath(path);
        var exists = File.Exists(sourcePath);
        var text = exists ? File.ReadAllText(sourcePath) : CommentedConfig.Template();
        var root = CommentedConfig.Parse(text);
        var defaultRoot = JsonSerializer.SerializeToNode(defaults)!.AsObject();
        var changed = !exists || sourcePath != path;
        changed |= FillMissing(root, defaultRoot);

        // Older pools did not carry stack ranges. Keep explicitly configured values.
        if (root["itemPool"] is JsonArray pool)
        {
            var defaultItems = defaults.ItemPool.ToDictionary(item => item.Tpl, StringComparer.OrdinalIgnoreCase);
            foreach (var entry in pool.OfType<JsonObject>())
            {
                var tpl = entry["tpl"]?.GetValue<string>() ?? string.Empty;
                defaultItems.TryGetValue(tpl, out var original);
                if (!entry.ContainsKey("minCount")) { entry["minCount"] = original?.MinCount ?? 1; changed = true; }
                if (!entry.ContainsKey("maxCount")) { entry["maxCount"] = original?.MaxCount ?? 1; changed = true; }
            }
        }

        var config = root.Deserialize<PocketRouletteConfig>(CommentedConfig.ReadOptions) ?? throw new JsonException("Config is empty.");
        ConfigRules.Normalize(config, warn);
        if (changed)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            if (exists)
                File.Copy(sourcePath, sourcePath + ".bak", overwrite: true);
            // Preserve user fields when adding new options; normalized values are served in memory.
            var temporaryPath = path + ".tmp";
            File.WriteAllText(temporaryPath, path.EndsWith(".jsonc", StringComparison.OrdinalIgnoreCase)
                ? CommentedConfig.Render(root) : root.ToJsonString(Options));
            File.Move(temporaryPath, path, overwrite: true);
            if (sourcePath != path) File.WriteAllText(path + ".legacy-imported", Path.GetFileName(sourcePath));
            warn?.Invoke(exists ? $"Added missing options; previous config saved as {Path.GetFileName(sourcePath)}.bak." : "Created default config.");
        }
        return config;
    }

    private static bool FillMissing(JsonObject target, JsonObject defaults)
    {
        var changed = false;
        foreach (var field in defaults)
        {
            if (!target.ContainsKey(field.Key)) { target[field.Key] = field.Value?.DeepClone(); changed = true; }
            else if (target[field.Key] is JsonObject nested && field.Value is JsonObject fallback)
                changed |= FillMissing(nested, fallback);
        }
        return changed;
    }
}
