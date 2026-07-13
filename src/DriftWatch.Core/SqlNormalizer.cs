using System.Text.RegularExpressions;

namespace DriftWatch.Core;

/// <summary>
/// Normalizes raw SQL object definitions so that cosmetic differences
/// (line endings, trailing whitespace, CREATE vs CREATE OR ALTER) do not
/// register as drift. Does not parse SQL and does not touch comments.
/// </summary>
public static partial class SqlNormalizer
{
    public static string Normalize(string definition, NormalizeOptions options)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(options);

        var lines = definition
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = lines[i].TrimEnd();
        }

        var start = 0;
        var end = lines.Length - 1;
        while (start <= end && lines[start].Length == 0)
        {
            start++;
        }

        while (end >= start && lines[end].Length == 0)
        {
            end--;
        }

        var text = string.Join("\n", lines[start..(end + 1)]);

        // Only at the very start of the definition; "$1$2" keeps the original
        // casing of the CREATE keyword so no case change is introduced.
        return CreateOrAlterRegex().Replace(text, "$1$2", 1);
    }

    [GeneratedRegex(@"\A(\s*)(CREATE)\s+OR\s+ALTER\b", RegexOptions.IgnoreCase)]
    private static partial Regex CreateOrAlterRegex();
}
