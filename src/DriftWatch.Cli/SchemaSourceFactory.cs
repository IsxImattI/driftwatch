using DriftWatch.Core;

namespace DriftWatch.Cli;

/// <summary>
/// Auto-detects what kind of source a CLI argument describes: an existing
/// directory becomes a <see cref="ScriptFolderSource"/>, anything else is
/// treated as a SQL Server connection string.
/// </summary>
public static class SchemaSourceFactory
{
    public static ISchemaSource Create(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        return Directory.Exists(source)
            ? new ScriptFolderSource(source)
            : new SqlServerSource(source);
    }
}
