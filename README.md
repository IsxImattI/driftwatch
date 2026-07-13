# driftwatch
Read-only schema drift detection for SQL Server. Compare instances or scripts, fail your CI on drift.

## Running integration tests

Integration tests run against a throwaway SQL Server container — never
against a real instance. With Docker running:

```
docker compose up -d
dotnet test tests/DriftWatch.Core.IntegrationTests
```

The container listens on `localhost,14333` (non-standard port to avoid
clashing with a local SQL Server). If it is not reachable, the
integration tests skip instead of failing. Optional environment
variables: `DRIFTWATCH_TEST_SA_PASSWORD` (sa password, must match the
one given to compose) or `DRIFTWATCH_TEST_CONNECTION_STRING` (full
override). Tear down with `docker compose down`.
