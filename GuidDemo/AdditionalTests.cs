using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using Dapper;
using Microsoft.Data.SqlClient;

namespace GuidDemo
{
    /// <summary>
    /// Additional performance tests to complement the main demo
    /// </summary>
    public static class AdditionalTests
    {
        private const string ConnString = "Server=localhost;Database=GuidDemo2;User ID=sa;Password=Azure@123;TrustServerCertificate=True;";
        private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        private static readonly Guid SiteId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        /// <summary>
        /// Test read performance - does deterministic vs sequential affect SELECT performance?
        /// </summary>
        public static void TestReadPerformance()
        {
            Console.WriteLine("\n== READ PERFORMANCE TEST ==");
            using var conn = new SqlConnection(ConnString);
            conn.Open();

            const int iterations = 10_000;
            var rng = new Random(42);

            // Test random reads from both scenarios
            var dbResults = new List<long>();
            var detResults = new List<long>();

            for (int run = 0; run < 5; run++)
            {
                // Test DB scenario reads
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < iterations; i++)
                {
                    var businessCode = (1000 + rng.Next(2000)).ToString();
                    var result = conn.QuerySingleOrDefault<Guid>(
                        "SELECT Id FROM dbo.Addresses_DB WHERE TenantId=@t AND SiteId=@s AND BusinessCode=@code",
                        new { t = TenantId, s = SiteId, code = businessCode });
                }
                sw.Stop();
                dbResults.Add(sw.ElapsedMilliseconds);

                // Test Det scenario reads
                sw.Restart();
                for (int i = 0; i < iterations; i++)
                {
                    var businessCode = (1000 + rng.Next(2000)).ToString();
                    var result = conn.QuerySingleOrDefault<Guid>(
                        "SELECT Id FROM dbo.Addresses_Det WHERE TenantId=@t AND SiteId=@s AND BusinessCode=@code",
                        new { t = TenantId, s = SiteId, code = businessCode });
                }
                sw.Stop();
                detResults.Add(sw.ElapsedMilliseconds);
            }

            Console.WriteLine($"DB Scenario Read Avg:  {dbResults.Average():F1} ms");
            Console.WriteLine($"Det Scenario Read Avg: {detResults.Average():F1} ms");
        }

        /// <summary>
        /// Test batch insert performance using bulk operations
        /// </summary>
        public static void TestBatchInsertPerformance()
        {
            Console.WriteLine("\n== BATCH INSERT PERFORMANCE TEST ==");
            using var conn = new SqlConnection(ConnString);
            conn.Open();

            const int batchSize = 1000;
            const int totalEntities = 10_000;

            // Clean tables
            conn.Execute("TRUNCATE TABLE dbo.Entities_DB; TRUNCATE TABLE dbo.Entities_Det;");

            var rng = new Random(42);
            var batches = Enumerable.Range(0, totalEntities / batchSize)
                .Select(batchIndex => 
                    Enumerable.Range(0, batchSize).Select(_ => new
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        SiteId = SiteId,
                        AdminOfficeId = Guid.NewGuid(), // Using random for simplicity in this test
                        RegOfficeId = Guid.NewGuid(),
                        CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    }).ToArray()
                ).ToArray();

            // Test DB scenario batch inserts
            var sw = Stopwatch.StartNew();
            using (var tx = conn.BeginTransaction())
            {
                foreach (var batch in batches)
                {
                    conn.Execute(@"INSERT INTO dbo.Entities_DB (TenantId, SiteId, AdminOfficeId, RegOfficeId, CreatedAtUtc)
                                   VALUES (@TenantId, @SiteId, @AdminOfficeId, @RegOfficeId, @CreatedAtUtc)",
                                 batch, tx);
                }
                tx.Commit();
            }
            sw.Stop();
            var dbBatchTime = sw.ElapsedMilliseconds;

