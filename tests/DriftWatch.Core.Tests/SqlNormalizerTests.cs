namespace DriftWatch.Core.Tests;

public class SqlNormalizerTests
{
    private static string Normalize(string definition) =>
        SqlNormalizer.Normalize(definition, NormalizeOptions.Default);

    // --- line endings ---

    [Fact]
    public void Normalize_ConvertsCrLfToLf()
    {
        Assert.Equal("line1\nline2", Normalize("line1\r\nline2"));
    }

    [Fact]
    public void Normalize_ConvertsLoneCrToLf()
    {
        Assert.Equal("line1\nline2", Normalize("line1\rline2"));
    }

    [Fact]
    public void Normalize_MixedLineEndings_AllBecomeLf()
    {
        Assert.Equal("a\nb\nc\nd", Normalize("a\r\nb\rc\nd"));
    }

    // --- trailing whitespace ---

    [Fact]
    public void Normalize_RemovesTrailingSpacesFromEachLine()
    {
        Assert.Equal("SELECT 1\nFROM t", Normalize("SELECT 1   \nFROM t  "));
    }

    [Fact]
    public void Normalize_RemovesTrailingTabs()
    {
        Assert.Equal("SELECT 1", Normalize("SELECT 1\t\t"));
    }

    [Fact]
    public void Normalize_KeepsLeadingIndentation()
    {
        Assert.Equal("SELECT\n    col1", Normalize("SELECT\n    col1   "));
    }

    // --- leading/trailing blank lines ---

    [Fact]
    public void Normalize_RemovesLeadingBlankLines()
    {
        Assert.Equal("SELECT 1", Normalize("\n\n\nSELECT 1"));
    }

    [Fact]
    public void Normalize_RemovesTrailingBlankLines()
    {
        Assert.Equal("SELECT 1", Normalize("SELECT 1\n\n\n"));
    }

    [Fact]
    public void Normalize_KeepsBlankLinesInsideDefinition()
    {
        Assert.Equal("SELECT 1\n\nSELECT 2", Normalize("\nSELECT 1\n\nSELECT 2\n"));
    }

    [Fact]
    public void Normalize_WhitespaceOnlyDefinition_BecomesEmpty()
    {
        Assert.Equal("", Normalize("  \r\n \t \n"));
    }

    // --- CREATE OR ALTER -> CREATE ---

    [Fact]
    public void Normalize_ReplacesCreateOrAlterAtStart()
    {
        Assert.Equal(
            "CREATE VIEW dbo.V AS SELECT 1",
            Normalize("CREATE OR ALTER VIEW dbo.V AS SELECT 1"));
    }

    [Fact]
    public void Normalize_ReplacesCreateOrAlter_CaseInsensitive()
    {
        Assert.Equal(
            "create view dbo.V as select 1",
            Normalize("create or alter view dbo.V as select 1"));
    }

    [Fact]
    public void Normalize_ReplacesCreateOrAlter_AcrossMultipleLines()
    {
        Assert.Equal(
            "CREATE PROCEDURE dbo.P AS BEGIN SELECT 1 END",
            Normalize("CREATE OR\nALTER PROCEDURE dbo.P AS BEGIN SELECT 1 END"));
    }

    [Fact]
    public void Normalize_DoesNotReplaceCreateOrAlterInsideDefinition()
    {
        const string definition =
            "CREATE PROCEDURE dbo.P AS\nEXEC('CREATE OR ALTER VIEW dbo.V AS SELECT 1')";
        Assert.Equal(definition, Normalize(definition));
    }

    [Fact]
    public void Normalize_PlainCreate_IsUnchanged()
    {
        const string definition = "CREATE VIEW dbo.V AS SELECT 1";
        Assert.Equal(definition, Normalize(definition));
    }

    [Fact]
    public void Normalize_CreateOrAlterAndCreate_ProduceSameResult()
    {
        Assert.Equal(
            Normalize("CREATE VIEW dbo.V AS SELECT 1"),
            Normalize("CREATE OR ALTER VIEW dbo.V AS SELECT 1"));
    }

    // --- IgnoreCase must not touch the definition text ---

    [Fact]
    public void Normalize_IgnoreCase_DoesNotChangeCasing()
    {
        const string definition = "CREATE VIEW dbo.V AS SELECT 'Hello World'";
        var normalized = SqlNormalizer.Normalize(definition, new NormalizeOptions(IgnoreCase: true));
        Assert.Equal(definition, normalized);
    }
}
