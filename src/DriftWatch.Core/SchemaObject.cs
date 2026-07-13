namespace DriftWatch.Core;

public sealed record SchemaObject(
    string Schema,
    string Name,
    SchemaObjectType Type,
    string Definition)
{
    public string FullName => $"{Schema}.{Name}";
}
