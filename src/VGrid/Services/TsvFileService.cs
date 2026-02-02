using System.IO;
using System.Text;
using VGrid.Models;

namespace VGrid.Services;

/// <summary>
/// Service for loading and saving delimited text files (TSV, CSV, etc.)
/// </summary>
public class TsvFileService : ITsvFileService
{
    /// <summary>
    /// Loads a document from the specified file path
    /// </summary>
    public async Task<TsvDocument> LoadAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        var format = DelimiterStrategyFactory.DetectFromExtension(filePath);
        var strategy = DelimiterStrategyFactory.Create(format);

        var rows = new List<Row>();
        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        var parsedRows = strategy.ParseContent(content);

        for (int i = 0; i < parsedRows.Count; i++)
        {
            rows.Add(new Row(i, parsedRows[i]));
        }

        // Create document
        var document = new TsvDocument(rows)
        {
            FilePath = filePath,
            IsDirty = false,
            DelimiterFormat = format
        };

        // Only ensure minimal extra space beyond actual data
        // Grid expands on demand when user navigates beyond current bounds
        int minRows = Math.Max(document.RowCount + 5, 20);
        int minCols = Math.Max(document.ColumnCount + 3, 15);
        document.EnsureSize(minRows, minCols);

        return document;
    }

    /// <summary>
    /// Saves the document to the specified file path
    /// </summary>
    public async Task SaveAsync(TsvDocument document, string filePath)
    {
        var strategy = DelimiterStrategyFactory.Create(document.DelimiterFormat);
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
                var line = strategy.FormatLine(cellValues);
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
    /// Validates whether the file at the specified path is a valid delimited text file
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
            if (extension != ".tsv" && extension != ".txt" && extension != ".tab" && extension != ".csv")
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
