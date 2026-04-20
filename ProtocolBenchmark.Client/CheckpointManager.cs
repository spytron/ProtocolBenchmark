using System.Text.Json;
using ProtocolBenchmark.Models;

namespace ProtocolBenchmark.Client;

/// <summary>
/// Test ilerlemesini diske kaydeder.
/// Yazılım çökerse kaldığı yerden devam edilebilir.
/// </summary>
public class CheckpointManager
{
    private readonly string _path;
    private CheckpointData _data;
    private static readonly JsonSerializerOptions _json =
        new() { WriteIndented = true };

    public CheckpointManager(string resultsDir = "results")
    {
        Directory.CreateDirectory(resultsDir);
        _path = Path.Combine(resultsDir, "checkpoint.json");
        _data = Load() ?? CreateNew();
    }

    public bool HasPreviousSession => File.Exists(_path) &&
                                      _data.Completed.Count > 0;

    public CheckpointData Data => _data;

    /// <summary>Belirli bir senaryo daha önce tamamlandı mı?</summary>
    public bool IsCompleted(ProtocolType protocol, ScenarioType scenario)
        => _data.IsCompleted(protocol, scenario);

    /// <summary>Senaryo tamamlandığında çağrılır.</summary>
    public void MarkCompleted(ProtocolType protocol, ScenarioType scenario)
    {
        _data.MarkCompleted(protocol, scenario);
        _data.CurrentTask = null;
        Save();
    }

    /// <summary>Senaryo başlarken çağrılır.</summary>
    public void MarkStarted(ProtocolType protocol, ScenarioType scenario)
    {
        _data.CurrentTask = $"{protocol}_{scenario}";
        _data.UpdatedAt   = DateTime.Now;
        Save();
    }

    /// <summary>Yeni oturum başlat — eski checkpoint silinir.</summary>
    public void Reset()
    {
        _data = CreateNew();
        Save();
    }

    /// <summary>Tüm senaryo/protokol kombinasyonlarını hesapla.</summary>
    public List<(ProtocolType Protocol, ScenarioType Scenario)> GetAllTasks()
    {
        var tasks = new List<(ProtocolType, ScenarioType)>();
        foreach (var proto in Enum.GetValues<ProtocolType>())
            foreach (var scenario in Enum.GetValues<ScenarioType>())
                tasks.Add((proto, scenario));
        return tasks;
    }

    /// <summary>Kalan (tamamlanmamış) görevleri döndür.</summary>
    public List<(ProtocolType Protocol, ScenarioType Scenario)> GetRemainingTasks()
        => GetAllTasks()
            .Where(t => !IsCompleted(t.Protocol, t.Scenario))
            .ToList();

    /// <summary>İlerleme yüzdesi.</summary>
    public double ProgressPercent
    {
        get
        {
            int total = Enum.GetValues<ProtocolType>().Length *
                        Enum.GetValues<ScenarioType>().Length;
            return total > 0 ? (double)_data.Completed.Count / total * 100 : 0;
        }
    }

    public void PrintStatus()
    {
        int total     = Enum.GetValues<ProtocolType>().Length *
                        Enum.GetValues<ScenarioType>().Length;
        int completed = _data.Completed.Count;
        int remaining = total - completed;

        Console.WriteLine($"  Oturum ID     : {_data.SessionId}");
        Console.WriteLine($"  Başlangıç     : {_data.StartedAt:dd.MM.yyyy HH:mm}");
        Console.WriteLine($"  Son güncelleme: {_data.UpdatedAt:dd.MM.yyyy HH:mm}");
        Console.WriteLine($"  Tamamlanan    : {completed}/{total} senaryo ({ProgressPercent:F0}%)");
        Console.WriteLine($"  Kalan         : {remaining} senaryo");
        if (_data.CurrentTask != null)
            Console.WriteLine($"  Yarıda kalan  : {_data.CurrentTask} (tekrar koşulacak)");
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private CheckpointData CreateNew() => new()
    {
        SessionId = DateTime.Now.ToString("yyyy-MM-dd_HH-mm"),
        StartedAt = DateTime.Now,
        UpdatedAt = DateTime.Now,
        Completed = new List<string>(),
        Remaining = GetAllTasks()
            .Select(t => $"{t.Protocol}_{t.Scenario}")
            .ToList()
    };

    private CheckpointData? Load()
    {
        if (!File.Exists(_path)) return null;
        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<CheckpointData>(json, _json);
        }
        catch { return null; }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_data, _json);
        File.WriteAllText(_path, json);
    }
}
