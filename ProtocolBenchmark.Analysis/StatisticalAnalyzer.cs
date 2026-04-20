using MathNet.Numerics.Distributions;
using MathNet.Numerics.Statistics;
using ProtocolBenchmark.Models;

namespace ProtocolBenchmark.Analysis;

public class StatisticalAnalyzer
{
    // ── Özet istatistikler ────────────────────────────────────────────────────
    public ScenarioSummary Summarize(
        List<MeasurementRecord> records,
        ProtocolType protocol,
        ScenarioType scenario)
    {
        var ok  = records.Where(r => r.IsSuccess).ToList();
        var lat = ok.Select(r => r.LatencyMs).OrderBy(x => x).ToList();

        double elapsed = records.Count > 1
            ? Math.Max((records.Max(r => r.Timestamp) -
                        records.Min(r => r.Timestamp)).TotalSeconds, 1)
            : 1;

        return new ScenarioSummary
        {
            Protocol             = protocol,
            Scenario             = scenario,
            TotalMessages        = records.Count,
            SuccessCount         = ok.Count,
            ErrorCount           = records.Count(r => !r.IsSuccess),
            LostCount            = records.Count(r => r.IsMessageLost),
            AvgLatencyMs         = lat.Count > 0 ? lat.Average()           : 0,
            MinLatencyMs         = lat.Count > 0 ? lat.Min()               : 0,
            MaxLatencyMs         = lat.Count > 0 ? lat.Max()               : 0,
            P95LatencyMs         = lat.Count > 0 ? Percentile(lat, 0.95)   : 0,
            P99LatencyMs         = lat.Count > 0 ? Percentile(lat, 0.99)   : 0,
            StdDevMs             = lat.Count > 1 ? Statistics.StandardDeviation(lat) : 0,
            AvgTtfbMs            = ok.Count > 0 ? ok.Average(r => r.TtfbMs)         : 0,
            AvgConnectTimeMs     = ok.Count > 0 ? ok.Average(r => r.ConnectTimeMs)  : 0,
            ThroughputMsgPerSec  = ok.Count / elapsed,
            AvgBandwidthKBPerMsg = records.Count > 0
                                   ? records.Average(r => r.PayloadBytes) / 1024.0  : 0,
            ConnectionDropCount  = records.Count(r =>
                                   r.ErrorMessage?.Contains("closed",
                                       StringComparison.OrdinalIgnoreCase) == true ||
                                   r.ErrorMessage?.Contains("abort",
                                       StringComparison.OrdinalIgnoreCase) == true),
            AvgMemoryMB          = records.Count > 0
                                   ? records.Average(r => r.MemoryBytes) / 1024.0 / 1024.0 : 0,
            PeakMemoryMB         = records.Count > 0
                                   ? records.Max(r => r.MemoryBytes)     / 1024.0 / 1024.0 : 0,
            TestStarted          = records.Count > 0 ? records.Min(r => r.Timestamp) : DateTime.UtcNow,
            TestEnded            = records.Count > 0 ? records.Max(r => r.Timestamp) : DateTime.UtcNow,
        };
    }

    // ── Welch t-testi ─────────────────────────────────────────────────────────
    /// <summary>
    /// H0: İki protokolün gecikme ortalamaları eşittir.
    /// p &lt; 0.05 → H0 reddedilir (fark istatistiksel olarak anlamlı).
    /// Eşit varyans varsayılmaz (Welch versiyonu).
    /// </summary>
    public (double T, double PValue, double Df) WelchTTest(
        List<double> a, List<double> b)
    {
        if (a.Count < 2 || b.Count < 2) return (0, 1, 0);

        double meanA = a.Average(), meanB = b.Average();
        double varA  = Statistics.Variance(a),  varB = Statistics.Variance(b);
        int    nA    = a.Count,                  nB   = b.Count;

        double se = Math.Sqrt(varA / nA + varB / nB);
        if (se == 0) return (0, 1, 0);

        double t  = (meanA - meanB) / se;

        // Welch–Satterthwaite serbestlik derecesi
        double num = Math.Pow(varA / nA + varB / nB, 2);
        double den = Math.Pow(varA / nA, 2) / (nA - 1)
                   + Math.Pow(varB / nB, 2) / (nB - 1);
        double df  = den > 0 ? num / den : nA + nB - 2;

        double p = 2.0 * (1.0 - StudentT.CDF(0, 1, df, Math.Abs(t)));
        return (t, p, df);
    }

