using PocketRoulette.Models;
using SPTarkov.DI.Annotations;

namespace PocketRoulette.Server;

// The loader and every router must share the initialized config and reload state.
[Injectable(InjectionType.Singleton)]
public class ConfigService
{
    private readonly object _gate = new();
    private string _path = string.Empty;
    private DateTime _lastWriteUtc;
    private PocketRouletteConfig? _current;

    public void Initialize(string path)
    {
        lock (_gate)
        {
            _path = path;
            try { Reload(); }
            catch (Exception ex)
            {
                _current = PocketRouletteConfig.CreateDefault();
                _current.ChancePercent = 0;
                _lastWriteUtc = WriteTime();
                Console.WriteLine($"[PocketRoulette] Invalid starting config; roulette is disabled until the config is fixed: {ex.Message}");
            }
        }
    }

    public PocketRouletteConfig Get()
    {
        lock (_gate)
        {
            if (_current == null)
                throw new InvalidOperationException("Pocket Roulette config has not loaded yet.");
            var writeTime = WriteTime();
            if (writeTime != _lastWriteUtc)
            {
                try { Reload(); }
                catch (Exception ex)
                {
                    _lastWriteUtc = writeTime;
                    Console.WriteLine($"[PocketRoulette] Invalid config edit; keeping previous values: {ex.Message}");
                }
            }
            return _current;
        }
    }

    public void Reload()
    {
        lock (_gate)
        {
            var next = ConfigFile.Load(_path, message => Console.WriteLine($"[PocketRoulette] Config: {message}"));
            _current = next;
            _lastWriteUtc = WriteTime();
        }
    }
    private DateTime WriteTime()
    {
        var source = ConfigFile.SourcePath(_path);
        return File.Exists(source) ? File.GetLastWriteTimeUtc(source) : DateTime.MinValue;
    }
}
