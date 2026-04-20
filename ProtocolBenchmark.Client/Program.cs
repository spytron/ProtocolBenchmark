using ProtocolBenchmark.Client;
using ProtocolBenchmark.Models;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/client-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var checkpoint = new CheckpointManager("results");
var runner     = new TestRunner(
    restUrl:    "http://localhost:5000",
    sseUrl:     "http://localhost:5001",
    wsUrl:      "ws://localhost:5002/ws",
    mqttHost:   "localhost",
    mqttPort:   1883
);

// ── Menüyü göster ──────────────────────────────────────────────────────────
while (true)
{
    var (choice, protocol, scenario) = ConsoleMenu.Show(checkpoint);

    switch (choice)
    {
        // ── Tamamlananları listele ─────────────────────────────────────────
        case MenuChoice.ListCompleted:
            ConsoleMenu.PrintCompleted(checkpoint);
            continue;

        // ── Sıfırla ───────────────────────────────────────────────────────
        case MenuChoice.Reset:
            checkpoint.Reset();
            Console.WriteLine("\n  ✅ Oturum sıfırlandı. Baştan başlanıyor...\n");
            await Task.Delay(1000);
            continue;

        // ── Tek senaryo ───────────────────────────────────────────────────
        case MenuChoice.RunSingle:
        {
            var protocols = protocol.HasValue
                ? new[] { protocol.Value }
                : Enum.GetValues<ProtocolType>();

            var scenarios = scenario.HasValue
                ? new[] { scenario.Value }
                : Enum.GetValues<ScenarioType>();

            foreach (var p in protocols)
            foreach (var s in scenarios)
            {
                checkpoint.MarkStarted(p, s);
                await runner.RunAsync(p, s);
                checkpoint.MarkCompleted(p, s);
            }

            runner.PrintComparison();
            break;
        }

        // ── Kaldığı yerden devam ──────────────────────────────────────────
        case MenuChoice.Continue:
        case MenuChoice.RunAll:
        {
            var remaining = choice == MenuChoice.Continue
                ? checkpoint.GetRemainingTasks()
                : (protocol.HasValue
                    ? checkpoint.GetAllTasks().Where(t => t.Protocol == protocol).ToList()
                    : checkpoint.GetAllTasks());

            var tasks = remaining.ToList();

            if (!tasks.Any())
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\n  ✅ Tüm senaryolar zaten tamamlanmış!");
                Console.ResetColor();
                break;
            }

            Console.WriteLine($"\n  {tasks.Count} senaryo koşulacak...\n");

            int done  = 0;
            int total = tasks.Count;

            foreach (var (p, s) in tasks)
            {
                done++;
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  [{done}/{total}] {p} × {s}");
                Console.ResetColor();

                checkpoint.MarkStarted(p, s);

                try
                {
                    await runner.RunAsync(p, s);
                    checkpoint.MarkCompleted(p, s);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Senaryo hatası: {P} {S}", p, s);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ❌ Hata: {ex.Message}");
                    Console.ResetColor();
                    // Checkpoint'i koruyoruz — bir sonraki başlatmada bu senaryodan devam eder
                }

                // Senaryolar arası kısa mola
                await Task.Delay(2000);
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n╔══════════════════════════════════════════════════════╗");
            Console.WriteLine("║         ✅ Tüm testler tamamlandı!                  ║");
            Console.WriteLine($"║  Tamamlanan : {checkpoint.Data.Completed.Count,3} senaryo                          ║");
            Console.WriteLine($"║  CSV dosyaları: results/ klasöründe                 ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════╝");
            Console.ResetColor();

            runner.PrintComparison();
            break;
        }
    }

    Console.Write("\n  Tekrar menüye dönmek ister misiniz? (E/H): ");
    var again = Console.ReadLine()?.Trim().ToUpperInvariant();
    if (again != "E") break;
}

Log.Information("Program sonlandırıldı.");
