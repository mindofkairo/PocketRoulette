using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using PocketRoulette.Models;

namespace PocketRoulette.Server;

public static class CommentedConfig
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    public static readonly JsonSerializerOptions ReadOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true
    };
    public static string Template()
    {
        using var stream = typeof(PocketRouletteConfig).Assembly.GetManifestResourceStream("PocketRoulette.DefaultConfig.json")!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static JsonObject Parse(string text) => JsonNode.Parse(text, documentOptions: new JsonDocumentOptions
    { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject
        ?? throw new JsonException("Config must be a JSON object.");

    public static string Render(JsonObject root)
    {
        var comments = new Dictionary<string, string>();
        var preceding = new List<string>();
        foreach (var line in Template().Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("//")) { preceding.Add(trimmed); continue; }
            var match = Regex.Match(trimmed, "^\"([^\"]+)\"\\s*:");
            if (match.Success && preceding.Count > 0) comments[match.Groups[1].Value] = string.Join("\n", preceding);
            preceding.Clear();
        }
        var order = Parse(Template());
        var builder = new StringBuilder();
        WriteObject(root, order, builder, 0, comments);
        return builder.ToString() + Environment.NewLine;
    }

    private static void WriteObject(JsonObject value, JsonObject? order, StringBuilder text, int level, Dictionary<string, string> comments)
    {
        text.AppendLine("{");
        var keys = (order?.Select(field => field.Key) ?? []).Where(value.ContainsKey)
            .Concat(value.Select(field => field.Key)).Distinct().ToList();
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            var pad = new string(' ', (level + 1) * 2);
            if (comments.TryGetValue(key, out var comment))
                foreach (var line in comment.Split('\n')) text.Append(pad).AppendLine(line);
            text.Append(pad).Append(JsonSerializer.Serialize(key, WriteOptions)).Append(": ");
            if (value[key] is JsonObject nested)
                WriteObject(nested, order?[key] as JsonObject, text, level + 1, comments);
            else
            {
                var lines = (value[key]?.ToJsonString(WriteOptions) ?? "null").Split('\n');
                text.Append(lines[0].TrimEnd('\r'));
                foreach (var line in lines.Skip(1)) text.AppendLine().Append(pad).Append(line.TrimEnd('\r'));
            }
            if (i < keys.Count - 1) text.Append(',');
            text.AppendLine();
        }
        text.Append(new string(' ', level * 2)).Append('}');
    }
}
