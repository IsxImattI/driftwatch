namespace DriftWatch.Core.Tests;

public class DriftComparerTests
{
    private static SchemaObject View(string name, string definition, string schema = "dbo") =>
        new(schema, name, SchemaObjectType.View, definition);

    private static DriftReport Compare(
        IReadOnlyList<SchemaObject> source,
        IReadOnlyList<SchemaObject> target,
        NormalizeOptions? options = null) =>
        DriftComparer.Compare(source, target, options ?? NormalizeOptions.Default);

    [Fact]
    public void Compare_ObjectMissingInTarget_IsOnlyInSource()
    {
        var obj = View("V1", "CREATE VIEW dbo.V1 AS SELECT 1");

        var report = Compare([obj], []);

        Assert.Equal([obj], report.OnlyInSource);
        Assert.Empty(report.OnlyInTarget);
        Assert.Empty(report.Different);
        Assert.True(report.HasDrift);
    }

    [Fact]
    public void Compare_ExtraObjectInTarget_IsOnlyInTarget()
    {
        var obj = View("V1", "CREATE VIEW dbo.V1 AS SELECT 1");

        var report = Compare([], [obj]);

        Assert.Empty(report.OnlyInSource);
        Assert.Equal([obj], report.OnlyInTarget);
        Assert.Empty(report.Different);
        Assert.True(report.HasDrift);
    }

    [Fact]
    public void Compare_DifferentDefinitions_ReportsPairAsDifferent()
    {
        var source = View("V1", "CREATE VIEW dbo.V1 AS SELECT 1");
        var target = View("V1", "CREATE VIEW dbo.V1 AS SELECT 2");

        var report = Compare([source], [target]);

        var pair = Assert.Single(report.Different);
        Assert.Same(source, pair.Source);
        Assert.Same(target, pair.Target);
        Assert.Empty(report.OnlyInSource);
        Assert.Empty(report.OnlyInTarget);
        Assert.True(report.HasDrift);
    }

    [Fact]
    public void Compare_WhitespaceOnlyDifference_IsNoDrift()
    {
        var source = View("V1", "\r\nCREATE VIEW dbo.V1 AS   \r\nSELECT 1  \r\n\r\n");
        var target = View("V1", "CREATE VIEW dbo.V1 AS\nSELECT 1");

        var report = Compare([source], [target]);

        Assert.Empty(report.OnlyInSource);
        Assert.Empty(report.OnlyInTarget);
        Assert.Empty(report.Different);
        Assert.False(report.HasDrift);
    }

    [Fact]
    public void Compare_CreateOrAlterVsCreate_IsNoDrift()
    {
        var source = View("V1", "CREATE OR ALTER VIEW dbo.V1 AS SELECT 1");
        var target = View("V1", "CREATE VIEW dbo.V1 AS SELECT 1");

        var report = Compare([source], [target]);

        Assert.False(report.HasDrift);
    }

    [Fact]
    public void Compare_MatchesNamesCaseInsensitively()
    {
        var source = View("MyView", "CREATE VIEW dbo.MyView AS SELECT 1", schema: "DBO");
        var target = View("MYVIEW", "CREATE VIEW dbo.MyView AS SELECT 1", schema: "dbo");

        var report = Compare([source], [target]);

        Assert.False(report.HasDrift);
    }

    [Fact]
    public void Compare_SameNameDifferentType_AreNotMatched()
    {
        var source = new SchemaObject("dbo", "Thing", SchemaObjectType.View, "CREATE VIEW dbo.Thing AS SELECT 1");
        var target = new SchemaObject("dbo", "Thing", SchemaObjectType.StoredProcedure, "CREATE PROCEDURE dbo.Thing AS SELECT 1");

        var report = Compare([source], [target]);

        Assert.Equal([source], report.OnlyInSource);
        Assert.Equal([target], report.OnlyInTarget);
        Assert.Empty(report.Different);
    }

    [Fact]
    public void Compare_CaseDifferenceInDefinition_IsDrift_WhenCaseSensitive()
    {
        var source = View("V1", "CREATE VIEW dbo.V1 AS SELECT Col FROM t");
        var target = View("V1", "CREATE VIEW dbo.V1 AS SELECT col FROM t");

        var report = Compare([source], [target]);

        Assert.Single(report.Different);
    }

    [Fact]
    public void Compare_CaseDifferenceInDefinition_IsNoDrift_WhenIgnoreCase()
    {
        var source = View("V1", "CREATE VIEW dbo.V1 AS SELECT Col FROM t");
        var target = View("V1", "CREATE VIEW dbo.V1 AS SELECT col FROM t");

        var report = Compare([source], [target], new NormalizeOptions(IgnoreCase: true));

        Assert.False(report.HasDrift);
    }

    [Fact]
    public void Compare_DuplicateKeyInSource_Throws()
    {
        var duplicates = new[]
        {
            View("V1", "CREATE VIEW dbo.V1 AS SELECT 1"),
            View("v1", "CREATE VIEW dbo.V1 AS SELECT 2"),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => Compare(duplicates, []));

        Assert.Contains("dbo.v1", ex.Message);
        Assert.Contains("source", ex.Message);
    }

    [Fact]
    public void Compare_DuplicateKeyInTarget_Throws()
    {
        var duplicates = new[]
        {
            View("V1", "CREATE VIEW dbo.V1 AS SELECT 1"),
            View("v1", "CREATE VIEW dbo.V1 AS SELECT 2"),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => Compare([], duplicates));

        Assert.Contains("dbo.v1", ex.Message);
        Assert.Contains("target", ex.Message);
    }

    [Fact]
    public void Compare_IdenticalSets_HaveNoDrift()
    {
        var source = new[]
        {
            View("V1", "CREATE VIEW dbo.V1 AS SELECT 1"),
            new SchemaObject("dbo", "P1", SchemaObjectType.StoredProcedure, "CREATE PROCEDURE dbo.P1 AS SELECT 1"),
        };
        var target = new[]
        {
            View("V1", "CREATE VIEW dbo.V1 AS SELECT 1"),
            new SchemaObject("dbo", "P1", SchemaObjectType.StoredProcedure, "CREATE PROCEDURE dbo.P1 AS SELECT 1"),
        };

        var report = Compare(source, target);

        Assert.False(report.HasDrift);
    }
}
