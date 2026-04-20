using System.Diagnostics;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using ProtocolBenchmark.Models;

namespace ProtocolBenchmark.Client;

// ── Ortak arayüz ──────────────────────────────────────────────────────────────
public interface IProtocolClient : IAsyncDisposable
{
    Task<double> ConnectAsync();
    Task<MeasurementRecord> SendAsync(
        BenchmarkPayload payload,
        ProtocolType     protocol,
        ScenarioType     scenario);
}

// ── REST ──────────────────────────────────────────────────────────────────────
public sealed class RestClient : IProtocolClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public RestClient(string baseUrl)
    {
        var handler = new SocketsHttpHandler
        {
            // Bağlantıyı yeniden kullan — her istekte yeni TCP açma
            PooledConnectionLifetime    = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer     = 100,
        };
        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout     = TimeSpan.FromSeconds(15)
        };
    }

    public Task<double> ConnectAsync() => Task.FromResult(0.0);

    public async Task<MeasurementRecord> SendAsync(
        BenchmarkPayload payload, ProtocolType protocol, ScenarioType scenario)
    {
        var sw = Stopwatch.StartNew();
        bool success = false; string? err = null; long bytes = 0; double ttfb = 0;

        try
        {
            var json    = JsonSerializer.Serialize(payload, _json);
            bytes       = Encoding.UTF8.GetByteCount(json);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.PostAsync("/message", content);
            ttfb     = sw.Elapsed.TotalMilliseconds;
            resp.EnsureSuccessStatusCode();
            success  = true;
        }
        catch (Exception ex) { err = ex.Message; }
        finally { sw.Stop(); }

        return RecordFactory.Create(payload, protocol, scenario,
            sw.Elapsed.TotalMilliseconds, ttfb, 0, bytes, success, err);
    }

    public ValueTask DisposeAsync() { _http.Dispose(); return ValueTask.CompletedTask; }
}

// ── SSE ───────────────────────────────────────────────────────────────────────
public sealed class SseClient : IProtocolClient
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _json =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public SseClient(string baseUrl)
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime    = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer     = 100,
        };
        _http = new HttpClient(handler)
        {
            BaseAddress = new Uri(baseUrl),
            Timeout     = TimeSpan.FromSeconds(15)
        };
    }

    public Task<double> ConnectAsync() => Task.FromResult(0.0);

    public async Task<MeasurementRecord> SendAsync(
        BenchmarkPayload payload, ProtocolType protocol, ScenarioType scenario)
    {
        var sw = Stopwatch.StartNew();
        bool success = false; string? err = null; long bytes = 0; double ttfb = 0;

        try
        {
            var json    = JsonSerializer.Serialize(payload, _json);
            bytes       = Encoding.UTF8.GetByteCount(json);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.PostAsync("/stream", content);
            ttfb     = sw.Elapsed.TotalMilliseconds;
            resp.EnsureSuccessStatusCode();

            // SSE stream'den ilk "data:" satırını oku
            using var stream = await resp.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);
            var line  = await reader.ReadLineAsync();
            success   = line != null && line.StartsWith("data:");
        }
        catch (Exception ex) { err = ex.Message; }
        finally { sw.Stop(); }

        return RecordFactory.Create(payload, protocol, scenario,
            sw.Elapsed.TotalMilliseconds, ttfb, 0, bytes, success, err);
    }

    public ValueTask DisposeAsync() { _http.Dispose(); return ValueTask.CompletedTask; }
}

// ── WebSocket ─────────────────────────────────────────────────────────────────
public sealed class WsClient : IProtocolClient
{
    private readonly string _url;
    private ClientWebSocket? _ws;
    private double _connectTimeMs;
    private static readonly JsonSerializerOptions _json =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public WsClient(string wsUrl) => _url = wsUrl;

    public async Task<double> ConnectAsync()
    {
        var sw = Stopwatch.StartNew();
        _ws    = new ClientWebSocket();
        await _ws.ConnectAsync(new Uri(_url), CancellationToken.None);
        sw.Stop();
        _connectTimeMs = sw.Elapsed.TotalMilliseconds;
        return _connectTimeMs;
    }

