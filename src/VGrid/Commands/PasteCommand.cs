using VGrid.Models;
using VGrid.VimEngine;

namespace VGrid.Commands;

/// <summary>
/// Command for pasting content with undo support
/// Handles line-wise, block-wise, and character-wise paste operations
/// </summary>
public class PasteCommand : ICommand
{
    private readonly TsvDocument _document;
    private readonly GridPosition _startPosition;
    private readonly YankedContent _content;
    private readonly VisualType _pasteType;
    private readonly bool _pasteBefore;

    // For undo: store old values and inserted row/column indices
    private readonly List<(GridPosition Position, string OldValue)> _oldCellValues = new();
    private readonly List<int> _insertedRowIndices = new();
    private readonly List<int> _insertedColumnIndices = new();
    private readonly int _originalRowCount;
    private readonly int _originalColumnCount;
    private readonly HashSet<int> _affectedColumns = new();

    public string Description => $"Paste {_pasteType} {(_pasteBefore ? "before" : "after")} ({_startPosition.Row}, {_startPosition.Column})";

    /// <summary>
    /// Gets the column indices that were affected by the paste operation
    /// </summary>
    public IEnumerable<int> AffectedColumns => _affectedColumns;

    public PasteCommand(TsvDocument document, GridPosition startPosition, YankedContent content, bool pasteBefore = false)
    {
        _document = document;
        _startPosition = startPosition;
        _content = content;
        _pasteType = content.SourceType;
        _pasteBefore = pasteBefore;
        _originalRowCount = document.RowCount;
        _originalColumnCount = document.ColumnCount;
    }

    public void Execute()
    {
        _oldCellValues.Clear();
        _insertedRowIndices.Clear();
        _insertedColumnIndices.Clear();
        _affectedColumns.Clear();

        switch (_pasteType)
        {
            case VisualType.Line:
                ExecuteLinePaste();
                break;
            case VisualType.Block:
                ExecuteBlockPaste();
                break;
            default: // VisualType.Character
                ExecuteCharacterPaste();
                break;
        }
    }

    private void ExecuteLinePaste()
    {
        // Insert new rows below (p) or above (P) the current row
        int insertOffset = _pasteBefore ? 0 : 1;
        for (int r = 0; r < _content.Rows; r++)
        {
            int insertRow = _startPosition.Row + insertOffset + r;
            _document.InsertRow(insertRow);
            _insertedRowIndices.Add(insertRow);

            // Fill the new row with yanked values
            var row = _document.Rows[insertRow];
            for (int c = 0; c < _content.Columns && c < row.Cells.Count; c++)
            {
                row.Cells[c].Value = _content.Values[r, c];
                _affectedColumns.Add(c);
            }
        }
    }

    private void ExecuteBlockPaste()
    {
        // Insert new columns to the right (p) or left (P) of the current column
        int insertOffset = _pasteBefore ? 0 : 1;
        for (int c = 0; c < _content.Columns; c++)
        {
            int insertCol = _startPosition.Column + insertOffset + c;
            _document.InsertColumn(insertCol);
            _insertedColumnIndices.Add(insertCol);
            _affectedColumns.Add(insertCol);

            // Fill the new column with yanked values
            for (int r = 0; r < _content.Rows && r < _document.RowCount; r++)
            {
                var row = _document.Rows[r];
                if (insertCol < row.Cells.Count)
                {
                    row.Cells[insertCol].Value = _content.Values[r, c];
                }
            }
        }
    }

    private void ExecuteCharacterPaste()
    {
        // For character paste, P pastes at current position, p pastes at current position
        // (Unlike line/block paste where position differs)
        int columnOffset = _pasteBefore ? 0 : 0; // Both paste at current position for character mode

        // Ensure document has enough rows and columns
        int neededRows = _startPosition.Row + _content.Rows;
        int neededCols = _startPosition.Column + columnOffset + _content.Columns;
        _document.EnsureSize(neededRows, Math.Max(neededCols, _document.ColumnCount));

        // Save old values and paste new values
        for (int r = 0; r < _content.Rows; r++)
        {
            for (int c = 0; c < _content.Columns; c++)
            {
                int targetRow = _startPosition.Row + r;
                int targetCol = _startPosition.Column + columnOffset + c;

                if (targetRow < _document.RowCount && targetCol < _document.Rows[targetRow].Cells.Count)
                {
                    var cell = _document.Rows[targetRow].Cells[targetCol];

                    // Store old value for undo
                    _oldCellValues.Add((new GridPosition(targetRow, targetCol), cell.Value));

                    // Paste the new value
                    cell.Value = _content.Values[r, c];
                    _affectedColumns.Add(targetCol);
                }
            }
        }
    }

    public void Undo()
    {
        switch (_pasteType)
        {
            case VisualType.Line:
                UndoLinePaste();
                break;
            case VisualType.Block:
                UndoBlockPaste();
                break;
            default: // VisualType.Character
                UndoCharacterPaste();
                break;
        }
    }

    private void UndoLinePaste()
    {
        // Delete inserted rows in reverse order (to maintain indices)
        for (int i = _insertedRowIndices.Count - 1; i >= 0; i--)
        {
            int rowIndex = _insertedRowIndices[i];
            if (rowIndex < _document.RowCount)
            {
                _document.DeleteRow(rowIndex);
            }
        }
    }

    private void UndoBlockPaste()
    {
        // Delete inserted columns in reverse order (to maintain indices)
        for (int i = _insertedColumnIndices.Count - 1; i >= 0; i--)
        {
            int colIndex = _insertedColumnIndices[i];
            if (colIndex < _document.ColumnCount)
            {
                _document.DeleteColumn(colIndex);
            }
        }
    }

    private void UndoCharacterPaste()
    {
        // Restore old cell values
        foreach (var (position, oldValue) in _oldCellValues)
        {
            if (position.Row < _document.RowCount &&
                position.Column < _document.Rows[position.Row].Cells.Count)
            {
                _document.Rows[position.Row].Cells[position.Column].Value = oldValue;
            }
        }

        // Remove any rows/columns that were added by EnsureSize
        while (_document.RowCount > _originalRowCount)
        {
            _document.DeleteRow(_document.RowCount - 1);
        }

        while (_document.ColumnCount > _originalColumnCount)
        {
            _document.DeleteColumn(_document.ColumnCount - 1);
        }
    }
}
