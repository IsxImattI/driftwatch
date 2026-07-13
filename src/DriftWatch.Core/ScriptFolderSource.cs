using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace DriftWatch.Core;

/// <summary>
/// Reads schema objects from a folder of .sql scripts (recursively).
/// The object type and name are extracted with a regex from the first
/// CREATE [OR ALTER] statement of each file — leading line and block
/// comments (copyright headers) are skipped, but the SQL is not parsed.
/// </summary>
public sealed partial class ScriptFolderSource : ISchemaSource
{
    private readonly string _folderPath;

    public ScriptFolderSource(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);
        _folderPath = Path.GetFullPath(folderPath);
    }

    public string Description => $"script folder '{_folderPath}'";

    public async Task<IReadOnlyList<SchemaObject>> ReadAsync(CancellationToken ct) =>
        (await ReadDetailedAsync(ct).ConfigureAwait(false)).Objects;

    /// <summary>
    /// Like <see cref="ReadAsync"/> but also reports which files were
    /// skipped. Returned as a per-call result instead of instance state so
    /// the source stays stateless and safe to read more than once.
    /// </summary>
    public async Task<ScriptFolderReadResult> ReadDetailedAsync(CancellationToken ct)
    {
        var files = Directory.EnumerateFiles(_folderPath, "*", SearchOption.AllDirectories)
            .Where(f => string.Equals(Path.GetExtension(f), ".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var objects = new List<SchemaObject>();
        var skippedFiles = new List<string>();
        var fileByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var content = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);

            if (!TryExtract(content, out var schemaObject))
            {
                skippedFiles.Add(file);
                continue;
            }

            var key = $"{schemaObject.Type}:{schemaObject.FullName}";
            if (fileByKey.TryGetValue(key, out var existingFile))
            {
                throw new DuplicateSchemaObjectException(key, existingFile, file);
            }

            fileByKey.Add(key, file);
            objects.Add(schemaObject);
        }

        return new ScriptFolderReadResult(objects, skippedFiles);
    }

    private static bool TryExtract(string content, [NotNullWhen(true)] out SchemaObject? schemaObject)
    {
        schemaObject = null;

        var match = CreateStatementRegex().Match(content);
        if (!match.Success)
        {
            return false;
        }

        var schema = match.Groups["schema"].Success ? match.Groups["schema"].Value : "dbo";
        var name = match.Groups["name"].Value;

        var type = match.Groups["type"].Value.ToUpperInvariant() switch
        {
            "VIEW" => SchemaObjectType.View,
            "TRIGGER" => SchemaObjectType.Trigger,
            "FUNCTION" => ReturnsTableRegex().IsMatch(content)
                ? SchemaObjectType.TableFunction
                : SchemaObjectType.ScalarFunction,
            _ => SchemaObjectType.StoredProcedure, // PROCEDURE / PROC
        };

        schemaObject = new SchemaObject(schema, name, type, content);
        return true;
    }

    // Anchored at the start of the file: skips a preamble of whitespace,
    // -- and /* */ comments, SET statements, GO batch separators (with an
    // optional repeat count) and USE statements — in any order — then
    // requires the CREATE [OR ALTER] statement. Identifiers may be
    // [bracketed], "quoted" or plain; the schema part is optional.
    [GeneratedRegex(
        """
        \A(?:\s+
           |--[^\n]*(?:\n|\z)
           |/\*.*?\*/
           |SET\s[^\n;]*(?:;|\n|\z)
           |GO(?:[ \t]+\d+)?[ \t]*(?:\n|\z)
           |USE\s+(?:\[[^\]]+\]|"[^"]+"|[A-Za-z_@#][A-Za-z0-9_@#$]*)\s*;?
        )*
        CREATE(?:\s+OR\s+ALTER)?\s+
        (?<type>VIEW|PROCEDURE|PROC|FUNCTION|TRIGGER)\s+
        (?:(?:\[(?<schema>[^\]]+)\]|"(?<schema>[^"]+)"|(?<schema>[A-Za-z_@#][A-Za-z0-9_@#$]*))\s*\.\s*)?
        (?:\[(?<name>[^\]]+)\]|"(?<name>[^"]+)"|(?<name>[A-Za-z_@#][A-Za-z0-9_@#$]*))
        """,
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace)]
    private static partial Regex CreateStatementRegex();

    // RETURNS TABLE (inline) or RETURNS @var TABLE (multi-statement TVF).
    [GeneratedRegex(
        @"RETURNS\s+(?:@[A-Za-z0-9_@#$]+\s+)?TABLE\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex ReturnsTableRegex();
}
