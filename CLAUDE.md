# DriftWatch

Read-only schema drift detection CLI for SQL Server. Compares two sources
(instance ↔ instance, or instance ↔ folder of .sql scripts) and reports
drift in views, stored procedures, functions, and triggers.

## Hard rules

- **READ-ONLY. Never execute DDL/DML against any database.** Only SELECT
  from system catalog views (sys.objects, sys.sql_modules, sys.schemas)
  and OBJECT_DEFINITION(). No exceptions, not even temp tables.
- No telemetry, no network calls except the SQL connections the user
  explicitly provides.
- Connection strings are never logged or written to disk.

## Scope v1.0 (do not expand without asking)

- Object types: views, stored procedures, scalar/table functions, triggers.
  NOT tables/indexes/constraints (that's v1.1).
- Sources: SQL Server instance (connection string) or directory of .sql files.
- Output: colored terminal diff (Spectre.Console) + `--format json`.
- Exit codes: 0 = no drift, 1 = drift detected, 2 = error. This makes the
  tool usable as a CI gate.

## Stack & conventions

- .NET 10, C#, nullable enabled, TreatWarningsAsErrors=true.
- CLI: Spectre.Console.Cli. Text diff: DiffPlex. DB access:
  Microsoft.Data.SqlClient (raw ADO.NET, no ORM).
- DriftWatch.Core has zero CLI/console dependencies — pure logic,
  fully unit-testable on strings.
- Normalization before comparison lives in Core: whitespace, line endings,
  CREATE vs CREATE OR ALTER, optional case-insensitive mode.
- xUnit for tests. Every normalization rule gets its own test.
- Integration tests run against mcr.microsoft.com/mssql/server via
  docker compose (never against a real/production instance).

## Workflow

- GitHub flow: feature branches → PR → main. Never commit directly to main.
- Conventional commits (feat:, fix:, test:, docs:, chore:).
- Run `dotnet build` and `dotnet test` before declaring any task done.