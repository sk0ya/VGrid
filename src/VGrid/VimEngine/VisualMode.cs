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

    private bool YankSelection(VimState state, TsvDocument document)
    {
        if (_selectionStart == null || state.CurrentSelection == null)
            return false;

        var selection = state.CurrentSelection;

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
}
