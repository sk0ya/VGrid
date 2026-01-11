using System.Windows.Input;
using VGrid.Commands;
using VGrid.Models;

namespace VGrid.VimEngine;

/// <summary>
/// Implements Vim Visual mode behavior
/// </summary>
public class VisualMode : IVimMode
{
    private GridPosition? _selectionStart;
    private VisualType _visualType = VisualType.Character;
    private bool _isFirstKey = true;

    public string GetModeName() => _visualType switch
    {
        VisualType.Line => "VISUAL LINE",
        VisualType.Block => "VISUAL BLOCK",
        _ => "VISUAL"
    };

    public void OnEnter(VimState state)
    {
        // Store the starting position of the selection
        _selectionStart = state.CursorPosition;

        // Determine visual type from CurrentSelection
        _visualType = state.CurrentSelection?.Type ?? VisualType.Character;

        // Mark as first key to ensure initial selection is displayed
        _isFirstKey = true;

        // Note: Initial cell selection will be set when HandleKey is first called
        // We cannot access document here as it's not passed to OnEnter
    }

    public void OnExit(VimState state)
    {
        // Clear selection state
        state.CurrentSelection = null;
        _selectionStart = null;

        // Clear header selections when exiting Visual mode
        state.ClearRowSelections();
        state.ClearColumnSelections();

        // Note: Cell.IsSelected flags will be cleared by the UI layer
        // when it detects the mode change
    }

    public bool HandleKey(VimState state, Key key, ModifierKeys modifiers, TsvDocument document)
    {
        // On first key press after entering Visual mode, update selection immediately
        if (_isFirstKey)
        {
            _isFirstKey = false;
            UpdateSelection(state, document);
        }

        // Escape returns to normal mode
        if (key == Key.Escape)
        {
            state.SwitchMode(VimMode.Normal);
            return true;
        }

        // Handle movement keys
        bool moved = key switch
        {
            Key.H => MoveSelection(state, document, state.CursorPosition.MoveLeft(1)),
            Key.J => MoveSelection(state, document, state.CursorPosition.MoveDown(1)),
            Key.K => MoveSelection(state, document, state.CursorPosition.MoveUp(1)),
            Key.L => MoveSelection(state, document, state.CursorPosition.MoveRight(1)),
            _ => false
        };

        if (moved)
            return true;

        // Handle operations on selection
        return key switch
        {
            // Delete selection (not implemented yet - will be added in later phases)
            Key.D => DeleteSelection(state, document),
            // Yank selection (not implemented yet - will be added in later phases)
            Key.Y => YankSelection(state, document),
            _ => false
        };
    }

    private bool MoveSelection(VimState state, TsvDocument document, GridPosition newPosition)
    {
        var clampedPos = newPosition.Clamp(document);

        // Update cursor position
        state.CursorPosition = clampedPos;

        // Update selection
        UpdateSelection(state, document);

        return true;
    }

    private void UpdateSelection(VimState state, TsvDocument document)
    {
        if (_selectionStart == null)
            return;

        // Clear all selections first
        ClearAllSelections(document);

        // Delegate to type-specific selection method
        switch (_visualType)
        {
            case VisualType.Character:
                UpdateCharacterSelection(state, document);
                break;
            case VisualType.Line:
                UpdateLineSelection(state, document);
                break;
            case VisualType.Block:
                UpdateBlockSelection(state, document);
                break;
        }

        // Update the selection range in VimState
        state.CurrentSelection = new SelectionRange(
            _visualType,
            _selectionStart,
            state.CursorPosition);
    }

    /// <summary>
    /// Character-wise (rectangular) selection
    /// </summary>
    private void UpdateCharacterSelection(VimState state, TsvDocument document)
    {
        if (_selectionStart == null)
            return;

        // Calculate selection range
        int startRow = Math.Min(_selectionStart.Row, state.CursorPosition.Row);
        int endRow = Math.Max(_selectionStart.Row, state.CursorPosition.Row);
        int startCol = Math.Min(_selectionStart.Column, state.CursorPosition.Column);
        int endCol = Math.Max(_selectionStart.Column, state.CursorPosition.Column);

        // Mark cells as selected
        for (int row = startRow; row <= endRow && row < document.RowCount; row++)
        {
            for (int col = startCol; col <= endCol; col++)
            {
                var cell = GetCell(state, new GridPosition(row, col), document);
                if (cell != null)
                {
                    cell.IsSelected = true;
                }
            }
        }
    }

