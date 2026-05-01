namespace VGrid.Services;

/// <summary>
/// Delimiter strategy for Tab-Separated Values (TSV) format
/// </summary>
public class TsvDelimiterStrategy : IDelimiterStrategy
{
    public DelimiterFormat Format => DelimiterFormat.Tsv;
    public char Delimiter => '\t';

    public string[] ParseLine(string line)
    {
        return line.Split('\t');
    }

    public string FormatLine(IEnumerable<string> values)
    {
        return string.Join("\t", values);
    }

    public List<string[]> ParseContent(string content)
    {
        var rows = new List<string[]>();
        if (string.IsNullOrEmpty(content))
            return rows;

        var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        // Remove trailing empty line if content ends with newline
        if (lines.Length > 0 && string.IsNullOrEmpty(lines[^1]))
        {
            lines = lines[..^1];
        }

        foreach (var line in lines)
        {
            rows.Add(ParseLine(line));
        }

        return rows;
    }
}
