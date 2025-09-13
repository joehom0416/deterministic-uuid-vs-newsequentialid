# GuidDemo

## Overview

GuidDemo is a .NET 8 console application that demonstrates the performance and design trade-offs between two approaches for handling primary and foreign keys in SQL Server tables using GUIDs:

- **Scenario A:** Database-generated GUIDs (`NEWSEQUENTIALID()`) as clustered primary keys, requiring SELECTs to resolve foreign keys.
- **Scenario B:** Deterministic GUIDs (UUID v5-style) generated in application code, allowing foreign keys to be computed without database lookups.

The project uses Dapper for data access and targets SQL Server.

## Scenarios Compared

### Scenario A: DB-Generated PKs

- Tables use `NEWSEQUENTIALID()` for primary keys.
- Foreign keys are resolved by querying the database for the corresponding GUID.
- Each insert operation requires SELECTs to resolve foreign keys.

### Scenario B: Deterministic PKs

- Application generates deterministic GUIDs using a namespace and business key.
- Foreign keys are computed in code, eliminating the need for SELECTs.
- Insert operations are faster due to zero SELECTs for FK resolution.

## Database Schema

The project creates two sets of tables:

- `Addresses_DB` and `Entities_DB` for Scenario A.
- `Addresses_Det` and `Entities_Det` for Scenario B.

Both sets store similar data but differ in how primary and foreign keys are generated and resolved.

## How It Works

1. **Database Setup:** The SQL script (`DbCreation.sql`) creates the required tables in the `GuidDemo2` database.
2. **Seeding Data:** The application seeds address data for both scenarios.
3. **Entity Insertion:** 
   - Scenario A: Resolves foreign keys via SELECTs.
   - Scenario B: Computes foreign keys deterministically.
4. **Performance Measurement:** The application measures and prints the time taken and the number of SELECTs performed for each scenario.

## Usage

1. **Database Preparation:**  
   Ensure SQL Server is running and accessible. Execute `DbCreation.sql` to set up the schema in the `GuidDemo2` database.

2. **Configuration:**  
   Update the connection string in `Program.cs` if needed:
```
   private const string ConnString = "Server=localhost;Database=GuidDemo2;User ID=sa;Password=Azure@123;TrustServerCertificate=True;";
```

3. **Run the Application:**  
   Build and run the project using Visual Studio 2022 or the .NET CLI:
```
   dotnet run
```

4. **Review Results:**  
   The console output will display the elapsed time and SELECT count for both scenarios, highlighting the performance impact of deterministic GUIDs.

## Dependencies

- [.NET 8](https://dotnet.microsoft.com/)
- [Dapper](https://github.com/DapperLib/Dapper)
- [System.Data.SqlClient](https://www.nuget.org/packages/System.Data.SqlClient/)

## Key Files

- `DbCreation.sql` – SQL script to create and clean up tables.
- `Program.cs` – Main application logic and performance test.
- `GuidDemo.csproj` – Project configuration.

## Notes

- The project is intended for demonstration and benchmarking purposes.
- No foreign key constraints are enforced in the schema; relationships are logical.
- The deterministic GUID generation uses SHA-1 hashing (UUID v5 style).

## License

This project is provided for educational and demonstration purposes.
