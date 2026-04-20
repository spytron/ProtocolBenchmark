using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using ProtocolBenchmark.Models;

namespace ProtocolBenchmark.Analysis;

// ── CSV yükleyici ─────────────────────────────────────────────────────────────
public static class CsvLoader
{
    public static List<MeasurementRecord> LoadAll(string dir)
    {
        var all    = new List<MeasurementRecord>();
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord   = true,
            MissingFieldFound = null,
            HeaderValidated   = null,
        };

        foreach (var file in Directory.GetFiles(dir, "*.csv"))
        {
            try
            {
                using var reader = new StreamReader(file);
                using var csv    = new CsvReader(reader, config);
                var records = csv.GetRecords<MeasurementRecord>().ToList();
                all.AddRange(records);
                Console.WriteLine($"  ✅ {Path.GetFileName(file),35} → {records.Count,6} kayıt");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠  {Path.GetFileName(file),35} → Atlandı: {ex.Message}");
            }
        }
        return all;
    }
}

// ── Rapor yazıcı ──────────────────────────────────────────────────────────────
public static class ReportWriter
{
    public static void WriteSummaryCsv(List<ScenarioSummary> summaries, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var w   = new StreamWriter(path);
        using var csv = new CsvWriter(w, CultureInfo.InvariantCulture);
        csv.WriteRecords(summaries);
        Console.WriteLine($"✅ Özet CSV → {path}");
    }

    public static void WriteHypothesisCsv(List<HypothesisResult> results, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var w   = new StreamWriter(path);
        using var csv = new CsvWriter(w, CultureInfo.InvariantCulture);
        csv.WriteRecords(results);
        Console.WriteLine($"✅ Hipotez CSV → {path}");
    }

    public static void WriteTextReport(
        List<ScenarioSummary>  summaries,
        List<HypothesisResult> hypotheses,
        string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var w = new StreamWriter(path);

        void Sep(char c = '═', int n = 76) => w.WriteLine(new string(c, n));
        void Line(char c = '─', int n = 76) => w.WriteLine(new string(c, n));

        Sep();
        w.WriteLine("  PROTOCOL BENCHMARK V3 – KAPSAMLI SONUÇ RAPORU");
        w.WriteLine($"  REST • SSE • WebSocket • MQTT");
        w.WriteLine($"  Oluşturulma: {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
        Sep();
        w.WriteLine();

        // ── Senaryo bazlı tablo ───────────────────────────────────────────────
        foreach (var sc in summaries.Select(s => s.Scenario).Distinct().OrderBy(x => x))
        {
            w.WriteLine($"  SENARYO: {sc}");
            Line();
            w.WriteLine($"  {"Protokol",-12} {"Ort(ms)",8} {"P95(ms)",8} {"P99(ms)",8} " +
                        $"{"TTFB(ms)",8} {"Bağ(ms)",8} {"msg/s",8} {"Hata%",7} {"Kayıp%",7} {"RAM(MB)",8}");
            Line('·');

            foreach (var s in summaries.Where(x => x.Scenario == sc))
                w.WriteLine(
                    $"  {s.Protocol,-12} {s.AvgLatencyMs,8:F2} {s.P95LatencyMs,8:F2} " +
                    $"{s.P99LatencyMs,8:F2} {s.AvgTtfbMs,8:F2} {s.AvgConnectTimeMs,8:F2} " +
                    $"{s.ThroughputMsgPerSec,8:F1} {s.ErrorRatePercent,7:F2} " +
                    $"{s.LossRatePercent,7:F2} {s.AvgMemoryMB,8:F2}");
            w.WriteLine();
        }

        // ── Hipotez değerlendirme ─────────────────────────────────────────────
        if (hypotheses.Any())
        {
            Sep();
            w.WriteLine("  HİPOTEZ DEĞERLENDİRME SONUÇLARI");
            Sep();
            w.WriteLine();

            foreach (var h in hypotheses)
            {
                w.WriteLine($"  {h.Label}");
                w.WriteLine($"    {h.NameA}: Ort={h.MeanA:F2}ms  vs  {h.NameB}: Ort={h.MeanB:F2}ms");
                w.WriteLine($"    t={h.TStatistic:F3}  df={h.Df:F0}  p={h.PValue:F4}  " +
                            $"Cohen's d={h.CohensD:F3} ({h.EffectSize} etki)");
                w.WriteLine($"    → {h.Verdict}");
                w.WriteLine();
            }
        }

        // ── Genel sıralama ────────────────────────────────────────────────────
        Sep();
        w.WriteLine("  GENEL PROTOKOL SIRALAMALARI");
        Sep();
        w.WriteLine();

        var ranked = summaries
            .GroupBy(s => s.Protocol)
            .Select(g => new
            {
                Protocol   = g.Key,
                AvgLatency = g.Average(x => x.AvgLatencyMs),
                AvgTp      = g.Average(x => x.ThroughputMsgPerSec),
                AvgErr     = g.Average(x => x.ErrorRatePercent),
                AvgMem     = g.Average(x => x.AvgMemoryMB)
            })
            .OrderBy(x => x.AvgLatency)
            .ToList();

        w.WriteLine("  Gecikme sırası (düşük = iyi):");
        for (int i = 0; i < ranked.Count; i++)
        {
            var r = ranked[i];
            w.WriteLine($"    {i + 1}. {r.Protocol,-12} " +
                        $"Ort:{r.AvgLatency:F2}ms  Tp:{r.AvgTp:F1}msg/s  " +
                        $"Hata:{r.AvgErr:F2}%  RAM:{r.AvgMem:F1}MB");
        }

        w.WriteLine();
        w.WriteLine("  Kullanım senaryosuna göre tavsiye:");
        w.WriteLine("    • Basit istek-yanıt           → REST");
        w.WriteLine("    • Sunucudan canlı bildirim     → SSE");
        w.WriteLine("    • Çift yönlü gerçek zamanlı    → WebSocket");
        w.WriteLine("    • Yüksek hacimli mesajlaşma    → MQTT");
        w.WriteLine();
        w.WriteLine($"  Rapor sonu – {DateTime.Now:dd.MM.yyyy HH:mm:ss}");

        Console.WriteLine($"✅ Metin raporu → {path}");
    }
}
