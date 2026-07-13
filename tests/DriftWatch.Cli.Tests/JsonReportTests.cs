using System.Text.Json;
using DriftWatch.Core;

namespace DriftWatch.Cli.Tests;

public class JsonReportTests
{
    private static readonly SchemaObject OnlySource =
        new("dbo", "V1", SchemaObjectType.View, "CREATE VIEW dbo.V1 AS SELECT TopSecretColumn FROM t");

    private static readonly SchemaObject OnlyTarget =
        new("dbo", "P1", SchemaObjectType.StoredProcedure, "CREATE PROCEDURE dbo.P1 AS SELECT 1");

    private static readonly DriftPair DifferentPair = new(
        new SchemaObject("sales", "F1", SchemaObjectType.ScalarFunction, "CREATE FUNCTION source"),
        new SchemaObject("sales", "F1", SchemaObjectType.ScalarFunction, "CREATE FUNCTION target"));

    private static string SerializeSample() =>
        JsonReport.Serialize(
            new DriftReport([OnlySource], [OnlyTarget], [DifferentPair]),
            sourceObjectCount: 5,
            targetObjectCount: 4,
            skippedFiles: ["C:\\scripts\\data.sql"],
            encryptedObjects: [new EncryptedObjectInfo("dbo", "SecretProc", SchemaObjectType.StoredProcedure)]);

    [Fact]
    public void Serialize_ProducesExpectedCamelCaseShape()
    {
        using var doc = JsonDocument.Parse(SerializeSample());
        var root = doc.RootElement;

        var summary = root.GetProperty("summary");
        Assert.Equal(5, summary.GetProperty("sourceObjectCount").GetInt32());
        Assert.Equal(4, summary.GetProperty("targetObjectCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("onlyInSourceCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("onlyInTargetCount").GetInt32());
        Assert.Equal(1, summary.GetProperty("differentCount").GetInt32());
        Assert.True(summary.GetProperty("hasDrift").GetBoolean());

        var onlyInSource = Assert.Single(root.GetProperty("onlyInSource").EnumerateArray());
        Assert.Equal("dbo", onlyInSource.GetProperty("schema").GetString());
        Assert.Equal("V1", onlyInSource.GetProperty("name").GetString());
        Assert.Equal("view", onlyInSource.GetProperty("type").GetString());

        var different = Assert.Single(root.GetProperty("different").EnumerateArray());
        Assert.Equal("sales", different.GetProperty("schema").GetString());
        Assert.Equal("scalarFunction", different.GetProperty("type").GetString());

        var warnings = root.GetProperty("warnings");
        var skipped = Assert.Single(warnings.GetProperty("skippedFiles").EnumerateArray());
        Assert.Equal("C:\\scripts\\data.sql", skipped.GetString());
        var encrypted = Assert.Single(warnings.GetProperty("encryptedObjects").EnumerateArray());
        Assert.Equal("SecretProc", encrypted.GetProperty("name").GetString());
        Assert.Equal("storedProcedure", encrypted.GetProperty("type").GetString());
    }

    [Fact]
    public void Serialize_NeverIncludesDefinitions()
    {
        var json = SerializeSample();

        Assert.DoesNotContain("TopSecretColumn", json);
        Assert.DoesNotContain("definition", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Serialize_NoDrift_HasDriftFalseAndEmptyArrays()
    {
        var json = JsonReport.Serialize(
            new DriftReport([], [], []),
            sourceObjectCount: 3,
            targetObjectCount: 3,
            skippedFiles: [],
            encryptedObjects: []);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.False(root.GetProperty("summary").GetProperty("hasDrift").GetBoolean());
        Assert.Empty(root.GetProperty("onlyInSource").EnumerateArray());
        Assert.Empty(root.GetProperty("onlyInTarget").EnumerateArray());
        Assert.Empty(root.GetProperty("different").EnumerateArray());
    }
}
