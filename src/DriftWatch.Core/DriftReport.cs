namespace DriftWatch.Core;

/// <summary>A matched pair of objects whose normalized definitions differ.</summary>
public sealed record DriftPair(SchemaObject Source, SchemaObject Target);

public sealed record DriftReport(
    IReadOnlyList<SchemaObject> OnlyInSource,
    IReadOnlyList<SchemaObject> OnlyInTarget,
    IReadOnlyList<DriftPair> Different)
{
    public bool HasDrift =>
        OnlyInSource.Count > 0 || OnlyInTarget.Count > 0 || Different.Count > 0;
}
