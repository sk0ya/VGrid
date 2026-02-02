using System.Text;

namespace VGrid.Services;

/// <summary>
/// Delimiter strategy for Comma-Separated Values (CSV) format.
/// Implements RFC 4180 compliant parsing and serialization.
/// </summary>
public class CsvDelimiterStrategy : IDelimiterStrategy
{
    public DelimiterFormat Format => DelimiterFormat.Csv;
    public char Delimiter => ',';

    public string[] ParseLine(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        int i = 0;

        while (i < line.Length)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Check for escaped quote ""
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                    }
                    else
                    {
                        // End of quoted field
                        inQuotes = false;
                        i++;
                    }
                }
                else
                {
                    field.Append(c);
                    i++;
                }
            }
            else
            {
                if (c == '"' && field.Length == 0)
                {
                    // Start of quoted field
                    inQuotes = true;
                    i++;
                }
                else if (c == ',')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    i++;
                }
                else
                {
                    field.Append(c);
                    i++;
                }
            }
        }

        // Add last field
        fields.Add(field.ToString());

        return fields.ToArray();
    }

    public string FormatLine(IEnumerable<string> values)
    {
        return string.Join(",", values.Select(QuoteField));
    }

    public List<string[]> ParseContent(string content)
    {
        var rows = new List<string[]>();
        if (string.IsNullOrEmpty(content))
            return rows;

        var fields = new List<string>();
        var field = new StringBuilder();
        bool inQuotes = false;
        int i = 0;

        while (i < content.Length)
        {
            char c = content[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i += 2;
                    }
                    else
                    {
                        inQuotes = false;
                        i++;
                    }
                }
                else
                {
                    field.Append(c);
                    i++;
                }
            }
            else
            {
                if (c == '"' && field.Length == 0)
                {
                    inQuotes = true;
                    i++;
                }
                else if (c == ',')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    i++;
                }
                else if (c == '\r')
                {
                    // End of row (\r\n or \r)
                    fields.Add(field.ToString());
                    field.Clear();
                    rows.Add(fields.ToArray());
                    fields.Clear();

                    if (i + 1 < content.Length && content[i + 1] == '\n')
                        i += 2;
                    else
                        i++;
                }
                else if (c == '\n')
                {
                    // End of row (\n)
                    fields.Add(field.ToString());
                    field.Clear();
                    rows.Add(fields.ToArray());
                    fields.Clear();
                    i++;
                }
                else
                {
                    field.Append(c);
                    i++;
                }
            }
        }

        // Add last row if there's remaining content
        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            rows.Add(fields.ToArray());
        }

        return rows;
    }

    /// <summary>
    /// Quotes a field value if it contains special characters (comma, quote, newline)
    /// </summary>
    private static string QuoteField(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
