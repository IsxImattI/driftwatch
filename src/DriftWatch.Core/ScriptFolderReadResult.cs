namespace DriftWatch.Core;

/// <summary>
/// Result of reading a script folder: the recognized objects plus the
/// .sql files that contained no recognizable CREATE statement.
/// </summary>
public sealed record ScriptFolderReadResult(
    IReadOnlyList<SchemaObject> Objects,
    IReadOnlyList<string> SkippedFiles);
