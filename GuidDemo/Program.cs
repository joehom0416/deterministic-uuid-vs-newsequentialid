using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;

class Program
{
    // Adjust as needed
    private const string ConnString ="Server=localhost;Database=GuidDemo2;User ID=sa;Password=Azure@123;TrustServerCertificate=True;";

    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SiteId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    // One namespace per entity-type (any fixed GUID you choose)
    private static readonly Guid AddressNamespace = Guid.Parse("11111111-2222-3333-4444-555555555555");

    static void Main()
    {
        Console.WriteLine("== Guid Demo: DB Lookup vs Deterministic ==");
        const int addressCount = 2_000;   // unique business codes to seed
        const int entityCount = 50_000;  // how many entity rows to insert

        using var conn = new SqlConnection(ConnString);
        conn.Open();

        // Clean tables
        conn.Execute("TRUNCATE TABLE dbo.Entities_DB; TRUNCATE TABLE dbo.Addresses_DB; TRUNCATE TABLE dbo.Entities_Det; TRUNCATE TABLE dbo.Addresses_Det;");

        // Seed address catalog for both scenarios
        SeedAddresses_DB(conn, addressCount);
        SeedAddresses_Det(conn, addressCount);

        // Prepare test data: each entity references two address business codes
        var rng = new Random(42);
        var pairs = Enumerable.Range(0, entityCount).Select(_ =>
        {
            var adminCode = (1000 + rng.Next(addressCount)).ToString();
            var regCode = (1000 + rng.Next(addressCount)).ToString();
            return (Admin: adminCode, Reg: regCode);
        }).ToArray();

        // Scenario A: DB-generated PKs -> must SELECT FK per insert
        var resultA = InsertEntities_WithDbLookup(conn, pairs);
        // Scenario B: Deterministic PKs -> compute FK, 0 selects per insert
        var resultB = InsertEntities_WithDeterministic(conn, pairs);

        Console.WriteLine();
        Console.WriteLine("== RESULTS ==");
        Console.WriteLine($"Scenario A (DB lookup + NEWSEQUENTIALID): {resultA.ElapsedMs,8} ms | selects: {resultA.Selects}");
        Console.WriteLine($"Scenario B (Deterministic FKs in code)  : {resultB.ElapsedMs,8} ms | selects: {resultB.Selects}");
        Console.WriteLine();
        Console.WriteLine("Note: Both use a clustered index on CreatedAtUtc for fair insert performance;");
        Console.WriteLine("the measured delta is dominated by FK resolution: many SELECTs vs zero SELECTs.");
    }

    static void SeedAddresses_DB(IDbConnection conn, int count)
    {
        using var tx = conn.BeginTransaction();
        var sql = @"INSERT INTO dbo.Addresses_DB (TenantId, SiteId, BusinessCode) VALUES (@t, @s, @code);";
        for (int i = 0; i < count; i++)
        {
            conn.Execute(sql, new { t = TenantId, s = SiteId, code = (1000 + i).ToString() }, tx);
        }
        tx.Commit();
    }

    static void SeedAddresses_Det(IDbConnection conn, int count)
    {
        using var tx = conn.BeginTransaction();
        var sql = @"INSERT INTO dbo.Addresses_Det (Id, TenantId, SiteId, BusinessCode) VALUES (@id, @t, @s, @code);";
        for (int i = 0; i < count; i++)
        {
            var code = (1000 + i).ToString();
            var id = CreateDeterministicGuid(AddressNamespace, $"{TenantId:D}|{SiteId:D}|{code}");
            conn.Execute(sql, new { id, t = TenantId, s = SiteId, code }, tx);
        }
        tx.Commit();
    }

    static (long ElapsedMs, long Selects) InsertEntities_WithDbLookup(IDbConnection conn, (string Admin, string Reg)[] pairs)
    {
        long selects = 0;
        var sw = Stopwatch.StartNew();
        using var tx = conn.BeginTransaction();

        var selectSql = @"SELECT Id FROM dbo.Addresses_DB WITH (READUNCOMMITTED)
                          WHERE TenantId=@t AND SiteId=@s AND BusinessCode=@code;";
        var insertSql = @"INSERT INTO dbo.Entities_DB (TenantId, SiteId, AdminOfficeId, RegOfficeId, CreatedAtUtc)
                          VALUES (@t, @s, @adminId, @regId, @ts);";

        foreach (var p in pairs)
        {
            var adminId = conn.ExecuteScalar<Guid>(selectSql, new { t = TenantId, s = SiteId, code = p.Admin }, tx);
            selects++;
            var regId = conn.ExecuteScalar<Guid>(selectSql, new { t = TenantId, s = SiteId, code = p.Reg }, tx);
            selects++;

            var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            conn.Execute(insertSql, new { t = TenantId, s = SiteId, adminId, regId, ts }, tx);
        }

        tx.Commit();
        sw.Stop();
        return (sw.ElapsedMilliseconds, selects);
    }

    static (long ElapsedMs, long Selects) InsertEntities_WithDeterministic(IDbConnection conn, (string Admin, string Reg)[] pairs)
    {
        var sw = Stopwatch.StartNew();
        using var tx = conn.BeginTransaction();

        var insertSql = @"INSERT INTO dbo.Entities_Det (Id, TenantId, SiteId, AdminOfficeId, RegOfficeId, CreatedAtUtc)
                          VALUES (@id, @t, @s, @adminId, @regId, @ts);";

        foreach (var p in pairs)
        {
            // compute FKs locally: zero SELECTs
            var adminId = CreateDeterministicGuid(AddressNamespace, $"{TenantId:D}|{SiteId:D}|{p.Admin}");
            var regId = CreateDeterministicGuid(AddressNamespace, $"{TenantId:D}|{SiteId:D}|{p.Reg}");

            var id = Guid.NewGuid(); // entity PK can be random; PK is nonclustered
            var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            conn.Execute(insertSql, new { id, t = TenantId, s = SiteId, adminId, regId, ts }, tx);
        }

        tx.Commit();
        sw.Stop();
        return (sw.ElapsedMilliseconds, 0);
    }

    // UUID v5-style deterministic GUID (SHA-1 over namespace + name)
    static Guid CreateDeterministicGuid(Guid ns, string name)
    {
        static byte[] ToNetworkOrder(Guid g)
        {
            var b = g.ToByteArray();
            void swap(int i, int j) { (b[i], b[j]) = (b[j], b[i]); }
            swap(0, 3); swap(1, 2); swap(4, 5); swap(6, 7);
            return b;
        }

        var nsBytes = ToNetworkOrder(ns);
        var nameBytes = Encoding.UTF8.GetBytes(name);

        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(nsBytes.Concat(nameBytes).ToArray());

        // Set version (5) and variant (RFC 4122)
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);

        // Take first 16 bytes
        var guidBytes = new byte[16];
        Array.Copy(hash, 0, guidBytes, 0, 16);

        // Convert back to little-endian Guid layout
        void swap(int i, int j) { (guidBytes[i], guidBytes[j]) = (guidBytes[j], guidBytes[i]); }
        swap(0, 3); swap(1, 2); swap(4, 5); swap(6, 7);

        return new Guid(guidBytes);
    }
}
