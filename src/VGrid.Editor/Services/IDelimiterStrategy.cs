namespace VGrid.Services;

/// <summary>
/// Represents the delimiter format used for file I/O
/// </summary>
public enum DelimiterFormat
{
    /// <summary>Tab-separated values</summary>
    Tsv,
    /// <summary>Comma-separated values (RFC 4180)</summary>
    Csv,
}

/// <summary>
/// Strategy interface for parsing and formatting delimited text
/// </summary>
public interface IDelimiterStrategy
{
    /// <summary>
    /// The delimiter format this strategy handles
    /// </summary>
    DelimiterFormat Format { get; }

    /// <summary>
    /// The primary delimiter character
    /// </summary>
    char Delimiter { get; }

    /// <summary>
    /// Splits a single line into cell values, handling format-specific escaping
    /// </summary>
    string[] ParseLine(string line);

    /// <summary>
    /// Joins cell values into a single line, applying format-specific escaping
    /// </summary>
    string FormatLine(IEnumerable<string> values);

    /// <summary>
    /// Parses full file content into rows of cell values.
    /// Needed for formats like CSV where quoted fields can contain newlines.
    /// </summary>
    List<string[]> ParseContent(string content);
}
