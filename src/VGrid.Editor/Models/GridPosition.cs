namespace VGrid.Models;

/// <summary>
/// Represents a position (cursor location) in the TSV grid
/// </summary>
public record GridPosition(int Row, int Column)
{
    /// <summary>
    /// Checks if this position is valid within the given document
    /// </summary>
    public bool IsValid(TsvDocument document)
    {
        return Row >= 0 && Row < document.RowCount &&
               Column >= 0 && Column < document.ColumnCount;
    }

    /// <summary>
    /// Creates a new position moved up by the specified number of rows
    /// </summary>
    public GridPosition MoveUp(int count = 1) => this with { Row = Math.Max(0, Row - count) };

    /// <summary>
    /// Creates a new position moved down by the specified number of rows
    /// </summary>
    public GridPosition MoveDown(int count = 1) => this with { Row = Row + count };

    /// <summary>
    /// Creates a new position moved left by the specified number of columns
    /// </summary>
    public GridPosition MoveLeft(int count = 1) => this with { Column = Math.Max(0, Column - count) };

    /// <summary>
    /// Creates a new position moved right by the specified number of columns
    /// </summary>
    public GridPosition MoveRight(int count = 1) => this with { Column = Column + count };

    /// <summary>
    /// Creates a new position at the start of the current row
    /// </summary>
    public GridPosition MoveToLineStart() => this with { Column = 0 };

    /// <summary>
    /// Creates a new position at the end of the current row
    /// </summary>
    public GridPosition MoveToLineEnd(TsvDocument document)
    {
        if (Row >= 0 && Row < document.RowCount)
        {
            return this with { Column = Math.Max(0, document.GetRow(Row).CellCount - 1) };
        }
        return this;
    }

    /// <summary>
    /// Creates a new position at the first row
    /// </summary>
    public GridPosition MoveToFirstRow() => this with { Row = 0 };

    /// <summary>
    /// Creates a new position at the last row
    /// </summary>
    public GridPosition MoveToLastRow(TsvDocument document) => this with { Row = Math.Max(0, document.RowCount - 1) };

    /// <summary>
    /// Clamps this position to ensure it's within valid bounds
    /// </summary>
    public GridPosition Clamp(TsvDocument document)
    {
        var clampedRow = Math.Max(0, Math.Min(Row, document.RowCount - 1));
        var clampedCol = Math.Max(0, Math.Min(Column, document.ColumnCount - 1));
        return new GridPosition(clampedRow, clampedCol);
    }
}
