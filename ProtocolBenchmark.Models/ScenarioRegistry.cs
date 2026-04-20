namespace ProtocolBenchmark.Models;

/// <summary>
/// Tüm senaryoların merkezi konfigürasyon kaydı.
/// Hem Client hem Analysis bu sınıfı kullanır.
/// </summary>
public static class ScenarioRegistry
{
    public static readonly Dictionary<ScenarioType, ScenarioConfig> All = new()
    {
        [ScenarioType.S1_Smoke] = new ScenarioConfig(
            Clients:             1,
            MessagesPerClient:   10,
            DelayBetweenMsMs:    500,
            DurationSeconds:     30,
            PayloadSizeBytes:    256,
            IsStress:            false,
            IsSpike:             false,
            SpikeTargetClients:  0
        ),
        [ScenarioType.S2_LightLoad] = new ScenarioConfig(
            Clients:             10,
            MessagesPerClient:   50,
            DelayBetweenMsMs:    200,
            DurationSeconds:     120,
            PayloadSizeBytes:    256,
            IsStress:            false,
            IsSpike:             false,
            SpikeTargetClients:  0
        ),
        [ScenarioType.S3_NormalLoad] = new ScenarioConfig(
            Clients:             50,
            MessagesPerClient:   100,
            DelayBetweenMsMs:    100,
            DurationSeconds:     300,
            PayloadSizeBytes:    256,
            IsStress:            false,
            IsSpike:             false,
            SpikeTargetClients:  0
        ),
        [ScenarioType.S4_HeavyLoad] = new ScenarioConfig(
            Clients:             200,
            MessagesPerClient:   50,
            DelayBetweenMsMs:    50,
            DurationSeconds:     300,
            PayloadSizeBytes:    256,
            IsStress:            false,
            IsSpike:             false,
            SpikeTargetClients:  0
        ),
        [ScenarioType.S5_Stress] = new ScenarioConfig(
            Clients:             500,
            MessagesPerClient:   100,
            DelayBetweenMsMs:    0,
            DurationSeconds:     0,     // çökene kadar devam eder
            PayloadSizeBytes:    256,
            IsStress:            true,
            IsSpike:             false,
            SpikeTargetClients:  0
        ),
        [ScenarioType.S6_Spike] = new ScenarioConfig(
            Clients:             10,    // başlangıç istemcisi
            MessagesPerClient:   200,
            DelayBetweenMsMs:    50,
            DurationSeconds:     180,
            PayloadSizeBytes:    256,
            IsStress:            false,
            IsSpike:             true,
            SpikeTargetClients:  500    // spike anında bu kadar istemciye çıkar
        ),
        [ScenarioType.S7_Endurance] = new ScenarioConfig(
            Clients:             50,
            MessagesPerClient:   int.MaxValue,   // süre dolana kadar devam
            DelayBetweenMsMs:    100,
            DurationSeconds:     1800,           // 30 dakika
            PayloadSizeBytes:    256,
            IsStress:            false,
            IsSpike:             false,
            SpikeTargetClients:  0
        ),
        [ScenarioType.S8_SmallPayload] = new ScenarioConfig(
            Clients:             50,
            MessagesPerClient:   100,
            DelayBetweenMsMs:    100,
            DurationSeconds:     180,
            PayloadSizeBytes:    100,            // 100 Byte
            IsStress:            false,
            IsSpike:             false,
            SpikeTargetClients:  0
        ),
        [ScenarioType.S9_LargePayload] = new ScenarioConfig(
            Clients:             50,
            MessagesPerClient:   50,
            DelayBetweenMsMs:    200,
            DurationSeconds:     180,
            PayloadSizeBytes:    102_400,        // 100 KB
            IsStress:            false,
            IsSpike:             false,
            SpikeTargetClients:  0
        ),
    };

    public static ScenarioConfig Get(ScenarioType scenario) => All[scenario];

    public static string Describe(ScenarioType scenario)
    {
        var c = All[scenario];
        return scenario switch
        {
            ScenarioType.S1_Smoke        => $"1 istemci, 30 sn — sistem doğrulama",
            ScenarioType.S2_LightLoad    => $"10 istemci, 2 dk — düşük yük",
            ScenarioType.S3_NormalLoad   => $"50 istemci, 5 dk — tipik yük",
            ScenarioType.S4_HeavyLoad    => $"200 istemci, 5 dk — yüksek yük",
            ScenarioType.S5_Stress       => $"500+ istemci, çökene dek — stres",
            ScenarioType.S6_Spike        => $"10→500→10 istemci, 3 dk — ani yük",
            ScenarioType.S7_Endurance    => $"50 istemci, 30 dk — dayanıklılık",
            ScenarioType.S8_SmallPayload => $"50 istemci, 100B payload, 3 dk",
            ScenarioType.S9_LargePayload => $"50 istemci, 100KB payload, 3 dk",
            _                            => scenario.ToString()
        };
    }
}
