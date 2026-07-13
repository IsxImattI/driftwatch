namespace DriftWatch.Core;

/// <summary>
/// A source of schema objects to compare — a live SQL Server instance
/// or a folder of .sql scripts.
/// </summary>
public interface ISchemaSource
{
    /// <summary>Human-friendly description of the source (e.g. a folder path),
    /// for output and error messages.</summary>
    string Description { get; }

    Task<IReadOnlyList<SchemaObject>> ReadAsync(CancellationToken ct);
}