    /// <summary>
    /// Line-wise selection (select entire rows)
    /// </summary>
    private void UpdateLineSelection(VimState state, TsvDocument document)
    {
        // If header selections exist (from row header clicks), use those
        if (state.SelectedRows.Count > 0)
        {
            foreach (int rowIndex in state.SelectedRows)
            {
                if (rowIndex >= 0 && rowIndex < document.RowCount)
                {
                    var rowObj = document.Rows[rowIndex];
                    foreach (var cell in rowObj.Cells)
                    {
                        cell.IsSelected = true;
                    }
                }
            }
            return;
        }

        // Otherwise, use traditional selection range logic
        if (_selectionStart == null)
            return;

        int startRow = Math.Min(_selectionStart.Row, state.CursorPosition.Row);
        int endRow = Math.Max(_selectionStart.Row, state.CursorPosition.Row);

        // Select ALL cells in selected rows
        for (int row = startRow; row <= endRow && row < document.RowCount; row++)
        {
            var rowObj = document.Rows[row];
            foreach (var cell in rowObj.Cells)
            {
                cell.IsSelected = true;
            }
        }
    }

    /// <summary>
    /// Block-wise selection (select entire columns)
    /// </summary>
    private void UpdateBlockSelection(VimState state, TsvDocument document)
    {
        // If header selections exist (from column header clicks), use those
        if (state.SelectedColumns.Count > 0)
        {
            foreach (var rowObj in document.Rows)
            {
                foreach (int colIndex in state.SelectedColumns)
                {
                    if (colIndex >= 0 && colIndex < rowObj.Cells.Count)
                    {
                        rowObj.Cells[colIndex].IsSelected = true;
                    }
                }
            }
            return;
        }

        // Otherwise, use traditional selection range logic
        if (_selectionStart == null)
            return;

        int startCol = Math.Min(_selectionStart.Column, state.CursorPosition.Column);
        int endCol = Math.Max(_selectionStart.Column, state.CursorPosition.Column);

        // Select ALL cells in selected columns (all rows)
        for (int row = 0; row < document.RowCount; row++)
        {
            var rowObj = document.Rows[row];
            for (int col = startCol; col <= endCol && col < rowObj.Cells.Count; col++)
            {
                rowObj.Cells[col].IsSelected = true;
            }
        }
    }

    private void ClearAllSelections(TsvDocument? document)
    {
        if (document == null)
            return;

        foreach (var row in document.Rows)
        {
            foreach (var cell in row.Cells)
            {
                cell.IsSelected = false;
            }
        }
    }

    private Cell? GetCell(VimState state, GridPosition position, TsvDocument? document)
    {
        if (document == null)
            return null;

        return document.GetCell(position);
    }

