namespace DriftWatch.Core;

/// <summary>
/// Identity of an object whose definition is not readable because it was
/// created WITH ENCRYPTION (sys.sql_modules.definition is NULL).
/// </summary>
public sealed record EncryptedObjectInfo(string Schema, string Name, SchemaObjectType Type)
{
    public string FullName => $"{Schema}.{Name}";
}

/// <summary>
/// Result of reading a SQL Server instance: the readable objects plus the
/// objects that were skipped because their definition is encrypted.
/// </summary>
public sealed record SqlServerReadResult(
    IReadOnlyList<SchemaObject> Objects,
    IReadOnlyList<EncryptedObjectInfo> EncryptedObjects);
