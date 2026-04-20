using ProtocolBenchmark.Models;

namespace ProtocolBenchmark.Client;

/// <summary>
/// Tüm kullanıcı etkileşimini yönetir.
/// Checkpoint varsa devam/baştan seçeneği sunar.
/// </summary>
public static class ConsoleMenu
{
    public static (MenuChoice Choice, ProtocolType? Protocol, ScenarioType? Scenario) Show(
        CheckpointManager checkpoint)
    {
        Console.Clear();
        PrintHeader();

        // Önceki oturum varsa göster
        if (checkpoint.HasPreviousSession)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("  ⚠️  Yarıda kalmış oturum bulundu!\n");
            Console.ResetColor();
            checkpoint.PrintStatus();
            Console.WriteLine();

            Console.WriteLine("  Ne yapmak istersiniz?");
            Console.WriteLine("  1 → Kaldığı yerden devam et");
            Console.WriteLine("  2 → Baştan başlat (yeni oturum)");
            Console.WriteLine("  3 → Tamamlananları listele");
            Console.WriteLine("  4 → Tek senaryo/protokol seç");
            Console.Write("\n  Seçim [1-4]: ");

            var choice = Console.ReadLine()?.Trim();
            switch (choice)
            {
                case "1": return (MenuChoice.Continue, null, null);
                case "2": return (MenuChoice.Reset, null, null);
                case "3": return (MenuChoice.ListCompleted, null, null);
                case "4": return SelectSingle();
                default : return (MenuChoice.Continue, null, null);
            }
        }

        // Yeni oturum menüsü
        return ShowMainMenu();
    }

    private static (MenuChoice, ProtocolType?, ScenarioType?) ShowMainMenu()
    {
        Console.WriteLine("  Protokol seçin:");
        Console.WriteLine("  1 → REST");
        Console.WriteLine("  2 → SSE  (Server-Sent Events)");
        Console.WriteLine("  3 → WebSocket");
        Console.WriteLine("  4 → MQTT");
        Console.WriteLine("  5 → Tümü (4 protokol karşılaştırmalı)");
        Console.Write("\n  Seçim [1-5]: ");

        var protoInput = Console.ReadLine()?.Trim();
        ProtocolType? selectedProtocol = protoInput switch
        {
            "1" => ProtocolType.REST,
            "2" => ProtocolType.SSE,
            "3" => ProtocolType.WebSocket,
            "4" => ProtocolType.MQTT,
            _   => null   // null = tümü
        };

        Console.WriteLine();
        PrintScenarioMenu();
        Console.Write("  Seçim [0-9]: ");

        var scenarioInput = Console.ReadLine()?.Trim();

        if (scenarioInput == "0")
            return (MenuChoice.RunAll, selectedProtocol, null);

        if (int.TryParse(scenarioInput, out int idx) && idx >= 1 && idx <= 9)
        {
            var scenario = (ScenarioType)(idx - 1);
            return (MenuChoice.RunSingle, selectedProtocol, scenario);
        }

        // Geçersiz giriş → tümünü çalıştır
        Console.WriteLine("  Geçersiz giriş, tüm senaryolar çalıştırılacak.");
        return (MenuChoice.RunAll, selectedProtocol, null);
    }

    private static (MenuChoice, ProtocolType?, ScenarioType?) SelectSingle()
    {
        Console.WriteLine();
        Console.WriteLine("  Protokol:");
        foreach (var p in Enum.GetValues<ProtocolType>())
            Console.WriteLine($"    {(int)p + 1} → {p}");
        Console.Write("  Seçim: ");
        int.TryParse(Console.ReadLine(), out int pi);
        var proto = (ProtocolType)(pi - 1);

        Console.WriteLine();
        PrintScenarioMenu();
        Console.Write("  Seçim [1-9]: ");
        int.TryParse(Console.ReadLine(), out int si);
        var scenario = (ScenarioType)(si - 1);

        return (MenuChoice.RunSingle, proto, scenario);
    }

    private static void PrintScenarioMenu()
    {
        Console.WriteLine("  Senaryo seçin:");
        Console.WriteLine("  ─────────────────────────────────────────────────");
        var scenarios = Enum.GetValues<ScenarioType>();
        for (int i = 0; i < scenarios.Length; i++)
        {
            var s    = scenarios[i];
            var desc = ScenarioRegistry.Describe(s);
            Console.WriteLine($"  {i + 1} → {s,-22} {desc}");
        }
        Console.WriteLine("  ─────────────────────────────────────────────────");
        Console.WriteLine("  0 → Tüm senaryolar sırayla (tam benchmark)");
    }

    private static void PrintHeader()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         Protocol Benchmark V3 – Test İstemcisi          ║");
        Console.WriteLine("║         REST • SSE • WebSocket • MQTT                   ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();
    }

    public static void PrintCompleted(CheckpointManager checkpoint)
    {
        Console.WriteLine("\n  ✅ Tamamlanan senaryolar:");
        foreach (var key in checkpoint.Data.Completed)
            Console.WriteLine($"    ✓ {key}");
        Console.WriteLine();
        Console.Write("  Devam etmek için Enter'a basın...");
        Console.ReadLine();
    }
}

public enum MenuChoice
{
    RunAll,
    RunSingle,
    Continue,
    Reset,
    ListCompleted
}
