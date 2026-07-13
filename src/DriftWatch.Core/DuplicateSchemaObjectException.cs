namespace DriftWatch.Core;

/// <summary>
/// Thrown when two script files define the same object (Type:FullName).
/// </summary>
public sealed class DuplicateSchemaObjectException : Exception
{
    public string Key { get; }
    public string FirstFilePath { get; }
    public string SecondFilePath { get; }

    public DuplicateSchemaObjectException(string key, string firstFilePath, string secondFilePath)
        : base($"Duplicate object '{key}' is defined in both '{firstFilePath}' and '{secondFilePath}'.")
    {
        Key = key;
        FirstFilePath = firstFilePath;
        SecondFilePath = secondFilePath;
    }
}