    private bool DeleteSelection(VimState state, TsvDocument document)
    {
        if (_selectionStart == null || state.CurrentSelection == null)
            return false;

        var selection = state.CurrentSelection;

        // Handle line-wise deletion (delete entire rows like dd)
        if (_visualType == VisualType.Line)
        {
            return DeleteLineSelection(state, document, selection);
        }

        // Handle block-wise deletion (delete entire columns)
        if (_visualType == VisualType.Block)
        {
            return DeleteBlockSelection(state, document, selection);
        }

        // Handle character-wise deletion (clear cell values)
        // First, yank the selection (like Vim: delete = yank + delete)
        int rows = selection.RowCount;
        int cols = selection.ColumnCount;
        string[,] values = new string[rows, cols];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int docRow = selection.StartRow + r;
                int docCol = selection.StartColumn + c;

                if (docRow < document.RowCount && docCol < document.Rows[docRow].Cells.Count)
                {
                    values[r, c] = document.Rows[docRow].Cells[docCol].Value;
                }
                else
                {
                    values[r, c] = string.Empty;
                }
            }
        }

        state.LastYank = new YankedContent
        {
            Values = values,
            SourceType = _visualType,
            Rows = rows,
            Columns = cols
        };

        // Then delete the selection
        var command = new DeleteSelectionCommand(document, selection);

        // Execute through command history if available (for undo support)
        if (state.CommandHistory != null)
        {
            state.CommandHistory.Execute(command);
        }
        else
        {
            // Fallback: execute directly without undo
            command.Execute();
        }

        // Return to normal mode after delete
        state.SwitchMode(VimMode.Normal);
        return true;
    }

    private bool DeleteLineSelection(VimState state, TsvDocument document, SelectionRange selection)
    {
        // Yank entire rows before deleting (like dd)
        int rowCount = selection.RowCount;
        int startRow = selection.StartRow;

        // Get the maximum column count from all selected rows
        int maxCols = 0;
        for (int r = 0; r < rowCount; r++)
        {
            int docRow = startRow + r;
            if (docRow < document.RowCount)
            {
                maxCols = Math.Max(maxCols, document.Rows[docRow].Cells.Count);
            }
        }

        // Yank all cells from selected rows
        string[,] values = new string[rowCount, maxCols];
        for (int r = 0; r < rowCount; r++)
        {
            int docRow = startRow + r;
            if (docRow < document.RowCount)
            {
                var row = document.Rows[docRow];
                for (int c = 0; c < maxCols; c++)
                {
                    if (c < row.Cells.Count)
                    {
                        values[r, c] = row.Cells[c].Value;
                    }
                    else
                    {
                        values[r, c] = string.Empty;
                    }
                }
            }
        }

        state.LastYank = new YankedContent
        {
            Values = values,
            SourceType = VisualType.Line,
            Rows = rowCount,
            Columns = maxCols
        };

        // Delete rows from bottom to top to maintain correct indices
        for (int r = rowCount - 1; r >= 0; r--)
        {
            int docRow = startRow + r;
            if (docRow < document.RowCount)
            {
                var command = new DeleteRowCommand(document, docRow);
                if (state.CommandHistory != null)
                {
                    state.CommandHistory.Execute(command);
                }
                else
                {
                    command.Execute();
                }
            }
        }

        // Adjust cursor position if needed
        if (state.CursorPosition.Row >= document.RowCount && document.RowCount > 0)
        {
            state.CursorPosition = new GridPosition(document.RowCount - 1, state.CursorPosition.Column);
        }

        // Return to normal mode after delete
        state.SwitchMode(VimMode.Normal);
        return true;
    }

    private bool YankSelection(VimState state, TsvDocument document)
    {
        if (_selectionStart == null || state.CurrentSelection == null)
            return false;

        var selection = state.CurrentSelection;

        // Handle line-wise yank (yank entire rows like yy)
        if (_visualType == VisualType.Line)
        {
            return YankLineSelection(state, document, selection);
        }

        // Handle character-wise and block-wise yank
        // Calculate dimensions
        int rows = selection.RowCount;
        int cols = selection.ColumnCount;
        string[,] values = new string[rows, cols];

        // Copy all values from the selected range
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                int docRow = selection.StartRow + r;
                int docCol = selection.StartColumn + c;

                if (docRow < document.RowCount && docCol < document.Rows[docRow].Cells.Count)
                {
                    values[r, c] = document.Rows[docRow].Cells[docCol].Value;
                }
                else
                {
                    values[r, c] = string.Empty;
                }
            }
        }

        // Store yanked content in VimState
        state.LastYank = new YankedContent
        {
            Values = values,
            SourceType = _visualType,
            Rows = rows,
            Columns = cols
        };

        // Return to normal mode after yank
        state.SwitchMode(VimMode.Normal);
        return true;
    }

    private bool YankLineSelection(VimState state, TsvDocument document, SelectionRange selection)
    {
        // Yank entire rows (like yy)
        int rowCount = selection.RowCount;
        int startRow = selection.StartRow;

        // Get the maximum column count from all selected rows
        int maxCols = 0;
        for (int r = 0; r < rowCount; r++)
        {
            int docRow = startRow + r;
            if (docRow < document.RowCount)
            {
                maxCols = Math.Max(maxCols, document.Rows[docRow].Cells.Count);
            }
        }

        // Yank all cells from selected rows
        string[,] values = new string[rowCount, maxCols];
        for (int r = 0; r < rowCount; r++)
        {
            int docRow = startRow + r;
            if (docRow < document.RowCount)
            {
                var row = document.Rows[docRow];
                for (int c = 0; c < maxCols; c++)
                {
                    if (c < row.Cells.Count)
                    {
                        values[r, c] = row.Cells[c].Value;
                    }
                    else
                    {
                        values[r, c] = string.Empty;
                    }
                }
            }
        }

        // Store yanked content in VimState
        state.LastYank = new YankedContent
        {
            Values = values,
            SourceType = VisualType.Line,
            Rows = rowCount,
            Columns = maxCols
        };

        // Return to normal mode after yank
        state.SwitchMode(VimMode.Normal);
        return true;
    }

    private bool DeleteBlockSelection(VimState state, TsvDocument document, SelectionRange selection)
    {
        // Yank entire columns before deleting
        int colCount = selection.ColumnCount;
        int startCol = selection.StartColumn;
        int rowCount = document.RowCount;

        // Yank all cells from selected columns
        string[,] values = new string[rowCount, colCount];
        for (int r = 0; r < rowCount; r++)
        {
            var row = document.Rows[r];
            for (int c = 0; c < colCount; c++)
            {
                int docCol = startCol + c;
                if (docCol < row.Cells.Count)
                {
                    values[r, c] = row.Cells[docCol].Value;
                }
                else
                {
                    values[r, c] = string.Empty;
                }
            }
        }

        state.LastYank = new YankedContent
        {
            Values = values,
            SourceType = VisualType.Block,
            Rows = rowCount,
            Columns = colCount
        };

        // Delete columns from right to left to maintain correct indices
        for (int c = colCount - 1; c >= 0; c--)
        {
            int docCol = startCol + c;
            if (docCol < document.ColumnCount)
            {
                var command = new DeleteColumnCommand(document, docCol);
                if (state.CommandHistory != null)
                {
                    state.CommandHistory.Execute(command);
                }
                else
                {
                    command.Execute();
                }
            }
        }

        // Adjust cursor position if needed
        if (state.CursorPosition.Column >= document.ColumnCount && document.ColumnCount > 0)
        {
            state.CursorPosition = new GridPosition(state.CursorPosition.Row, document.ColumnCount - 1);
        }

        // Return to normal mode after delete
        state.SwitchMode(VimMode.Normal);
        return true;
    }
}
