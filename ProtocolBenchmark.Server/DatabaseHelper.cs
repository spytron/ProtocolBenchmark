using Microsoft.Data.Sqlite;
using ProtocolBenchmark.Models;

namespace ProtocolBenchmark.Server;

public static class DatabaseHelper
{
    private static string _connStr = "";

    public static void Initialize(string dbPath)
    {
        _connStr = $"Data Source={dbPath}";
        using var conn = new SqliteConnection(_connStr);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Measurements (
                Id               INTEGER PRIMARY KEY AUTOINCREMENT,
                Protocol         TEXT    NOT NULL,
                Scenario         TEXT    NOT NULL,
                ClientId         TEXT    NOT NULL,
                SequenceNo       INTEGER NOT NULL,
                LatencyMs        REAL    NOT NULL,
                TtfbMs           REAL    NOT NULL DEFAULT 0,
                ConnectTimeMs    REAL    NOT NULL DEFAULT 0,
                PayloadBytes     INTEGER NOT NULL,
                IsSuccess        INTEGER NOT NULL,
                IsMessageLost    INTEGER NOT NULL DEFAULT 0,
                ErrorMessage     TEXT,
                MemoryBytes      INTEGER NOT NULL DEFAULT 0,
                CpuPercent       REAL    NOT NULL DEFAULT 0,
                Timestamp        TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_proto_scenario
                ON Measurements(Protocol, Scenario);";
        cmd.ExecuteNonQuery();
    }

    public static void Insert(MeasurementRecord r)
    {
        using var conn = new SqliteConnection(_connStr);
        conn.Open();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Measurements
                (Protocol, Scenario, ClientId, SequenceNo, LatencyMs,
                 TtfbMs, ConnectTimeMs, PayloadBytes, IsSuccess,
                 IsMessageLost, ErrorMessage, MemoryBytes, CpuPercent, Timestamp)
            VALUES
                ($proto, $scenario, $client, $seq, $latency,
                 $ttfb, $connect, $bytes, $ok,
                 $lost, $err, $mem, $cpu, $ts)";

        cmd.Parameters.AddWithValue("$proto",    r.Protocol.ToString());
        cmd.Parameters.AddWithValue("$scenario", r.Scenario.ToString());
        cmd.Parameters.AddWithValue("$client",   r.ClientId);
        cmd.Parameters.AddWithValue("$seq",      r.SequenceNo);
        cmd.Parameters.AddWithValue("$latency",  r.LatencyMs);
        cmd.Parameters.AddWithValue("$ttfb",     r.TtfbMs);
        cmd.Parameters.AddWithValue("$connect",  r.ConnectTimeMs);
        cmd.Parameters.AddWithValue("$bytes",    r.PayloadBytes);
        cmd.Parameters.AddWithValue("$ok",       r.IsSuccess ? 1 : 0);
        cmd.Parameters.AddWithValue("$lost",     r.IsMessageLost ? 1 : 0);
        cmd.Parameters.AddWithValue("$err",      (object?)r.ErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$mem",      r.MemoryBytes);
        cmd.Parameters.AddWithValue("$cpu",      r.CpuPercent);
        cmd.Parameters.AddWithValue("$ts",       r.Timestamp.ToString("O"));
        cmd.ExecuteNonQuery();
    }
}
