using System;
using System.Text;
using System.Windows;

namespace VGrid.VimEngine;

/// <summary>
/// Helper class for clipboard operations with TSV format conversion
/// </summary>
public static class ClipboardHelper
{
    private static string? _lastClipboardContent;

    /// <summary>
    /// Copies YankedContent to the system clipboard in TSV format
    /// </summary>
    public static void CopyToClipboard(YankedContent? yank)
    {
        if (yank == null || yank.Values == null)
            return;

        try
        {
            string tsvText = ConvertToTsv(yank);
            System.Windows.Clipboard.SetText(tsvText);
            // Remember what we set to the clipboard
            _lastClipboardContent = tsvText;

            string preview = tsvText.Length > 100 ? tsvText.Substring(0, 100) + "..." : tsvText;
            System.Diagnostics.Debug.WriteLine($"[ClipboardHelper] CopyToClipboard: Set _lastClipboardContent (length: {tsvText.Length})");
            System.Diagnostics.Debug.WriteLine($"[ClipboardHelper]   Content: [{preview}]");
        }
        catch (Exception ex)
        {
            // Ignore clipboard errors (e.g., clipboard in use by another process)
            System.Diagnostics.Debug.WriteLine($"[ClipboardHelper] CopyToClipboard: Error - {ex.Message}");
        }
    }

    /// <summary>
    /// Reads TSV text from the system clipboard and converts it to YankedContent
    /// </summary>
    public static YankedContent? ReadFromClipboard()
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsText())
                return null;

            string text = System.Windows.Clipboard.GetText();
            return ConvertFromTsv(text);
        }
        catch (Exception)
        {
            // Ignore clipboard errors
            return null;
        }
    }

    /// <summary>
    /// Checks if the clipboard content has changed from what we last set
    /// </summary>
    public static bool HasClipboardChangedExternally()
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsText())
            {
                // If clipboard doesn't contain text and we had set something, it changed
                bool changed = _lastClipboardContent != null;
                System.Diagnostics.Debug.WriteLine($"[ClipboardHelper] HasClipboardChangedExternally: No text in clipboard. _lastClipboardContent is null? {_lastClipboardContent == null}. Result: {changed}");
                return changed;
            }

            string currentContent = System.Windows.Clipboard.GetText();

            // If we never set anything, any content is considered external
            if (_lastClipboardContent == null)
            {
                bool result = !string.IsNullOrEmpty(currentContent);
                System.Diagnostics.Debug.WriteLine($"[ClipboardHelper] HasClipboardChangedExternally: _lastClipboardContent is null. Current content length: {currentContent.Length}. Result: {result}");
                return result;
            }

            // Check if current content differs from what we last set
            bool differs = currentContent != _lastClipboardContent;

            // Show first 100 chars for debugging
            string lastPreview = _lastClipboardContent.Length > 100 ? _lastClipboardContent.Substring(0, 100) + "..." : _lastClipboardContent;
            string currentPreview = currentContent.Length > 100 ? currentContent.Substring(0, 100) + "..." : currentContent;

            System.Diagnostics.Debug.WriteLine($"[ClipboardHelper] HasClipboardChangedExternally: Comparing.");
            System.Diagnostics.Debug.WriteLine($"  Last ({_lastClipboardContent.Length} chars): [{lastPreview}]");
            System.Diagnostics.Debug.WriteLine($"  Current ({currentContent.Length} chars): [{currentPreview}]");
            System.Diagnostics.Debug.WriteLine($"  Differs: {differs}");
            return differs;
        }
        catch (Exception ex)
        {
            // If we can't access clipboard, assume no change
            System.Diagnostics.Debug.WriteLine($"[ClipboardHelper] HasClipboardChangedExternally: Error - {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Resets the tracked clipboard content
    /// </summary>
    public static void ResetTracking()
    {
        _lastClipboardContent = null;
    }

    /// <summary>
    /// Converts YankedContent to TSV format string
    /// </summary>
    private static string ConvertToTsv(YankedContent yank)
    {
        var sb = new StringBuilder();

        for (int r = 0; r < yank.Rows; r++)
        {
            for (int c = 0; c < yank.Columns; c++)
            {
                if (c > 0)
                    sb.Append('\t');

                string value = yank.Values[r, c] ?? string.Empty;
                sb.Append(value);
            }

            // Don't add newline after the last row
            if (r < yank.Rows - 1)
                sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Converts TSV format string to YankedContent
    /// </summary>
    private static YankedContent ConvertFromTsv(string tsvText)
    {
        if (string.IsNullOrEmpty(tsvText))
        {
            return new YankedContent
            {
                Values = new string[0, 0],
                SourceType = VisualType.Character,
                Rows = 0,
                Columns = 0
            };
        }

        // Split by newlines (handle both \r\n and \n)
        string[] lines = tsvText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        // Remove trailing empty line if present (from final newline)
        if (lines.Length > 0 && string.IsNullOrEmpty(lines[lines.Length - 1]))
        {
            Array.Resize(ref lines, lines.Length - 1);
        }

        if (lines.Length == 0)
        {
            return new YankedContent
            {
                Values = new string[0, 0],
                SourceType = VisualType.Character,
                Rows = 0,
                Columns = 0
            };
        }

        // Determine column count (max across all rows)
        int maxColumns = 0;
        var rowData = new string[lines.Length][];
        for (int i = 0; i < lines.Length; i++)
        {
            rowData[i] = lines[i].Split('\t');
            maxColumns = Math.Max(maxColumns, rowData[i].Length);
        }

        // Create 2D array and fill it
        string[,] values = new string[lines.Length, maxColumns];
        for (int r = 0; r < lines.Length; r++)
        {
            for (int c = 0; c < maxColumns; c++)
            {
                if (c < rowData[r].Length)
                    values[r, c] = rowData[r][c];
                else
                    values[r, c] = string.Empty;
            }
        }

        // Determine visual type based on content structure
        VisualType sourceType = VisualType.Character;
        if (lines.Length == 1 && maxColumns == 1)
        {
            // Single cell - character type
            sourceType = VisualType.Character;
        }
        else if (maxColumns == 1)
        {
            // Single column, multiple rows - could be line-wise
            sourceType = VisualType.Character;
        }
        else
        {
            // Multiple rows/columns - character type
            sourceType = VisualType.Character;
        }

        return new YankedContent
        {
            Values = values,
            SourceType = sourceType,
            Rows = lines.Length,
            Columns = maxColumns
        };
    }
}
