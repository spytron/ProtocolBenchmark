using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using ProtocolBenchmark.Models;
using Serilog;

namespace ProtocolBenchmark.Client;

public class TestRunner
{
    private readonly string _restUrl;
    private readonly string _sseUrl;
    private readonly string _wsUrl;
    private readonly string _mqttHost;
    private readonly int    _mqttPort;
    private readonly string _resultsDir;
    private readonly List<ScenarioSummary> _results = new();

    public TestRunner(
        string restUrl   = "http://localhost:5000",
        string sseUrl    = "http://localhost:5001",
        string wsUrl     = "ws://localhost:5002",
        string mqttHost  = "localhost",
        int    mqttPort  = 1883,
        string resultsDir = "results")
    {
        _restUrl    = restUrl;
        _sseUrl     = sseUrl;
        _wsUrl      = wsUrl;
        _mqttHost   = mqttHost;
        _mqttPort   = mqttPort;
        _resultsDir = resultsDir;
        Directory.CreateDirectory(_resultsDir);
    }

    // ── Tek senaryo koş ───────────────────────────────────────────────────────
    public async Task RunAsync(ProtocolType protocol, ScenarioType scenario)
    {
        var cfg = ScenarioRegistry.Get(scenario);
        var allRecords = new List<MeasurementRecord>();
        var sw = Stopwatch.StartNew();

        PrintScenarioHeader(protocol, scenario, cfg);

        try
        {
            if (cfg.IsSpike)
                await RunSpikeScenarioAsync(protocol, scenario, cfg, allRecords);
            else if (cfg.IsStress)
                await RunStressScenarioAsync(protocol, scenario, cfg, allRecords);
            else
                await RunNormalScenarioAsync(protocol, scenario, cfg, allRecords);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{P}|{S}] Senaryo hatası", protocol, scenario);
        }

        sw.Stop();

        var summary = Summarize(protocol, scenario, allRecords, sw.Elapsed);
        _results.Add(summary);

