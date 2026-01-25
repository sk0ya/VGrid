using System.IO;
using System.Text;
using VGrid.Models;

namespace VGrid.Services;

/// <summary>
/// Service for loading and saving TSV (Tab-Separated Values) files
/// </summary>
public class TsvFileService : ITsvFileService
{
    private const char TabDelimiter = '\t';

    /// <summary>
    /// Loads a TSV document from the specified file path
    /// </summary>
    public async Task<TsvDocument> LoadAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var rows = new List<Row>();
        var lines = await File.ReadAllLinesAsync(filePath, Encoding.UTF8);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            // Split by tab delimiter
            var values = line.Split(TabDelimiter);
            rows.Add(new Row(i, values));
        }

        // Create document
        var document = new TsvDocument(rows)
        {
            FilePath = filePath,
            IsDirty = false
        };

        // Only ensure minimal extra space beyond actual data
        // Grid expands on demand when user navigates beyond current bounds
        int minRows = Math.Max(document.RowCount + 5, 20);
        int minCols = Math.Max(document.ColumnCount + 3, 15);
        document.EnsureSize(minRows, minCols);

        return document;
    }

    /// <summary>
    /// Saves the TSV document to the specified file path
    /// </summary>
    public async Task SaveAsync(TsvDocument document, string filePath)
    {
        var lines = new List<string>();

        // Find the last non-empty row
        int lastNonEmptyRow = -1;
        for (int i = document.Rows.Count - 1; i >= 0; i--)
        {
            if (document.Rows[i].Cells.Any(c => !string.IsNullOrEmpty(c.Value)))
            {
                lastNonEmptyRow = i;
                break;
            }
        }

        // Only save rows up to the last non-empty row
        for (int i = 0; i <= lastNonEmptyRow; i++)
        {
            var row = document.Rows[i];

            // Find the last non-empty column in this row
            int lastNonEmptyCol = -1;
            for (int j = row.Cells.Count - 1; j >= 0; j--)
            {
                if (!string.IsNullOrEmpty(row.Cells[j].Value))
                {
                    lastNonEmptyCol = j;
                    break;
                }
            }

            // Create line with cells up to last non-empty column
            if (lastNonEmptyCol >= 0)
            {
                var cellValues = row.Cells.Take(lastNonEmptyCol + 1).Select(c => c.Value ?? string.Empty);
                var line = string.Join(TabDelimiter.ToString(), cellValues);
                lines.Add(line);
            }
            else
            {
                // Empty row
                lines.Add(string.Empty);
            }
        }

        // Write to file
        await File.WriteAllLinesAsync(filePath, lines, Encoding.UTF8);

        // Update document state
        document.FilePath = filePath;
        document.IsDirty = false;
    }

    /// <summary>
    /// Validates whether the file at the specified path is a valid TSV file
    /// </summary>
    public bool ValidateTsv(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            // Check file extension
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            if (extension != ".tsv" && extension != ".txt" && extension != ".tab")
            {
                return false;
            }

            // Try to read first few lines to verify it's readable
            using var reader = new StreamReader(filePath, Encoding.UTF8);
            for (int i = 0; i < 5 && !reader.EndOfStream; i++)
            {
                reader.ReadLine();
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
}
