using System.Windows.Input;
using VGrid.Models;

namespace VGrid.VimEngine;

/// <summary>
/// Implements Vim Visual mode behavior
/// </summary>
public class VisualMode : IVimMode
{
    private GridPosition? _selectionStart;

    public string GetModeName() => "VISUAL";

    public void OnEnter(VimState state)
    {
        // Store the starting position of the selection
        _selectionStart = state.CursorPosition;

        // Mark the current cell as selected
        var cell = GetCell(state, state.CursorPosition, null);
        if (cell != null)
        {
            cell.IsSelected = true;
        }
    }

    public void OnExit(VimState state)
    {
        // Clear all selections
        if (state != null)
        {
            ClearAllSelections(null);
        }
        _selectionStart = null;
    }

    public bool HandleKey(VimState state, Key key, ModifierKeys modifiers, TsvDocument document)
    {
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

        // Calculate selection range
        int startRow = Math.Min(_selectionStart.Row, state.CursorPosition.Row);
        int endRow = Math.Max(_selectionStart.Row, state.CursorPosition.Row);
        int startCol = Math.Min(_selectionStart.Column, state.CursorPosition.Column);
        int endCol = Math.Max(_selectionStart.Column, state.CursorPosition.Column);

        // Mark cells as selected
        for (int row = startRow; row <= endRow; row++)
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
        // Placeholder - will be implemented in later phases with command pattern
        // For now, just return to normal mode
        state.SwitchMode(VimMode.Normal);
        return true;
    }

    private bool YankSelection(VimState state, TsvDocument document)
    {
        // Placeholder - will be implemented in later phases
        // For now, just return to normal mode
        state.SwitchMode(VimMode.Normal);
        return true;
    }
}
