namespace DriftWatch.Core;

/// <summary>
/// Options controlling normalization and comparison of SQL definitions.
/// </summary>
/// <param name="IgnoreCase">
/// When true, definitions are compared case-insensitively. Normalization
/// itself never changes the casing of a definition (string literals must
/// stay intact); this flag only tells the comparer how to compare.
/// </param>
public sealed record NormalizeOptions(bool IgnoreCase = false)
{
    public static NormalizeOptions Default { get; } = new();
}