        PrintSummary(summary);
        SaveCsv(allRecords, protocol, scenario);
    }

    // ── Normal yük senaryosu ──────────────────────────────────────────────────
    private async Task RunNormalScenarioAsync(
        ProtocolType protocol, ScenarioType scenario,
        ScenarioConfig cfg, List<MeasurementRecord> allRecords)
    {
        var deadline = cfg.DurationSeconds > 0
            ? DateTime.UtcNow.AddSeconds(cfg.DurationSeconds)
            : DateTime.MaxValue;

        var tasks = Enumerable.Range(0, cfg.Clients).Select(i =>
            RunClientAsync(protocol, scenario, cfg, $"C{i:D4}", deadline, allRecords));

        await Task.WhenAll(tasks);
    }

    // ── Stres senaryosu (kırılma noktası) ─────────────────────────────────────
    private async Task RunStressScenarioAsync(
        ProtocolType protocol, ScenarioType scenario,
        ScenarioConfig cfg, List<MeasurementRecord> allRecords)
    {
        int currentClients = 50;
        int step           = 50;

        while (true)
        {
            Log.Information("[STRESS] {C} istemci koşuluyor...", currentClients);

            var batchRecords = new List<MeasurementRecord>();
            var deadline     = DateTime.UtcNow.AddSeconds(30);

            var tasks = Enumerable.Range(0, currentClients).Select(i =>
                RunClientAsync(protocol, scenario, cfg, $"C{i:D4}", deadline, batchRecords));

            await Task.WhenAll(tasks);
            lock (allRecords) allRecords.AddRange(batchRecords);

            // Kırılma noktası kontrolü: hata oranı %20 üzerindeyse dur
            var errorRate = batchRecords.Count > 0
                ? (double)batchRecords.Count(r => !r.IsSuccess) / batchRecords.Count * 100
                : 0;

            Log.Information("[STRESS] {C} istemci → Hata: {E:F1}%", currentClients, errorRate);

            if (errorRate > 20)
            {
                Log.Warning("[STRESS] Kırılma noktası: {C} istemcide hata oranı %{E:F1}",
                    currentClients, errorRate);
                break;
            }

            currentClients += step;

            // Maksimum 2000 istemci
            if (currentClients > 2000) break;

            await Task.Delay(2000); // batch arası bekleme
        }
    }

    // ── Spike senaryosu ───────────────────────────────────────────────────────
    private async Task RunSpikeScenarioAsync(
        ProtocolType protocol, ScenarioType scenario,
        ScenarioConfig cfg, List<MeasurementRecord> allRecords)
    {
        // Faz 1: Düşük yük (10 istemci, 30 sn)
        Log.Information("[SPIKE] Faz 1: {C} istemci (düşük yük)", cfg.Clients);
        var deadline1 = DateTime.UtcNow.AddSeconds(30);
        var tasks1 = Enumerable.Range(0, cfg.Clients).Select(i =>
            RunClientAsync(protocol, scenario, cfg, $"C{i:D4}", deadline1, allRecords));
        await Task.WhenAll(tasks1);

        // Faz 2: Spike (hedef istemci sayısına çık, 60 sn)
        Log.Information("[SPIKE] Faz 2: {C} istemci (spike!)", cfg.SpikeTargetClients);
        var deadline2 = DateTime.UtcNow.AddSeconds(60);
        var tasks2 = Enumerable.Range(0, cfg.SpikeTargetClients).Select(i =>
            RunClientAsync(protocol, scenario, cfg, $"S{i:D4}", deadline2, allRecords));
        await Task.WhenAll(tasks2);

        // Faz 3: Toparlanma (düşük yüke dön, 30 sn)
        Log.Information("[SPIKE] Faz 3: {C} istemci (toparlanma)", cfg.Clients);
        var deadline3 = DateTime.UtcNow.AddSeconds(30);
        var tasks3 = Enumerable.Range(0, cfg.Clients).Select(i =>
            RunClientAsync(protocol, scenario, cfg, $"R{i:D4}", deadline3, allRecords));
        await Task.WhenAll(tasks3);
    }

    // ── Tek istemci döngüsü ───────────────────────────────────────────────────
    private async Task RunClientAsync(
        ProtocolType protocol, ScenarioType scenario,
        ScenarioConfig cfg, string clientId,
        DateTime deadline, List<MeasurementRecord> allRecords)
    {
        IProtocolClient client = protocol switch
        {
            ProtocolType.REST      => new RestClient(_restUrl),
            ProtocolType.SSE       => new SseClient(_sseUrl),
            ProtocolType.WebSocket => new WsClient(_wsUrl),
            ProtocolType.MQTT      => new MqttBenchmarkClient(_mqttHost, _mqttPort),
            _                      => throw new ArgumentOutOfRangeException(nameof(protocol))
        };

        await using (client)
        {
            double connectTime = 0;
            try { connectTime = await client.ConnectAsync(); }
            catch (Exception ex)
            {
                Log.Warning(ex, "[{P}] {C} bağlanamadı", protocol, clientId);
                return;
            }

            int seq = 0;
            while (DateTime.UtcNow < deadline &&
                   seq < cfg.MessagesPerClient)
            {
                var payload = PayloadFactory.Create(clientId, seq, cfg);
                try
                {
                    var record = await client.SendAsync(payload, protocol, scenario);
                    record.ConnectTimeMs = connectTime;
                    lock (allRecords) allRecords.Add(record);
                }
                catch (Exception ex)
                {
                    lock (allRecords) allRecords.Add(new MeasurementRecord
                    {
                        Protocol     = protocol,
                        Scenario     = scenario,
                        ClientId     = clientId,
                        SequenceNo   = seq,
                        IsSuccess    = false,
                        ErrorMessage = ex.Message,
                        Timestamp    = DateTime.UtcNow,
                        MemoryBytes  = GC.GetTotalMemory(false)
                    });
                }

                seq++;
                if (cfg.DelayBetweenMsMs > 0)
                    await Task.Delay(cfg.DelayBetweenMsMs);
            }
        }
    }

    // ── Özet hesapla ──────────────────────────────────────────────────────────
    private static ScenarioSummary Summarize(
        ProtocolType protocol, ScenarioType scenario,
        List<MeasurementRecord> records, TimeSpan elapsed)
    {
        var ok  = records.Where(r => r.IsSuccess).ToList();
        var lat = ok.Select(r => r.LatencyMs).OrderBy(x => x).ToList();
        double dur = Math.Max(elapsed.TotalSeconds, 1);

        return new ScenarioSummary
        {
            Protocol             = protocol,
            Scenario             = scenario,
            TotalMessages        = records.Count,
            SuccessCount         = ok.Count,
            ErrorCount           = records.Count(r => !r.IsSuccess),
            LostCount            = records.Count(r => r.IsMessageLost),
            AvgLatencyMs         = lat.Count > 0 ? lat.Average()          : 0,
            MinLatencyMs         = lat.Count > 0 ? lat.Min()              : 0,
            MaxLatencyMs         = lat.Count > 0 ? lat.Max()              : 0,
            P95LatencyMs         = lat.Count > 0 ? Percentile(lat, 0.95)  : 0,
            P99LatencyMs         = lat.Count > 0 ? Percentile(lat, 0.99)  : 0,
            StdDevMs             = lat.Count > 1 ? StdDev(lat)            : 0,
            AvgTtfbMs            = ok.Count > 0 ? ok.Average(r => r.TtfbMs)           : 0,
            AvgConnectTimeMs     = ok.Count > 0 ? ok.Average(r => r.ConnectTimeMs)    : 0,
            ThroughputMsgPerSec  = ok.Count / dur,
            AvgBandwidthKBPerMsg = records.Count > 0
                                   ? records.Average(r => r.PayloadBytes) / 1024.0 : 0,
            ConnectionDropCount  = records.Count(r => r.ErrorMessage?.Contains("closed") == true ||
                                                      r.ErrorMessage?.Contains("abort")  == true),
            AvgMemoryMB          = records.Count > 0
                                   ? records.Average(r => r.MemoryBytes) / 1024.0 / 1024.0 : 0,
            PeakMemoryMB         = records.Count > 0
                                   ? records.Max(r => r.MemoryBytes)     / 1024.0 / 1024.0 : 0,
            TestStarted          = DateTime.UtcNow - elapsed,
            TestEnded            = DateTime.UtcNow
        };
    }

    // ── CSV kaydet ────────────────────────────────────────────────────────────
    private void SaveCsv(List<MeasurementRecord> records,
                         ProtocolType protocol, ScenarioType scenario)
    {
        var path = Path.Combine(_resultsDir, $"{protocol}_{scenario}.csv");
        try
        {
            // FileShare.ReadWrite → Excel gibi programlar açıksa bile yazar
            using var fs     = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            using var writer = new StreamWriter(fs);
            using var csv    = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteRecords(records);
            Log.Information("CSV kaydedildi → {Path}", path);
        }
        catch (IOException)
        {
            // Dosya hala kilitliyse zaman damgalı isimle kaydet
            var alt = Path.Combine(_resultsDir, $"{protocol}_{scenario}_{DateTime.Now:HHmmss}.csv");
            Log.Warning("Dosya kilitli, alternatif kaydediliyor → {Alt}", alt);
            using var writer = new StreamWriter(alt);
            using var csv    = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteRecords(records);
        }
    }

    // ── Karşılaştırma tablosu ─────────────────────────────────────────────────
    public void PrintComparison()
    {
        if (!_results.Any()) return;
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════╦══════════╦══════════╦══════════╦══════════╦════════════╗");
        Console.WriteLine("║ Protokol     ║ Ort.(ms) ║ P95 (ms) ║ TTFB(ms) ║  msg/s   ║   Hata %   ║");
        Console.WriteLine("╠══════════════╬══════════╬══════════╬══════════╬══════════╬════════════╣");
        Console.ResetColor();
        foreach (var r in _results)
            Console.WriteLine(
                $"║ {r.Protocol,-12} ║ {r.AvgLatencyMs,8:F2} ║ " +
                $"{r.P95LatencyMs,8:F2} ║ {r.AvgTtfbMs,8:F2} ║ " +
                $"{r.ThroughputMsgPerSec,8:F1} ║ {r.ErrorRatePercent,9:F2}% ║");
        Console.WriteLine("╚══════════════╩══════════╩══════════╩══════════╩══════════╩════════════╝");
    }

    // ── Yardımcı ──────────────────────────────────────────────────────────────
    private static void PrintScenarioHeader(ProtocolType p, ScenarioType s, ScenarioConfig cfg)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n  ▶ [{p}] {s} başlıyor...");
        Console.WriteLine($"    {ScenarioRegistry.Describe(s)}");
        Console.ResetColor();
    }

    private static void PrintSummary(ScenarioSummary s)
    {
        var ok  = s.SuccessCount;
        var tot = s.TotalMessages;
        Console.ForegroundColor = s.ErrorRatePercent > 5
            ? ConsoleColor.Red : ConsoleColor.Green;
        Console.WriteLine(
            $"  ✓ [{s.Protocol}|{s.Scenario}] " +
            $"Ort:{s.AvgLatencyMs:F2}ms P95:{s.P95LatencyMs:F2}ms " +
            $"Tp:{s.ThroughputMsgPerSec:F1}msg/s " +
            $"Başarı:{ok}/{tot} Hata:{s.ErrorRatePercent:F1}%");
        Console.ResetColor();
    }

    private static double Percentile(List<double> sorted, double p)
    {
        if (sorted.Count == 0) return 0;
        int idx = (int)Math.Ceiling(p * sorted.Count) - 1;
        return sorted[Math.Clamp(idx, 0, sorted.Count - 1)];
    }

    private static double StdDev(List<double> v)
    {
        double avg = v.Average();
        return Math.Sqrt(v.Sum(x => Math.Pow(x - avg, 2)) / v.Count);
    }
}
