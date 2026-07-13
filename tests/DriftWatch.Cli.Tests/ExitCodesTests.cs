using DriftWatch.Core;

namespace DriftWatch.Cli.Tests;

public class ExitCodesTests
{
    private static readonly SchemaObject SomeObject =
        new("dbo", "V1", SchemaObjectType.View, "CREATE VIEW dbo.V1 AS SELECT 1");

    [Fact]
    public void FromReport_NoDrift_ReturnsZero()
    {
        Assert.Equal(0, ExitCodes.FromReport(new DriftReport([], [], [])));
    }

    [Fact]
    public void FromReport_OnlyInSource_ReturnsOne()
    {
        Assert.Equal(1, ExitCodes.FromReport(new DriftReport([SomeObject], [], [])));
    }

    [Fact]
    public void FromReport_OnlyInTarget_ReturnsOne()
    {
        Assert.Equal(1, ExitCodes.FromReport(new DriftReport([], [SomeObject], [])));
    }

    [Fact]
    public void FromReport_Different_ReturnsOne()
    {
        var pair = new DriftPair(SomeObject, SomeObject);

        Assert.Equal(1, ExitCodes.FromReport(new DriftReport([], [], [pair])));
    }

    [Fact]
    public void ErrorCode_IsTwo()
    {
        Assert.Equal(2, ExitCodes.Error);
    }
}
