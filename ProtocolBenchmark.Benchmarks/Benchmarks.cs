using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using MQTTnet;
using MQTTnet.Client;
using ProtocolBenchmark.Models;

// ── Çalıştır ──────────────────────────────────────────────────────────────────
// ÖNEMLI: Yalnızca Release modunda çalıştırın!
// Komut: dotnet run -c Release

var config = DefaultConfig.Instance
    .AddExporter(CsvExporter.Default)
    .AddExporter(HtmlExporter.Default)
    .AddExporter(MarkdownExporter.GitHub)
    .AddColumn(StatisticColumn.P95)
    .AddColumn(StatisticColumn.P90)
    .AddColumn(StatisticColumn.StdDev);

BenchmarkRunner.Run<ProtocolBenchmarks>(config);

// ── Benchmark Sınıfı ──────────────────────────────────────────────────────────
[SimpleJob(RuntimeMoniker.Net80, warmupCount: 5, iterationCount: 20)]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class ProtocolBenchmarks
{
    private const string REST_URL  = "http://localhost:5000";
    private const string SSE_URL   = "http://localhost:5001";
    private const string WS_URL    = "ws://localhost:5002";
    private const string MQTT_HOST = "localhost";
    private const int    MQTT_PORT = 1883;

    private HttpClient   _http = null!;
    private IMqttClient  _mqttClient = null!;
    private byte[]       _payloadBytes = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _http = new HttpClient();

        var payload = new BenchmarkPayload
        {
            ClientId   = "BENCH_001",
            SequenceNo = 0,
            SentAt     = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data       = new string('X', 176)   // ~256 byte toplam
        };
        _payloadBytes = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(payload,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        // MQTT bağlantısı
        var factory = new MqttFactory();
        _mqttClient = factory.CreateMqttClient();
        var opts = new MqttClientOptionsBuilder()
            .WithTcpServer(MQTT_HOST, MQTT_PORT)
            .WithClientId("bench_client")
            .Build();
        await _mqttClient.ConnectAsync(opts);
        await _mqttClient.SubscribeAsync("benchmark/response/BENCH_001");
    }

    // ── REST ──────────────────────────────────────────────────────────────────
    [Benchmark(Baseline = true, Description = "REST – HTTP/1.1 + JSON")]
    public async Task Rest()
    {
        var content = new ByteArrayContent(_payloadBytes);
        content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        var resp = await _http.PostAsync(REST_URL + "/message", content);
        resp.EnsureSuccessStatusCode();
    }

    // ── SSE ───────────────────────────────────────────────────────────────────
    [Benchmark(Description = "SSE – HTTP streaming")]
    public async Task Sse()
    {
        var content = new ByteArrayContent(_payloadBytes);
        content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        var resp = await _http.PostAsync(SSE_URL + "/stream", content);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);
        await reader.ReadLineAsync(); // İlk SSE satırını oku
    }

    // ── WebSocket ─────────────────────────────────────────────────────────────
    [Benchmark(Description = "WebSocket – WS + JSON")]
    public async Task WebSocket()
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(WS_URL), CancellationToken.None);

        await ws.SendAsync(
            new ArraySegment<byte>(_payloadBytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None);

        var buf = new byte[4096];
        await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);

        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
    }

    // ── MQTT ──────────────────────────────────────────────────────────────────
    [Benchmark(Description = "MQTT – TCP + JSON (QoS 1)")]
    public async Task Mqtt()
    {
        var tcs = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _mqttClient.ApplicationMessageReceivedAsync += args =>
        {
            tcs.TrySetResult(true);
            return Task.CompletedTask;
        };

        var msg = new MqttApplicationMessageBuilder()
            .WithTopic("benchmark/request")
            .WithPayload(_payloadBytes)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        await _mqttClient.PublishAsync(msg);
        await Task.WhenAny(tcs.Task, Task.Delay(5000));
    }

    // ── Sadece serializasyon (ağ yok) ─────────────────────────────────────────
    [Benchmark(Description = "JSON Serializasyon (ağsız)")]
    public string JsonOnly()
    {
        var p = new BenchmarkPayload
        {
            ClientId = "X", SequenceNo = 0,
            SentAt   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data     = new string('X', 176)
        };
        return JsonSerializer.Serialize(p);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        _http.Dispose();
        if (_mqttClient.IsConnected)
            await _mqttClient.DisconnectAsync();
        _mqttClient.Dispose();
    }
}
