# GuidDemo

## Overview

GuidDemo is a .NET 8 console application that demonstrates the performance and design trade-offs between two approaches for handling primary and foreign keys in SQL Server tables using GUIDs:

- **Scenario A:** Database-generated GUIDs (`NEWSEQUENTIALID()`) as clustered primary keys, requiring SELECTs to resolve foreign keys.
- **Scenario B:** Deterministic GUIDs (UUID v5-style) generated in application code, allowing foreign keys to be computed without database lookups.

The project uses Dapper for data access and targets SQL Server.

## Scenarios Compared

### Scenario A: DB-Generated PKs

- Tables use `NEWSEQUENTIALID()` for primary keys with clustered indexing.
- Foreign keys are resolved by querying the database for the corresponding GUID.
- Each insert operation requires 2 SELECTs to resolve foreign keys (AdminOfficeId and RegOfficeId).

### Scenario B: Deterministic PKs

- Application generates deterministic GUIDs using UUID v5 (SHA-1 hash of namespace + business key).
- Foreign keys are computed in code using the same deterministic algorithm, eliminating the need for SELECTs.
- Insert operations are faster due to zero SELECTs for FK resolution.

## Database Schema

The project creates two sets of tables:

- `Addresses_DB` and `Entities_DB` for Scenario A (with `NEWSEQUENTIALID()` defaults).
- `Addresses_Det` and `Entities_Det` for Scenario B (application-supplied deterministic IDs).

Both sets store similar data but differ in how primary and foreign keys are generated and resolved. Both use clustered primary keys on the Id column for consistent indexing behavior.

## Performance Testing Methodology

### Test Configuration

- **Address Count:** 2,000 unique business codes
- **Entity Count:** 50,000 entity records (each referencing 2 addresses)
- **Test Runs:** 5 iterations for statistical validity
- **Metrics Measured:** 
  - Execution time (average, min, max, standard deviation)
  - Memory usage
  - Database SELECT count

### Test Isolation

- Each test run starts with clean tables
- Garbage collection is forced between tests
- Connection warm-up is performed
- Same test data is used across all runs

## How It Works

1. **Database Setup:** The SQL script (`DbCreation.sql`) creates the required tables in the `GuidDemo2` database.
2. **Data Preparation:** Address catalog is seeded for both scenarios with identical business codes.
3. **Entity Insertion Testing:** 
   - **Scenario A:** Resolves foreign keys via 2 SELECTs per entity (100,000 total SELECTs).
   - **Scenario B:** Computes foreign keys deterministically (0 SELECTs).
4. **Statistical Analysis:** Multiple test runs provide average performance metrics and variance analysis.

## Usage

1. **Database Preparation:**  
   Ensure SQL Server is running and accessible. Execute `DbCreation.sql` to set up the schema in the `GuidDemo2` database.

2. **Configuration:**  
   Update the connection string in `Program.cs` if needed:
   ```csharp
   private const string ConnString = "Server=localhost;Database=GuidDemo2;User ID=sa;Password=Azure@123;TrustServerCertificate=True;";
   ```

3. **Run the Application:**  
   Build and run the project using Visual Studio 2022 or the .NET CLI:
   ```bash
   dotnet run
   ```

4. **Review Results:**  
   The console output will display:
   - Individual test run results
   - Statistical summary (avg, min, max, standard deviation)
   - Memory usage comparison
   - Performance improvement percentage
   - SELECT count reduction

## Expected Results

Typical performance improvements with deterministic GUIDs:

- **Time:** 60-80% faster execution
- **Database Load:** 100,000 fewer SELECT operations
- **Scalability:** Linear improvement as entity count increases

## Dependencies

- [.NET 8](https://dotnet.microsoft.com/)
- [Dapper](https://github.com/DapperLib/Dapper) (v2.1.66)
- [Microsoft.Data.SqlClient](https://www.nuget.org/packages/Microsoft.Data.SqlClient/) (v6.1.1)

## Key Files

- `DbCreation.sql` – SQL script to create and clean up tables.
- `Program.cs` – Main application logic and performance test framework.
- `GuidDemo.csproj` – Project configuration with package dependencies.

## Technical Notes

- **UUID v5 Implementation:** Uses SHA-1 hashing with proper RFC 4122 version and variant bits.
- **Deterministic Key Generation:** Format: `{namespace-guid}|{tenant-id}|{site-id}|{business-code}`
- **Index Strategy:** Both scenarios use clustered primary keys for fair comparison.
- **Transaction Isolation:** All operations use explicit transactions for consistency.
- **Test Validity:** Multiple runs with statistical analysis account for system variance.

## Limitations

- No foreign key constraints are enforced in the schema (relationships are logical only).
- Test focuses on insert performance; read performance patterns may differ.
- Results may vary based on hardware, SQL Server configuration, and system load.

## License

This project is provided for educational and demonstration purposes.
