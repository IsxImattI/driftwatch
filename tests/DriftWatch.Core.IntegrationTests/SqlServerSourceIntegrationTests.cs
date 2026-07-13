namespace DriftWatch.Core.IntegrationTests;

public sealed class SqlServerSourceIntegrationTests : IClassFixture<SqlServerFixture>
{
    private readonly SqlServerFixture _fixture;

    public SqlServerSourceIntegrationTests(SqlServerFixture fixture)
    {
        _fixture = fixture;
    }

    private SqlServerSource CreateSeededSource()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        return new SqlServerSource(_fixture.SeededConnectionString);
    }

    [SkippableFact]
    public async Task ReadDetailedAsync_ReturnsAllSeededObjectsWithCorrectTypes()
    {
        var source = CreateSeededSource();

        var result = await source.ReadDetailedAsync(CancellationToken.None);

        var byFullName = result.Objects.ToDictionary(o => o.FullName, o => o.Type);
        Assert.Equal(5, byFullName.Count);
        Assert.Equal(SchemaObjectType.View, byFullName["dbo.OrdersView"]);
        Assert.Equal(SchemaObjectType.StoredProcedure, byFullName["dbo.GetOrders"]);
        Assert.Equal(SchemaObjectType.ScalarFunction, byFullName["dbo.AddOne"]);
        Assert.Equal(SchemaObjectType.TableFunction, byFullName["dbo.OrdersTvf"]);
        Assert.Equal(SchemaObjectType.Trigger, byFullName["dbo.OrdersAudit"]);
    }

    [SkippableFact]
    public async Task ReadDetailedAsync_EncryptedObject_GoesToEncryptedObjectsOnly()
    {
        var source = CreateSeededSource();

        var result = await source.ReadDetailedAsync(CancellationToken.None);

        var encrypted = Assert.Single(result.EncryptedObjects);
        Assert.Equal("dbo.SecretProc", encrypted.FullName);
        Assert.Equal(SchemaObjectType.StoredProcedure, encrypted.Type);
        Assert.DoesNotContain(result.Objects, o => o.FullName == "dbo.SecretProc");
    }

    [SkippableFact]
    public async Task ReadDetailedAsync_ViewDefinition_ContainsExpectedSelect()
    {
        var source = CreateSeededSource();

        var result = await source.ReadDetailedAsync(CancellationToken.None);

        var view = Assert.Single(result.Objects, o => o.FullName == "dbo.OrdersView");
        Assert.Contains("SELECT Id FROM dbo.Orders", view.Definition);
    }

    [SkippableFact]
    public async Task ReadDetailedAsync_DatabaseWithOnlyTables_ReturnsEmptyLists()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var source = new SqlServerSource(_fixture.EmptyConnectionString);

        var result = await source.ReadDetailedAsync(CancellationToken.None);

        Assert.Empty(result.Objects);
        Assert.Empty(result.EncryptedObjects);
    }
}
