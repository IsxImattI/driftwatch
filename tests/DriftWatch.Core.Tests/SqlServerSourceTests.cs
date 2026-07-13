namespace DriftWatch.Core.Tests;

public class SqlServerSourceTests
{
    // --- type code mapping (sys.objects.type is char(2), space-padded) ---

    [Theory]
    [InlineData("V", SchemaObjectType.View)]
    [InlineData("P", SchemaObjectType.StoredProcedure)]
    [InlineData("FN", SchemaObjectType.ScalarFunction)]
    [InlineData("IF", SchemaObjectType.TableFunction)]
    [InlineData("TF", SchemaObjectType.TableFunction)]
    [InlineData("TR", SchemaObjectType.Trigger)]
    public void MapTypeCode_MapsAllSupportedCodes(string code, SchemaObjectType expected)
    {
        Assert.Equal(expected, SqlServerSource.MapTypeCode(code));
    }

    [Theory]
    [InlineData("V ", SchemaObjectType.View)]
    [InlineData("P ", SchemaObjectType.StoredProcedure)]
    public void MapTypeCode_TrimsChar2Padding(string code, SchemaObjectType expected)
    {
        Assert.Equal(expected, SqlServerSource.MapTypeCode(code));
    }

    [Fact]
    public void MapTypeCode_UnknownCode_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => SqlServerSource.MapTypeCode("U"));

        Assert.Contains("'U'", ex.Message);
    }

    // --- Description must never leak credentials ---

    [Fact]
    public void Description_ContainsServerAndDatabase_ButNotPassword()
    {
        var source = new SqlServerSource(
            "Server=myserver.example.com;Database=MyDb;User Id=sa;Password=Sup3rS3cret!;TrustServerCertificate=True");

        Assert.Contains("myserver.example.com", source.Description);
        Assert.Contains("MyDb", source.Description);
        Assert.DoesNotContain("Sup3rS3cret!", source.Description);
        Assert.DoesNotContain("Password", source.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User Id", source.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Description_MissingDatabase_UsesDefaultPlaceholder()
    {
        var source = new SqlServerSource("Server=localhost;Integrated Security=True");

        Assert.Contains("localhost", source.Description);
        Assert.Contains("(default)", source.Description);
    }

    [Fact]
    public void Constructor_InvalidConnectionString_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => new SqlServerSource("this is not a connection string"));
    }
}
