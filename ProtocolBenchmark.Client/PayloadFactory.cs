using ProtocolBenchmark.Models;

namespace ProtocolBenchmark.Client;

public static class PayloadFactory
{
    // Sabit seed → her çalıştırmada aynı veri → tekrarlanabilir deney
    private static readonly Random _rnd = new Random(42);
    private static readonly object _lock = new();

    public static BenchmarkPayload Create(string clientId, int seq, ScenarioConfig cfg)
    {
        string data;
        lock (_lock)
        {
            // Payload boyutuna göre veri üret
            data = cfg.PayloadSizeBytes <= 512
                ? GenerateSmall(cfg.PayloadSizeBytes)
                : GenerateLarge(cfg.PayloadSizeBytes);
        }

        return new BenchmarkPayload
        {
            ClientId   = clientId,
            SequenceNo = seq,
            SentAt     = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data       = data
        };
    }

    private static string GenerateSmall(int targetBytes)
    {
        // JSON wrapper ~80 byte olduğu için veri kısmını küçük tut
        var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        int dataLen = Math.Max(10, targetBytes - 80);
        var buf = new char[dataLen];
        for (int i = 0; i < dataLen; i++)
            buf[i] = chars[_rnd.Next(chars.Length)];
        return new string(buf);
    }

    private static string GenerateLarge(int targetBytes)
    {
        // Büyük payload: tekrar eden pattern ile hızlı oluştur
        int dataLen = Math.Max(100, targetBytes - 80);
        return new string('X', dataLen);
    }
}
