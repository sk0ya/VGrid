using System.IO;

namespace VGrid.Services;

/// <summary>
/// Factory for creating delimiter strategies based on format or file extension
/// </summary>
public static class DelimiterStrategyFactory
{
    /// <summary>
    /// Creates a delimiter strategy for the specified format
    /// </summary>
    public static IDelimiterStrategy Create(DelimiterFormat format) => format switch
    {
        DelimiterFormat.Csv => new CsvDelimiterStrategy(),
        _ => new TsvDelimiterStrategy(),
    };

    /// <summary>
    /// Detects the delimiter format from the file extension
    /// </summary>
    public static DelimiterFormat DetectFromExtension(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".csv" => DelimiterFormat.Csv,
            _ => DelimiterFormat.Tsv,
        };
    }
}
