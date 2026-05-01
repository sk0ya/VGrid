using VGrid.Models;

namespace VGrid.VimEngine;

/// <summary>
/// Represents a visual mode selection range
/// </summary>
public record SelectionRange(
    VisualType Type,
    GridPosition Start,
    GridPosition End)
{
    /// <summary>
    /// The starting row index (inclusive, min of Start and End)
    /// </summary>
    public int StartRow => Math.Min(Start.Row, End.Row);

    /// <summary>
    /// The ending row index (inclusive, max of Start and End)
    /// </summary>
    public int EndRow => Math.Max(Start.Row, End.Row);

    /// <summary>
    /// The starting column index (inclusive, min of Start and End)
    /// </summary>
    public int StartColumn => Math.Min(Start.Column, End.Column);

    /// <summary>
    /// The ending column index (inclusive, max of Start and End)
    /// </summary>
    public int EndColumn => Math.Max(Start.Column, End.Column);

    /// <summary>
    /// The number of rows in the selection
    /// </summary>
    public int RowCount => EndRow - StartRow + 1;

    /// <summary>
    /// The number of columns in the selection
    /// </summary>
    public int ColumnCount => EndColumn - StartColumn + 1;
}
