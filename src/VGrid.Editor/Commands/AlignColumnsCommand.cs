using VGrid.Models;

namespace VGrid.Commands;

/// <summary>
/// Command for aligning all columns by padding cells with spaces to match the maximum width in each column
/// </summary>
public class AlignColumnsCommand : ICommand
{
    private readonly TsvDocument _document;
    private readonly Dictionary<GridPosition, string> _oldValues;
    private readonly Dictionary<GridPosition, string> _newValues;

    public string Description => "Align all columns";

    public AlignColumnsCommand(TsvDocument document)
    {
        _document = document;
        _oldValues = new Dictionary<GridPosition, string>();
        _newValues = new Dictionary<GridPosition, string>();

        CalculateAlignedValues();
    }

    private void CalculateAlignedValues()
    {
        if (_document.RowCount == 0 || _document.ColumnCount == 0)
            return;

        // Calculate maximum width for each column (using display width for CJK characters)
        var maxWidths = new int[_document.ColumnCount];

        for (int col = 0; col < _document.ColumnCount; col++)
        {
            int maxWidth = 0;
            for (int row = 0; row < _document.RowCount; row++)
            {
                var cell = _document.GetCell(row, col);
                if (cell != null)
                {
                    int width = GetDisplayWidth(cell.Value);
                    if (width > maxWidth)
                        maxWidth = width;
                }
            }
            maxWidths[col] = maxWidth;
        }

        // Calculate new padded values for each cell
        for (int row = 0; row < _document.RowCount; row++)
        {
            for (int col = 0; col < _document.ColumnCount; col++)
            {
                var cell = _document.GetCell(row, col);
                if (cell != null)
                {
                    var position = new GridPosition(row, col);
                    string oldValue = cell.Value;
                    string newValue = PadToWidth(oldValue, maxWidths[col]);

                    // Only store if value actually changes
                    if (oldValue != newValue)
                    {
                        _oldValues[position] = oldValue;
                        _newValues[position] = newValue;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets the display width of a string, counting full-width characters as 2
    /// </summary>
    private static int GetDisplayWidth(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        int width = 0;
        foreach (char c in text)
        {
            width += IsFullWidth(c) ? 2 : 1;
        }
        return width;
    }

    /// <summary>
    /// Determines if a character is full-width (CJK, etc.)
    /// </summary>
    private static bool IsFullWidth(char c)
    {
        // Common full-width character ranges:
        // - CJK Unified Ideographs: U+4E00-U+9FFF
        // - CJK Compatibility Ideographs: U+F900-U+FAFF
        // - Hiragana: U+3040-U+309F
        // - Katakana: U+30A0-U+30FF
        // - Full-width ASCII: U+FF00-U+FF5E
        // - Full-width punctuation and symbols: U+FF5F-U+FFEF
        // - CJK Symbols and Punctuation: U+3000-U+303F
        // - Hangul: U+AC00-U+D7AF
        return (c >= '\u4E00' && c <= '\u9FFF') ||   // CJK Unified Ideographs
               (c >= '\uF900' && c <= '\uFAFF') ||   // CJK Compatibility Ideographs
               (c >= '\u3040' && c <= '\u309F') ||   // Hiragana
               (c >= '\u30A0' && c <= '\u30FF') ||   // Katakana
               (c >= '\uFF00' && c <= '\uFFEF') ||   // Full-width forms
               (c >= '\u3000' && c <= '\u303F') ||   // CJK Symbols and Punctuation
               (c >= '\uAC00' && c <= '\uD7AF');     // Hangul
    }

    /// <summary>
    /// Pads a string with spaces to reach the target display width
    /// </summary>
    private static string PadToWidth(string text, int targetWidth)
    {
        int currentWidth = GetDisplayWidth(text);
        int paddingNeeded = targetWidth - currentWidth;

        if (paddingNeeded <= 0)
            return text;

        return text + new string(' ', paddingNeeded);
    }

    public void Execute()
    {
        foreach (var kvp in _newValues)
        {
            _document.SetCell(kvp.Key, kvp.Value);
        }
    }

    public void Undo()
    {
        foreach (var kvp in _oldValues)
        {
            _document.SetCell(kvp.Key, kvp.Value);
        }
    }
}
