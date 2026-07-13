using System.Text.Json;
using System.Text.Json.Serialization;
using DriftWatch.Core;

namespace DriftWatch.Cli;

/// <summary>
/// Serializes a drift report to JSON for --format json. Deliberately
/// excludes object definitions: they can be huge and may contain
/// sensitive logic; the JSON output only carries identities and counts.
/// </summary>
public static class JsonReport
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(
        DriftReport report,
        int sourceObjectCount,
        int targetObjectCount,
        IReadOnlyList<string> skippedFiles,
        IReadOnlyList<EncryptedObjectInfo> encryptedObjects)
    {
        var payload = new
        {
            Summary = new
            {
                SourceObjectCount = sourceObjectCount,
                TargetObjectCount = targetObjectCount,
                OnlyInSourceCount = report.OnlyInSource.Count,
                OnlyInTargetCount = report.OnlyInTarget.Count,
                DifferentCount = report.Different.Count,
                report.HasDrift,
            },
            OnlyInSource = report.OnlyInSource.Select(ToRef),
            OnlyInTarget = report.OnlyInTarget.Select(ToRef),
            Different = report.Different.Select(pair => ToRef(pair.Source)),
            Warnings = new
            {
                SkippedFiles = skippedFiles,
                EncryptedObjects = encryptedObjects.Select(e => new { e.Schema, e.Name, e.Type }),
            },
        };

        return JsonSerializer.Serialize(payload, SerializerOptions);
    }

    private static object ToRef(SchemaObject obj) => new { obj.Schema, obj.Name, obj.Type };
}
