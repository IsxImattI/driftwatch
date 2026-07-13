using DriftWatch.Core;

namespace DriftWatch.Cli.Tests;

public sealed class SchemaSourceFactoryTests : IDisposable
{
    private readonly string _tempDir;

    public SchemaSourceFactoryTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "driftwatch-cli-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void Create_ExistingDirectory_ReturnsScriptFolderSource()
    {
        var source = SchemaSourceFactory.Create(_tempDir);

        Assert.IsType<ScriptFolderSource>(source);
    }

    [Fact]
    public void Create_ConnectionString_ReturnsSqlServerSource()
    {
        var source = SchemaSourceFactory.Create("Server=localhost,14333;Database=AppDb;Integrated Security=True");

        Assert.IsType<SqlServerSource>(source);
    }

    [Fact]
    public void Create_NonExistingPathThatIsNotAConnectionString_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(
            () => SchemaSourceFactory.Create(Path.Combine(_tempDir, "no-such-subfolder")));
    }

    [Fact]
    public void Create_BlankInput_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => SchemaSourceFactory.Create("   "));
    }
}
