using VGrid.Models;
using VGrid.VimEngine;

namespace VGrid.Commands;

/// <summary>
/// Command for pasting content over a selected range with undo support
/// Fills the entire selection with the yanked content
/// </summary>
public class PasteOverSelectionCommand : ICommand
{
    private readonly TsvDocument _document;
    private readonly VimEngine.SelectionRange _selection;
    private readonly YankedContent _content;
    private readonly List<(GridPosition Position, string OldValue)> _oldCellValues = new();
    private readonly HashSet<int> _affectedColumns = new();

    public string Description => $"Paste over selection at ({_selection.StartRow}, {_selection.StartColumn})";

    /// <summary>
    /// Gets the column indices that were affected by the paste operation
    /// </summary>
    public IEnumerable<int> AffectedColumns => _affectedColumns;

    public PasteOverSelectionCommand(TsvDocument document, VimEngine.SelectionRange selection, YankedContent content)
    {
        _document = document;
        _selection = selection;
        _content = content;
    }

    public void Execute()
    {
        _oldCellValues.Clear();
        _affectedColumns.Clear();

        // For character-wise selection, paste over each cell in the selection
        if (_selection.Type == VisualType.Character)
        {
            // Calculate the actual paste range:
            // - If selection is larger than content, repeat the content to fill selection
            // - If content is larger than selection, expand paste area to fit content dimensions
            // For example: 1x3 content into 3x1 selection should create a 3x3 paste area
            int pasteRowCount = Math.Max(_selection.RowCount, _content.Rows);
            int pasteColCount = Math.Max(_selection.ColumnCount, _content.Columns);

            // If selection is narrower than content, expand to content width
            // If selection is taller than content, use selection height
            if (_selection.RowCount > 1 && _selection.ColumnCount == 1 && _content.Columns > 1)
            {
                // Special case: vertical selection (Nx1) with horizontal content (1xM)
                // Paste content to each row: create NxM area
                pasteRowCount = _selection.RowCount;
                pasteColCount = _content.Columns;
            }
            else if (_selection.RowCount == 1 && _selection.ColumnCount > 1 && _content.Rows > 1)
            {
                // Special case: horizontal selection (1xN) with vertical content (Mx1)
                // Paste content to each column: create MxN area
                pasteRowCount = _content.Rows;
                pasteColCount = _selection.ColumnCount;
            }

            // Iterate through the calculated paste area
            for (int r = 0; r < pasteRowCount; r++)
            {
                for (int c = 0; c < pasteColCount; c++)
                {
                    int targetRow = _selection.StartRow + r;
                    int targetCol = _selection.StartColumn + c;

                    if (targetRow < _document.RowCount && targetCol < _document.Rows[targetRow].Cells.Count)
                    {
                        var cell = _document.Rows[targetRow].Cells[targetCol];

                        // Store old value for undo
                        _oldCellValues.Add((new GridPosition(targetRow, targetCol), cell.Value));

                        // Paste the new value - use modulo to repeat pattern if needed
                        int yankRow = r % _content.Rows;
                        int yankCol = c % _content.Columns;
                        cell.Value = _content.Values[yankRow, yankCol];
                        _affectedColumns.Add(targetCol);
                    }
                }
            }
        }
        else if (_selection.Type == VisualType.Line)
        {
            // For line-wise, paste to all cells in selected rows
            for (int r = 0; r < _selection.RowCount; r++)
            {
                int targetRow = _selection.StartRow + r;
                if (targetRow >= _document.RowCount)
                    continue;

                var row = _document.Rows[targetRow];
                for (int c = 0; c < row.Cells.Count; c++)
                {
                    var cell = row.Cells[c];

                    // Store old value for undo
                    _oldCellValues.Add((new GridPosition(targetRow, c), cell.Value));

                    // Paste the new value - use modulo to repeat pattern if needed
                    int yankRow = r % _content.Rows;
                    int yankCol = c % _content.Columns;
                    cell.Value = _content.Values[yankRow, yankCol];
                    _affectedColumns.Add(c);
                }
            }
        }
        else if (_selection.Type == VisualType.Block)
        {
            // For block-wise, paste to all cells in selected columns
            for (int c = 0; c < _selection.ColumnCount; c++)
            {
                int targetCol = _selection.StartColumn + c;
                _affectedColumns.Add(targetCol);

                for (int r = 0; r < _document.RowCount; r++)
                {
                    var row = _document.Rows[r];
                    if (targetCol >= row.Cells.Count)
                        continue;

                    var cell = row.Cells[targetCol];

                    // Store old value for undo
                    _oldCellValues.Add((new GridPosition(r, targetCol), cell.Value));

                    // Paste the new value - use modulo to repeat pattern if needed
                    int yankRow = r % _content.Rows;
                    int yankCol = c % _content.Columns;
                    cell.Value = _content.Values[yankRow, yankCol];
                }
            }
        }
    }

    public void Undo()
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
    }
}