    public async Task<MeasurementRecord> SendAsync(
        BenchmarkPayload payload, ProtocolType protocol, ScenarioType scenario)
    {
        var sw = Stopwatch.StartNew();
        bool success = false; string? err = null; long bytes = 0; double ttfb = 0;

        try
        {
            var json = JsonSerializer.Serialize(payload, _json);
            var data = Encoding.UTF8.GetBytes(json);
            bytes    = data.Length;

            await _ws!.SendAsync(
                new ArraySegment<byte>(data),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None);

            var buf    = new byte[8192];
            var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
            ttfb       = sw.Elapsed.TotalMilliseconds;
            success    = result.MessageType != WebSocketMessageType.Close;
        }
        catch (Exception ex) { err = ex.Message; }
        finally { sw.Stop(); }

        return RecordFactory.Create(payload, protocol, scenario,
            sw.Elapsed.TotalMilliseconds, ttfb, _connectTimeMs, bytes, success, err);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_ws?.State == WebSocketState.Open)
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Bitti", CancellationToken.None);
        }
        catch { /* Bağlantı zaten kapandıysa yoksay */ }
        finally { _ws?.Dispose(); }
    }
}

// ── MQTT ──────────────────────────────────────────────────────────────────────
public sealed class MqttBenchmarkClient : IProtocolClient
{
    private readonly string _host;
    private readonly int    _port;
    private IMqttClient?    _client;
    private double          _connectTimeMs;
    private string?         _clientId;

    private readonly Dictionary<int, TaskCompletionSource<bool>> _pending = new();
    private static readonly JsonSerializerOptions _json =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public MqttBenchmarkClient(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public async Task<double> ConnectAsync()
    {
        _clientId = $"client_{Guid.NewGuid():N}";
        var sw    = Stopwatch.StartNew();

        var factory = new MqttFactory();
        _client     = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_host, _port)
            .WithClientId(_clientId)
            .WithCleanSession(true)
            .Build();

        _client.ApplicationMessageReceivedAsync += args =>
        {
            try
            {
                var json     = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);
                var response = JsonSerializer.Deserialize<BenchmarkResponse>(json, _json);
                if (response != null && _pending.TryGetValue(response.SequenceNo, out var tcs))
                    tcs.TrySetResult(true);
            }
            catch { }
            return Task.CompletedTask;
        };

        await _client.ConnectAsync(options);
        sw.Stop();
        _connectTimeMs = sw.Elapsed.TotalMilliseconds;

        await _client.SubscribeAsync($"benchmark/response/{_clientId}");
        return _connectTimeMs;
    }

    public async Task<MeasurementRecord> SendAsync(
        BenchmarkPayload payload, ProtocolType protocol, ScenarioType scenario)
    {
        payload.ClientId = _clientId ?? payload.ClientId;
        var sw = Stopwatch.StartNew();
        bool success = false; string? err = null; double ttfb = 0; long payloadBytes = 0;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[payload.SequenceNo] = tcs;

        try
        {
            var json  = JsonSerializer.Serialize(payload, _json);
            var bytes = Encoding.UTF8.GetBytes(json);
            payloadBytes = bytes.Length;

            var message = new MqttApplicationMessageBuilder()
                .WithTopic("benchmark/request")
                .WithPayload(bytes)
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            await _client!.PublishAsync(message);

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(10_000));
            ttfb    = sw.Elapsed.TotalMilliseconds;
            success = completed == tcs.Task && tcs.Task.Result;
            if (!success) err = "Timeout: yanıt gelmedi";
        }
        catch (Exception ex) { err = ex.Message; }
        finally
        {
            _pending.Remove(payload.SequenceNo);
            sw.Stop();
        }

        return RecordFactory.Create(payload, protocol, scenario,
            sw.Elapsed.TotalMilliseconds, ttfb, _connectTimeMs, payloadBytes, success, err);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client?.IsConnected == true)
            await _client.DisconnectAsync();
        _client?.Dispose();
    }
}

// ── Kayıt fabrikası ───────────────────────────────────────────────────────────
public static class RecordFactory
{
    public static MeasurementRecord Create(
        BenchmarkPayload p, ProtocolType proto, ScenarioType sc,
        double latencyMs, double ttfbMs, double connectMs,
        long bytes, bool ok, string? err) => new()
    {
        Protocol      = proto,
        Scenario      = sc,
        ClientId      = p.ClientId,
        SequenceNo    = p.SequenceNo,
        LatencyMs     = latencyMs,
        TtfbMs        = ttfbMs,
        ConnectTimeMs = connectMs,
        PayloadBytes  = bytes,
        IsSuccess     = ok,
        IsMessageLost = !ok && err?.Contains("Timeout") == true,
        ErrorMessage  = err,
        Timestamp     = DateTime.UtcNow,
        MemoryBytes   = GC.GetTotalMemory(false),
        CpuPercent    = 0
    };
}