    // ── Cohen's d ─────────────────────────────────────────────────────────────
    /// <summary>
    /// Etki büyüklüğü.
    /// |d| &lt; 0.2 küçük | 0.2–0.5 orta | 0.5–0.8 büyük | &gt; 0.8 çok büyük
    /// </summary>
    public double CohensD(List<double> a, List<double> b)
    {
        if (a.Count < 2 || b.Count < 2) return 0;

        double sdA = Statistics.StandardDeviation(a);
        double sdB = Statistics.StandardDeviation(b);
        int    nA  = a.Count, nB = b.Count;

        double pooled = Math.Sqrt(
            ((nA - 1) * sdA * sdA + (nB - 1) * sdB * sdB) / (nA + nB - 2));

        return pooled == 0 ? 0 : (a.Average() - b.Average()) / pooled;
    }

    // ── IQR aykırı değer (Tukey yöntemi) ─────────────────────────────────────
    /// <summary>
    /// Q1 − 1.5·IQR ve Q3 + 1.5·IQR sınırları dışındaki değerler aykırı.
    /// </summary>
    public (List<double> Outliers, double Q1, double Q3, double IQR) IqrOutliers(
        List<double> values)
    {
        if (values.Count < 4) return (new(), 0, 0, 0);

        var sorted = values.OrderBy(x => x).ToList();
        double q1  = Percentile(sorted, 0.25);
        double q3  = Percentile(sorted, 0.75);
        double iqr = q3 - q1;
        double lo  = q1 - 1.5 * iqr;
        double hi  = q3 + 1.5 * iqr;

        return (sorted.Where(v => v < lo || v > hi).ToList(), q1, q3, iqr);
    }

    // ── Hipotez değerlendirmesi ───────────────────────────────────────────────
    public HypothesisResult EvaluateHypothesis(
        string label,
        List<double> groupA, string nameA,
        List<double> groupB, string nameB,
        bool expectALower = true)
    {
        var (t, p, df) = WelchTTest(groupA, groupB);
        double d       = CohensD(groupA, groupB);
        bool   sig     = p < 0.05;
        double meanA   = groupA.Count > 0 ? groupA.Average() : 0;
        double meanB   = groupB.Count > 0 ? groupB.Average() : 0;

        // Hipotez yönü doğru mu?
        bool directionCorrect = expectALower
            ? meanA < meanB
            : meanA > meanB;

        string effectSize = Math.Abs(d) switch
        {
            < 0.2 => "Küçük",
            < 0.5 => "Orta",
            < 0.8 => "Büyük",
            _     => "Çok Büyük"
        };

        string verdict = (sig && directionCorrect)
            ? "✅ DESTEKLENDI"
            : (sig && !directionCorrect)
                ? "❌ REDDEDİLDİ (Ters yön)"
                : "⚠️ DESTEKLENMED (Anlamlı değil)";

        return new HypothesisResult
        {
            Label           = label,
            NameA           = nameA,
            NameB           = nameB,
            MeanA           = meanA,
            MeanB           = meanB,
            TStatistic      = t,
            PValue          = p,
            Df              = df,
            CohensD         = d,
            EffectSize      = effectSize,
            IsSignificant   = sig,
            DirectionCorrect= directionCorrect,
            Verdict         = verdict
        };
    }

    // ── Yardımcı ──────────────────────────────────────────────────────────────
    private static double Percentile(List<double> sorted, double p)
    {
        if (sorted.Count == 0) return 0;
        int idx = (int)Math.Ceiling(p * sorted.Count) - 1;
        return sorted[Math.Clamp(idx, 0, sorted.Count - 1)];
    }
}

// ── Hipotez sonuç modeli ──────────────────────────────────────────────────────
public class HypothesisResult
{
    public string Label            { get; set; } = "";
    public string NameA            { get; set; } = "";
    public string NameB            { get; set; } = "";
    public double MeanA            { get; set; }
    public double MeanB            { get; set; }
    public double TStatistic       { get; set; }
    public double PValue           { get; set; }
    public double Df               { get; set; }
    public double CohensD          { get; set; }
    public string EffectSize       { get; set; } = "";
    public bool   IsSignificant    { get; set; }
    public bool   DirectionCorrect { get; set; }
    public string Verdict          { get; set; } = "";
}
