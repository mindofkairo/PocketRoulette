using System.Text.Json;

namespace PocketRoulette.Server;

public sealed class FeatureStore
{
#if POCKET_ROULETTE_TESTING
    private bool _memoryOnly;
    public bool IsTesting => _memoryOnly;
    public static FeatureStore ForTesting() => new() { _memoryOnly = true };
#endif
    private string _path = "";
    public FeatureState State { get; private set; } = new();
    public bool SaveNeedsRetry { get; private set; }
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public void Initialize(string path)
    {
        _path = path;
        if (!File.Exists(path)) { State = new(); return; }
        try { State = Read(path); }
        catch (Exception original) when (original is JsonException or InvalidDataException)
        {
            if (!File.Exists(path + ".bak")) throw new InvalidDataException("Luck/payment state is unreadable; restore its backup before rolling.", original);
            State = Read(path + ".bak");
            File.Copy(path, path + ".corrupt", true);
            File.Copy(path + ".bak", path, true);
            Console.WriteLine("[PocketRoulette] Recovered luck/payment state from backup; damaged file preserved as .corrupt.");
        }
    }

    private static FeatureState Read(string path)
    {
        var state = JsonSerializer.Deserialize<FeatureState>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty luck state.");
        if (state.Version != 1 || state.Players == null || state.Raids == null)
            throw new InvalidDataException("Unsupported luck state version or missing ledgers.");
        return state;
    }

    public void Save()
    {
#if POCKET_ROULETTE_TESTING
        if (_memoryOnly) return;
#endif
        SaveNeedsRetry = true;
        if (string.IsNullOrWhiteSpace(_path)) throw new InvalidOperationException("Feature store is not initialized.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_path))!);
        var temporary = _path + ".tmp";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(State, Options);
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { stream.Write(bytes); stream.Flush(flushToDisk: true); }
        if (File.Exists(_path)) File.Copy(_path, _path + ".bak", true);
        File.Move(temporary, _path, overwrite: true);
        SaveNeedsRetry = false;
    }
}
