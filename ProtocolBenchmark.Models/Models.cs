namespace ProtocolBenchmark.Models;

// ── Protokoller ───────────────────────────────────────────────────────────────
public enum ProtocolType
{
    REST,
    SSE,
    WebSocket,
    MQTT
}

// ── Test senaryoları ──────────────────────────────────────────────────────────
public enum ScenarioType
{
    S1_Smoke,           // 1 istemci, 30 sn   — sistem doğrulama
    S2_LightLoad,       // 10 istemci, 2 dk   — düşük yük
    S3_NormalLoad,      // 50 istemci, 5 dk   — tipik yük
    S4_HeavyLoad,       // 200 istemci, 5 dk  — yüksek yük
    S5_Stress,          // 500→∞, çökene dek  — kırılma noktası
    S6_Spike,           // 10→500→10, 3 dk    — ani yük
    S7_Endurance,       // 50 istemci, 30 dk  — dayanıklılık
    S8_SmallPayload,    // 50 istemci, 100B   — küçük mesaj
    S9_LargePayload     // 50 istemci, 100KB  — büyük mesaj
}

// ── Ortak mesaj modeli (tüm protokoller bu veriyi taşır) ──────────────────────
public class BenchmarkPayload
{
    public string ClientId   { get; set; } = string.Empty;
    public int    SequenceNo { get; set; }
    public long   SentAt     { get; set; }   // Unix ms — istemci gönderim zamanı
    public string Data       { get; set; } = string.Empty;
}

public class BenchmarkResponse
{
    public string ClientId    { get; set; } = string.Empty;
    public int    SequenceNo  { get; set; }
    public long   ReceivedAt  { get; set; }  // Unix ms — sunucu alım zamanı
    public long   FirstByteAt { get; set; }  // Unix ms — TTFB için
    public string Status      { get; set; } = "OK";
}

// ── Ham ölçüm kaydı (her mesaj için bir kayıt) ────────────────────────────────
public class MeasurementRecord
{
    public int          Id                    { get; set; }
    public ProtocolType Protocol              { get; set; }
    public ScenarioType Scenario             { get; set; }
    public string       ClientId             { get; set; } = string.Empty;
    public int          SequenceNo           { get; set; }

    // Performans metrikleri
    public double       LatencyMs            { get; set; }  // round-trip
    public double       TtfbMs               { get; set; }  // time to first byte
    public double       ConnectTimeMs        { get; set; }  // bağlantı kurulum süresi
    public long         PayloadBytes         { get; set; }  // mesaj boyutu (byte)

    // Güvenilirlik metrikleri
    public bool         IsSuccess            { get; set; }
    public bool         IsMessageLost        { get; set; }  // timeout ile kayıp
    public string?      ErrorMessage         { get; set; }

    // Kaynak metrikleri
    public long         MemoryBytes          { get; set; }  // GC.GetTotalMemory
    public double       CpuPercent           { get; set; }  // opsiyonel

    public DateTime     Timestamp            { get; set; }
}

// ── Senaryo özet istatistikleri ───────────────────────────────────────────────
public class ScenarioSummary
{
    public int          Id                   { get; set; }
    public ProtocolType Protocol             { get; set; }
    public ScenarioType Scenario             { get; set; }

    // Mesaj istatistikleri
    public int          TotalMessages        { get; set; }
    public int          SuccessCount         { get; set; }
    public int          ErrorCount           { get; set; }
    public int          LostCount            { get; set; }
    public double       ErrorRatePercent     => TotalMessages > 0
                                               ? (double)ErrorCount / TotalMessages * 100 : 0;
    public double       LossRatePercent      => TotalMessages > 0
                                               ? (double)LostCount / TotalMessages * 100 : 0;

    // Gecikme istatistikleri
    public double       AvgLatencyMs         { get; set; }
    public double       MinLatencyMs         { get; set; }
    public double       MaxLatencyMs         { get; set; }
    public double       P95LatencyMs         { get; set; }
    public double       P99LatencyMs         { get; set; }
    public double       StdDevMs             { get; set; }

    // Ek performans metrikleri
    public double       AvgTtfbMs            { get; set; }
    public double       AvgConnectTimeMs     { get; set; }
    public double       ThroughputMsgPerSec  { get; set; }
    public double       AvgBandwidthKBPerMsg { get; set; }

    // Bağlantı kararlılığı
    public int          ConnectionDropCount  { get; set; }

    // Kaynak kullanımı
    public double       AvgMemoryMB          { get; set; }
    public double       PeakMemoryMB         { get; set; }

    // Zaman
    public DateTime     TestStarted          { get; set; }
    public DateTime     TestEnded            { get; set; }
    public double       TotalDurationSec     => (TestEnded - TestStarted).TotalSeconds;
}

// ── Checkpoint modeli ─────────────────────────────────────────────────────────
public class CheckpointData
{
    public string            SessionId    { get; set; } = string.Empty;
    public DateTime          StartedAt    { get; set; }
    public DateTime          UpdatedAt    { get; set; }
    public List<string>      Completed    { get; set; } = new();  // "REST_S1_Smoke" gibi
    public List<string>      Remaining    { get; set; } = new();
    public string?           CurrentTask  { get; set; }           // şu an koşulan

    // Kolay erişim
    public bool IsCompleted(ProtocolType p, ScenarioType s)
        => Completed.Contains($"{p}_{s}");

    public void MarkCompleted(ProtocolType p, ScenarioType s)
    {
        var key = $"{p}_{s}";
        if (!Completed.Contains(key)) Completed.Add(key);
        Remaining.Remove(key);
        UpdatedAt = DateTime.Now;
    }
}

// ── Senaryo konfigürasyonu ────────────────────────────────────────────────────
public record ScenarioConfig(
    int     Clients,
    int     MessagesPerClient,
    int     DelayBetweenMsMs,
    int     DurationSeconds,    // 0 = mesaj sayısıyla belirlenir
    int     PayloadSizeBytes,
    bool    IsStress,           // kırılma noktası aranıyor mu?
    bool    IsSpike,            // ani yük var mı?
    int     SpikeTargetClients  // spike hedef istemci sayısı (0 = yok)
);
