using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ProtocolBenchmark.Models;
using ProtocolBenchmark.Server;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/server-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

DatabaseHelper.Initialize("benchmark.db");

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ASP.NET Core request loglarını kapat
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.None);
builder.Logging.AddFilter("Microsoft.AspNetCore.Routing",              LogLevel.None);
builder.Logging.AddFilter("Microsoft.AspNetCore.StaticFiles",          LogLevel.None);

// Kestrel: REST(5000), SSE(5001), WebSocket(5002) aynı sunucuda
builder.WebHost.ConfigureKestrel(k =>
{
    k.ListenLocalhost(5000); // REST
    k.ListenLocalhost(5001); // SSE
    k.ListenLocalhost(5002); // WebSocket
});

var app = builder.Build();

// WebSocket middleware — built-in Kestrel WebSocket
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

// ── REST endpoint'leri (port 5000) ────────────────────────────────────────────
app.MapGet("/health", (HttpContext ctx) =>
    Results.Ok(new { status = "OK", protocol = "REST", port = ctx.Connection.LocalPort }));

app.MapPost("/message", async (HttpContext ctx) =>
{
    var payload  = await ctx.Request.ReadFromJsonAsync<BenchmarkPayload>();
    var response = new BenchmarkResponse
    {
        ClientId    = payload?.ClientId ?? "",
        SequenceNo  = payload?.SequenceNo ?? 0,
        ReceivedAt  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        FirstByteAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        Status      = "OK"
    };
    return Results.Ok(response);
});

// ── SSE endpoint'i (port 5001) ─────────────────────────────────────────────────
app.MapPost("/stream", async (HttpContext ctx) =>
{
    var payload  = await ctx.Request.ReadFromJsonAsync<BenchmarkPayload>();
    var response = new BenchmarkResponse
    {
        ClientId    = payload?.ClientId ?? "",
        SequenceNo  = payload?.SequenceNo ?? 0,
        ReceivedAt  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        FirstByteAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        Status      = "OK"
    };
    ctx.Response.ContentType = "text/event-stream";
    ctx.Response.Headers["Cache-Control"] = "no-cache";
    await ctx.Response.WriteAsync(
        $"data: {JsonSerializer.Serialize(response)}\n\n");
    await ctx.Response.Body.FlushAsync();
});

// ── WebSocket endpoint'i (port 5002) ───────────────────────────────────────────
app.Map("/ws", async (HttpContext ctx) =>
{
    if (!ctx.WebSockets.IsWebSocketRequest)
    {
        ctx.Response.StatusCode = 400;
        return;
    }

    using var ws  = await ctx.WebSockets.AcceptWebSocketAsync();
    var buf       = new byte[256 * 1024];

    while (ws.State == WebSocketState.Open)
    {
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;

        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buf), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close) break;
            ms.Write(buf, 0, result.Count);
        }
        while (!result.EndOfMessage);

        if (result.MessageType == WebSocketMessageType.Close)
        {
            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "OK", CancellationToken.None);
            break;
        }

        var json    = Encoding.UTF8.GetString(ms.ToArray());
        var payload = JsonSerializer.Deserialize<BenchmarkPayload>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var response = new BenchmarkResponse
        {
            ClientId    = payload?.ClientId ?? "",
            SequenceNo  = payload?.SequenceNo ?? 0,
            ReceivedAt  = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            FirstByteAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Status      = "OK"
        };

        var respBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response));
        await ws.SendAsync(
            new ArraySegment<byte>(respBytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None);
    }
});

// ── MQTT Responder: Mosquitto broker'a bağlan ────────────────────────────────
using var cts2 = new CancellationTokenSource();
var mqttResponder = new MqttResponder("localhost", 1883);
var mqttTask = mqttResponder.StartAsync(cts2.Token);

Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════╗");
Console.WriteLine("║         Protocol Benchmark V3 – Sunucu              ║");
Console.WriteLine("╠══════════════════════════════════════════════════════╣");
Console.WriteLine("║  REST      → http://localhost:5000/message          ║");
Console.WriteLine("║  SSE       → http://localhost:5001/stream           ║");
Console.WriteLine("║  WebSocket → ws://localhost:5002/ws                 ║");
Console.WriteLine("║  MQTT      → localhost:1883  (Mosquitto)            ║");
Console.WriteLine("║  Durdurmak için CTRL+C                               ║");
Console.WriteLine("╚══════════════════════════════════════════════════════╝");
Console.WriteLine();

await app.RunAsync();