            // Test Det scenario batch inserts
            sw.Restart();
            using (var tx = conn.BeginTransaction())
            {
                foreach (var batch in batches)
                {
                    conn.Execute(@"INSERT INTO dbo.Entities_Det (Id, TenantId, SiteId, AdminOfficeId, RegOfficeId, CreatedAtUtc)
                                   VALUES (@Id, @TenantId, @SiteId, @AdminOfficeId, @RegOfficeId, @CreatedAtUtc)",
                                 batch, tx);
                }
                tx.Commit();
            }
            sw.Stop();
            var detBatchTime = sw.ElapsedMilliseconds;

            Console.WriteLine($"DB Scenario Batch Insert:  {dbBatchTime} ms");
            Console.WriteLine($"Det Scenario Batch Insert: {detBatchTime} ms");
            Console.WriteLine($"Batch size: {batchSize}, Total: {totalEntities}");
        }

        /// <summary>
        /// Test index fragmentation over time
        /// </summary>
        public static void TestIndexFragmentation()
        {
            Console.WriteLine("\n== INDEX FRAGMENTATION TEST ==");
            using var conn = new SqlConnection(ConnString);
            conn.Open();

            var fragmentationQuery = @"
                SELECT 
                    OBJECT_NAME(ips.object_id) AS TableName,
                    i.name AS IndexName,
                    ips.avg_fragmentation_in_percent,
                    ips.page_count
                FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'DETAILED') ips
                INNER JOIN sys.indexes i ON ips.object_id = i.object_id AND ips.index_id = i.index_id
                WHERE ips.index_level = 0 
                  AND ips.page_count > 10
                  AND OBJECT_NAME(ips.object_id) IN ('Entities_DB', 'Entities_Det', 'Addresses_DB', 'Addresses_Det')
                ORDER BY TableName, IndexName";

            var results = conn.Query(fragmentationQuery).ToList();
            
            Console.WriteLine("Table Name      | Index Name           | Fragmentation % | Pages");
            Console.WriteLine("----------------|---------------------|-----------------|-------");
            
            foreach (var row in results)
            {
                Console.WriteLine($"{row.TableName,-15} | {row.IndexName,-19} | {row.avg_fragmentation_in_percent,13:F1}% | {row.page_count,5}");
            }
        }

        /// <summary>
        /// Test concurrent insert performance
        /// </summary>
        public static async Task TestConcurrentInserts()
        {
            Console.WriteLine("\n== CONCURRENT INSERT TEST ==");
            const int threadsCount = 4;
            const int insertsPerThread = 2500; // 10k total

            var tasks = Enumerable.Range(0, threadsCount).Select(async threadId =>
            {
                using var conn = new SqlConnection(ConnString);
                await conn.OpenAsync();

                var sw = Stopwatch.StartNew();
                using var tx = conn.BeginTransaction();
                
                for (int i = 0; i < insertsPerThread; i++)
                {
                    var entity = new
                    {
                        Id = Guid.NewGuid(),
                        TenantId = TenantId,
                        SiteId = SiteId,
                        AdminOfficeId = Guid.NewGuid(),
                        RegOfficeId = Guid.NewGuid(),
                        CreatedAtUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };

                    await conn.ExecuteAsync(@"INSERT INTO dbo.Entities_Det (Id, TenantId, SiteId, AdminOfficeId, RegOfficeId, CreatedAtUtc)
                                             VALUES (@Id, @TenantId, @SiteId, @AdminOfficeId, @RegOfficeId, @CreatedAtUtc)",
                                           entity, tx);
                }
                
                tx.Commit();
                sw.Stop();
                return sw.ElapsedMilliseconds;
            });

            var results = await Task.WhenAll(tasks);
            Console.WriteLine($"Concurrent insert results (4 threads, {insertsPerThread:N0} each):");
            for (int i = 0; i < results.Length; i++)
            {
                Console.WriteLine($"Thread {i + 1}: {results[i]:N0} ms");
            }
            Console.WriteLine($"Average: {results.Average():F1} ms");
        }
    }
}