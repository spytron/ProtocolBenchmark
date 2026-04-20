using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using ProtocolBenchmark.Models;
using Serilog;

namespace ProtocolBenchmark.Server;

/// <summary>
/// Mosquitto broker'a bağlanan MQTT sunucu servisi.
/// benchmark/request topic'ini dinler, yanıtı benchmark/response/{clientId} topic'ine gönderir.
/// </summary>
public class MqttResponder
{
    private readonly string _host;
    private readonly int    _port;
    private static readonly JsonSerializerOptions _jsonIn  =
        new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions _jsonOut =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public MqttResponder(string host = "localhost", int port = 1883)
    {
        _host = host;
        _port = port;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var factory = new MqttFactory();
        using var client = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_host, _port)
            .WithClientId("benchmark_server_responder")
            .WithCleanSession(true)
            .Build();

        // Gelen mesajları işle ve yanıt gönder
        client.ApplicationMessageReceivedAsync += async args =>
        {
            try
            {
                var json    = Encoding.UTF8.GetString(args.ApplicationMessage.PayloadSegment);
                var payload = JsonSerializer.Deserialize<BenchmarkPayload>(json, _jsonIn);
                if (payload == null) return;

                var response = new BenchmarkResponse
                {
                    ClientId    = payload.ClientId,
                    SequenceNo  = payload.SequenceNo,
                    ReceivedAt  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    FirstByteAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    Status      = "OK"
                };

                var responseTopic = $"benchmark/response/{payload.ClientId}";
                var responseJson  = JsonSerializer.Serialize(response, _jsonOut);

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic(responseTopic)
                    .WithPayload(responseJson)
                    .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
                    .Build();

                await client.PublishAsync(message, ct);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[MQTT Responder] Mesaj işleme hatası");
            }
        };

        // Bağlan
        await client.ConnectAsync(options, ct);
        Log.Information("[MQTT Responder] Mosquitto'ya bağlandı: {Host}:{Port}", _host, _port);

        // benchmark/request topic'ini dinle
        await client.SubscribeAsync(
            new MqttTopicFilterBuilder()
                .WithTopic("benchmark/request")
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtMostOnce)
                .Build(), ct);

        Log.Information("[MQTT Responder] benchmark/request dinleniyor...");

        // İptal edilene kadar bekle
        try { await Task.Delay(Timeout.Infinite, ct); }
        catch (OperationCanceledException) { }

        await client.DisconnectAsync();
        Log.Information("[MQTT Responder] Bağlantı kesildi.");
    }
}
