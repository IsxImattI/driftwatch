namespace DriftWatch.Core.Tests;

public sealed class ScriptFolderSourceTests : IDisposable
{
    private readonly string _root;

    public ScriptFolderSourceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "driftwatch-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string WriteScript(string relativePath, string content)
    {
        var fullPath = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    private Task<ScriptFolderReadResult> ReadAsync() =>
        new ScriptFolderSource(_root).ReadDetailedAsync(CancellationToken.None);

    private async Task<SchemaObject> ReadSingleAsync()
    {
        var result = await ReadAsync();
        Assert.Empty(result.SkippedFiles);
        return Assert.Single(result.Objects);
    }

    // --- object types ---

    [Fact]
    public async Task ReadAsync_RecognizesView()
    {
        WriteScript("v.sql", "CREATE VIEW dbo.V1 AS SELECT 1 AS X");

        var obj = await ReadSingleAsync();

        Assert.Equal(SchemaObjectType.View, obj.Type);
        Assert.Equal("dbo.V1", obj.FullName);
    }

    [Fact]
    public async Task ReadAsync_RecognizesProcedure()
    {
        WriteScript("p.sql", "CREATE PROCEDURE dbo.P1 AS BEGIN SELECT 1 END");

        var obj = await ReadSingleAsync();

        Assert.Equal(SchemaObjectType.StoredProcedure, obj.Type);
        Assert.Equal("dbo.P1", obj.FullName);
    }

    [Fact]
    public async Task ReadAsync_RecognizesProcShorthand()
    {
        WriteScript("p.sql", "CREATE PROC dbo.P1 AS BEGIN SELECT 1 END");

        var obj = await ReadSingleAsync();

        Assert.Equal(SchemaObjectType.StoredProcedure, obj.Type);
    }

    [Fact]
    public async Task ReadAsync_RecognizesTrigger()
    {
        WriteScript("t.sql", "CREATE TRIGGER dbo.T1 ON dbo.Orders AFTER INSERT AS SELECT 1");

        var obj = await ReadSingleAsync();

        Assert.Equal(SchemaObjectType.Trigger, obj.Type);
        Assert.Equal("dbo.T1", obj.FullName);
    }

    [Fact]
    public async Task ReadAsync_ScalarFunction_WhenReturnsScalar()
    {
        WriteScript("f.sql",
            "CREATE FUNCTION dbo.F1 (@a INT) RETURNS INT AS BEGIN RETURN @a END");

        var obj = await ReadSingleAsync();

        Assert.Equal(SchemaObjectType.ScalarFunction, obj.Type);
    }

    [Fact]
    public async Task ReadAsync_TableFunction_WhenReturnsTableInline()
    {
        WriteScript("f.sql",
            "CREATE FUNCTION dbo.F1 (@a INT) RETURNS TABLE AS RETURN (SELECT @a AS X)");

        var obj = await ReadSingleAsync();

        Assert.Equal(SchemaObjectType.TableFunction, obj.Type);
    }

    [Fact]
    public async Task ReadAsync_TableFunction_WhenReturnsTableVariable()
    {
        WriteScript("f.sql",
            "CREATE FUNCTION dbo.F1 (@a INT)\nRETURNS @result TABLE (X INT)\nAS BEGIN INSERT @result VALUES (@a) RETURN END");

        var obj = await ReadSingleAsync();

        Assert.Equal(SchemaObjectType.TableFunction, obj.Type);
    }

    // --- name forms ---

    [Fact]
    public async Task ReadAsync_ParsesBracketedIdentifiers()
    {
        WriteScript("v.sql", "CREATE VIEW [Sales].[Order View] AS SELECT 1 AS X");

        var obj = await ReadSingleAsync();

        Assert.Equal("Sales", obj.Schema);
        Assert.Equal("Order View", obj.Name);
    }

    [Fact]
    public async Task ReadAsync_ParsesQuotedIdentifiers()
    {
        WriteScript("v.sql", "CREATE VIEW \"Sales\".\"OrderView\" AS SELECT 1 AS X");

        var obj = await ReadSingleAsync();

        Assert.Equal("Sales", obj.Schema);
        Assert.Equal("OrderView", obj.Name);
    }

    [Fact]
    public async Task ReadAsync_MissingSchema_DefaultsToDbo()
    {
        WriteScript("v.sql", "CREATE VIEW V1 AS SELECT 1 AS X");

        var obj = await ReadSingleAsync();

        Assert.Equal("dbo", obj.Schema);
        Assert.Equal("V1", obj.Name);
    }

    [Fact]
    public async Task ReadAsync_AllowsWhitespaceAndNewlinesInStatement()
    {
        WriteScript("v.sql", "CREATE\n    VIEW\n    [dbo]\n    .\n    [V1]\nAS SELECT 1 AS X");

        var obj = await ReadSingleAsync();

        Assert.Equal("dbo.V1", obj.FullName);
    }

    // --- CREATE OR ALTER ---

    [Fact]
    public async Task ReadAsync_RecognizesCreateOrAlter()
    {
        WriteScript("v.sql", "create or alter view dbo.V1 as select 1 as X");

        var obj = await ReadSingleAsync();

        Assert.Equal(SchemaObjectType.View, obj.Type);
        Assert.Equal("dbo.V1", obj.FullName);
    }

    // --- comment headers ---

    [Fact]
    public async Task ReadAsync_SkipsLineCommentHeaderBeforeCreate()
    {
        WriteScript("v.sql",
            "-- Copyright 2026 ACME\n-- Do not edit\nCREATE VIEW dbo.V1 AS SELECT 1 AS X");

        var obj = await ReadSingleAsync();

        Assert.Equal("dbo.V1", obj.FullName);
    }

    [Fact]
    public async Task ReadAsync_SkipsBlockCommentHeaderBeforeCreate()
    {
        WriteScript("v.sql",
            "/*\n * Copyright 2026\n * CREATE VIEW dbo.Decoy -- must be ignored\n */\nCREATE VIEW dbo.V1 AS SELECT 1 AS X");

        var obj = await ReadSingleAsync();

        Assert.Equal("dbo.V1", obj.FullName);
    }

    // --- SET / GO / USE preamble ---

    [Fact]
    public async Task ReadAsync_SkipsSsmsScriptAsPreamble()
    {
        WriteScript("v.sql",
            "SET ANSI_NULLS ON\nGO\nSET QUOTED_IDENTIFIER ON\nGO\nCREATE VIEW [dbo].[V1] AS SELECT 1 AS X\nGO\n");

        var obj = await ReadSingleAsync();

        Assert.Equal(SchemaObjectType.View, obj.Type);
        Assert.Equal("dbo.V1", obj.FullName);
    }

    [Fact]
    public async Task ReadAsync_SkipsUseAndGoWithCountBeforeCreate()
    {
        WriteScript("p.sql",
            "USE [MyDb]\ngo 2\nCREATE PROCEDURE dbo.P1 AS SELECT 1");

        var obj = await ReadSingleAsync();

        Assert.Equal(SchemaObjectType.StoredProcedure, obj.Type);
        Assert.Equal("dbo.P1", obj.FullName);
    }

    [Fact]
    public async Task ReadAsync_SkipsCommentsBetweenSetStatements()
    {
        WriteScript("v.sql",
            "SET ANSI_NULLS ON\n-- generated by SSMS\nSET QUOTED_IDENTIFIER ON\n/* batch */\nGO\nCREATE VIEW dbo.V1 AS SELECT 1 AS X");

        var obj = await ReadSingleAsync();

        Assert.Equal("dbo.V1", obj.FullName);
    }

    [Fact]
    public async Task ReadAsync_FileWithOnlySetAndGo_IsStillSkipped()
    {
        var script = WriteScript("settings.sql",
            "SET ANSI_NULLS ON\nGO\nSET QUOTED_IDENTIFIER ON\nGO\n");

        var result = await ReadAsync();

        Assert.Empty(result.Objects);
        Assert.Equal([script], result.SkippedFiles);
    }

    // --- Definition is the full file content ---

    [Fact]
    public async Task ReadAsync_DefinitionIsWholeFileContent()
    {
        const string content = "-- header\nCREATE VIEW dbo.V1 AS SELECT 1 AS X\n";
        WriteScript("v.sql", content);

        var obj = await ReadSingleAsync();

        Assert.Equal(content, obj.Definition);
    }

    // --- skipped files ---

    [Fact]
    public async Task ReadAsync_FilesWithoutCreateStatement_AreSkipped()
    {
        WriteScript("v.sql", "CREATE VIEW dbo.V1 AS SELECT 1 AS X");
        var dataScript = WriteScript("data.sql", "INSERT INTO dbo.T VALUES (1);\nGRANT SELECT ON dbo.T TO public;");

        var result = await ReadAsync();

        Assert.Single(result.Objects);
        Assert.Equal([dataScript], result.SkippedFiles);
    }

    [Fact]
    public async Task ReadAsync_NonSqlFiles_AreIgnoredEntirely()
    {
        WriteScript("readme.txt", "CREATE VIEW dbo.V1 AS SELECT 1 AS X");

        var result = await ReadAsync();

        Assert.Empty(result.Objects);
        Assert.Empty(result.SkippedFiles);
    }

    // --- duplicates ---

    [Fact]
    public async Task ReadAsync_DuplicateObjectAcrossTwoFiles_ThrowsWithBothPaths()
    {
        var first = WriteScript("a.sql", "CREATE VIEW dbo.V1 AS SELECT 1 AS X");
        var second = WriteScript("b.sql", "CREATE VIEW [dbo].[v1] AS SELECT 2 AS X");

        var ex = await Assert.ThrowsAsync<DuplicateSchemaObjectException>(ReadAsync);

        Assert.Contains("dbo.V1", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(first, ex.Message);
        Assert.Contains(second, ex.Message);
        Assert.Equal(first, ex.FirstFilePath);
        Assert.Equal(second, ex.SecondFilePath);
    }

    // --- folder traversal ---

    [Fact]
    public async Task ReadAsync_ReadsSubfoldersRecursively()
    {
        WriteScript("views/v1.sql", "CREATE VIEW dbo.V1 AS SELECT 1 AS X");
        WriteScript("procs/nested/p1.sql", "CREATE PROCEDURE dbo.P1 AS SELECT 1");

        var result = await ReadAsync();

        Assert.Equal(2, result.Objects.Count);
    }

    [Fact]
    public async Task ReadAsync_MatchesSqlExtensionCaseInsensitively()
    {
        WriteScript("v.SQL", "CREATE VIEW dbo.V1 AS SELECT 1 AS X");

        var obj = await ReadSingleAsync();

        Assert.Equal("dbo.V1", obj.FullName);
    }

    // --- source metadata ---

    [Fact]
    public void Description_ContainsFolderPath()
    {
        var source = new ScriptFolderSource(_root);

        Assert.Contains(_root, source.Description);
    }
}
