namespace DriftWatch.Core;

/// <summary>
/// Compares two sets of schema objects. Objects are matched by
/// FullName + Type (name comparison is always case-insensitive);
/// definitions are normalized before comparison. A duplicate key within
/// either list is an error — callers must de-duplicate (or report) first.
/// </summary>
public static class DriftComparer
{
    public static DriftReport Compare(
        IReadOnlyList<SchemaObject> source,
        IReadOnlyList<SchemaObject> target,
        NormalizeOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(options);

        var targetByKey = new Dictionary<string, SchemaObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var obj in target)
        {
            if (!targetByKey.TryAdd(Key(obj), obj))
            {
                throw new InvalidOperationException(
                    $"Duplicate object '{Key(obj)}' in target.");
            }
        }

        var onlyInSource = new List<SchemaObject>();
        var different = new List<DriftPair>();
        var sourceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matchedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var definitionComparison = options.IgnoreCase
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        foreach (var sourceObj in source)
        {
            var key = Key(sourceObj);
            if (!sourceKeys.Add(key))
            {
                throw new InvalidOperationException(
                    $"Duplicate object '{key}' in source.");
            }
            if (!targetByKey.TryGetValue(key, out var targetObj))
            {
                onlyInSource.Add(sourceObj);
                continue;
            }

            matchedKeys.Add(key);

            var sourceDefinition = SqlNormalizer.Normalize(sourceObj.Definition, options);
            var targetDefinition = SqlNormalizer.Normalize(targetObj.Definition, options);
            if (!string.Equals(sourceDefinition, targetDefinition, definitionComparison))
            {
                different.Add(new DriftPair(sourceObj, targetObj));
            }
        }

        var onlyInTarget = target.Where(obj => !matchedKeys.Contains(Key(obj))).ToList();

        return new DriftReport(onlyInSource, onlyInTarget, different);
    }

    private static string Key(SchemaObject obj) => $"{obj.Type}:{obj.FullName}";
}
