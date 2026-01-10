using System.Windows.Input;
using VGrid.Models;

namespace VGrid.VimEngine;

/// <summary>
/// Implements Vim Normal mode behavior
/// </summary>
public class NormalMode : IVimMode
{
    public string GetModeName() => "NORMAL";

    public void OnEnter(VimState state)
    {
        // Clear any selection when entering normal mode
        state.ResetState();
    }

    public void OnExit(VimState state)
    {
        // Nothing to do when exiting normal mode
    }

    public bool HandleKey(VimState state, Key key, ModifierKeys modifiers, TsvDocument document)
    {
        // Handle count prefix (0-9)
        if (HandleCountPrefix(state, key))
            return true;

        // Get the count (default to 1 if no prefix)
        int count = state.CountPrefix ?? 1;

        // Handle navigation keys
        bool handled = key switch
        {
            // Basic movement (hjkl)
            Key.H => MoveLeft(state, document, count),
            Key.J => MoveDown(state, document, count),
            Key.K => MoveUp(state, document, count),
            Key.L => MoveRight(state, document, count),

            // Line movement
            Key.D0 when state.CountPrefix == null => MoveToLineStart(state),
            Key.OemPeriod when modifiers.HasFlag(ModifierKeys.Shift) => MoveToLineEnd(state, document), // >
            Key.D4 when modifiers.HasFlag(ModifierKeys.Shift) => MoveToLineEnd(state, document), // $ (Shift+4)

            // File movement
            Key.G when state.PendingKeys.Keys.LastOrDefault() == Key.G => MoveToFirstLine(state),
            Key.G when state.PendingKeys.Keys.Count == 0 => HandlePendingG(state),

            // Mode switching
            Key.I => SwitchToInsertMode(state),
            Key.A => SwitchToInsertModeAfter(state, document),
            Key.O => InsertLineBelow(state, document),
            Key.V when modifiers.HasFlag(ModifierKeys.Shift) => SwitchToVisualLineMode(state),
            Key.V when modifiers.HasFlag(ModifierKeys.Control) => SwitchToVisualBlockMode(state),
            Key.V => SwitchToVisualMode(state),

            // Escape (should stay in normal mode, but clear state)
            Key.Escape => ClearState(state),

            // Paste operation
            Key.P => PasteAfterCursor(state, document),

            // Placeholder keys for future implementation
            Key.B =>true,
            Key.C =>true,
            Key.D =>true,
            Key.E =>true,
            Key.F =>true,
            Key.G =>true,
            Key.M =>true,
            Key.N =>true,
            Key.Q =>true,
            Key.R =>true,
            Key.S =>true,
            Key.T =>true,
            Key.U =>true,
            Key.W =>true,
            Key.X =>true,
            Key.Y =>true,
            Key.Z =>true,

            _ => false
        };

        // Clear count prefix after command execution
        if (handled && key != Key.G)
        {
            state.CountPrefix = null;
            state.PendingKeys.Clear();
        }

        return handled;
    }

    private bool HandleCountPrefix(VimState state, Key key)
    {
        // Numbers 1-9 can be count prefix (0 is only if there's already a prefix)
        if (key >= Key.D1 && key <= Key.D9)
        {
            int digit = key - Key.D0;
            state.CountPrefix = (state.CountPrefix ?? 0) * 10 + digit;
            return true;
        }

        if (key == Key.D0 && state.CountPrefix.HasValue)
        {
            state.CountPrefix = state.CountPrefix.Value * 10;
            return true;
        }

        return false;
    }

    private bool MoveLeft(VimState state, TsvDocument document, int count)
    {
        var newPos = state.CursorPosition.MoveLeft(count).Clamp(document);
        state.CursorPosition = newPos;
        return true;
    }

    private bool MoveRight(VimState state, TsvDocument document, int count)
    {
        var newPos = state.CursorPosition.MoveRight(count).Clamp(document);
        state.CursorPosition = newPos;
        return true;
    }

    private bool MoveUp(VimState state, TsvDocument document, int count)
    {
        var newPos = state.CursorPosition.MoveUp(count).Clamp(document);
        state.CursorPosition = newPos;
        return true;
    }

    private bool MoveDown(VimState state, TsvDocument document, int count)
    {
        var newPos = state.CursorPosition.MoveDown(count).Clamp(document);
        state.CursorPosition = newPos;
        return true;
    }

    private bool MoveToLineStart(VimState state)
    {
        state.CursorPosition = state.CursorPosition.MoveToLineStart();
        return true;
    }

    private bool MoveToLineEnd(VimState state, TsvDocument document)
    {
        state.CursorPosition = state.CursorPosition.MoveToLineEnd(document);
        return true;
    }

    private bool MoveToFirstLine(VimState state)
    {
        state.CursorPosition = state.CursorPosition.MoveToFirstRow();
        state.PendingKeys.Clear();
        return true;
    }

    private bool HandlePendingG(VimState state)
    {
        // Add 'g' to pending keys, wait for second 'g'
        state.PendingKeys.Add(Key.G);
        return true;
    }

    private bool SwitchToInsertMode(VimState state)
    {
        state.SwitchMode(VimMode.Insert);
        return true;
    }

    private bool SwitchToInsertModeAfter(VimState state, TsvDocument document)
    {
        // Move cursor one position to the right, then enter insert mode
        var newPos = state.CursorPosition.MoveRight(1).Clamp(document);
        state.CursorPosition = newPos;
        state.SwitchMode(VimMode.Insert);
        return true;
    }

    private bool InsertLineBelow(VimState state, TsvDocument document)
    {
        // Insert a new row below the current row
        document.InsertRow(state.CursorPosition.Row + 1);
        state.CursorPosition = new GridPosition(state.CursorPosition.Row + 1, 0);
        state.SwitchMode(VimMode.Insert);
        return true;
    }

    private bool SwitchToVisualMode(VimState state)
    {
        // Character-wise visual mode (v)
        state.CurrentSelection = new SelectionRange(
            VisualType.Character,
            state.CursorPosition,
            state.CursorPosition);
        state.SwitchMode(VimMode.Visual);
        return true;
    }

    private bool SwitchToVisualLineMode(VimState state)
    {
        // Line-wise visual mode (Shift+V)
        state.CurrentSelection = new SelectionRange(
            VisualType.Line,
            state.CursorPosition,
            state.CursorPosition);
        state.SwitchMode(VimMode.Visual);
        return true;
    }

    private bool SwitchToVisualBlockMode(VimState state)
    {
        // Block-wise (column) visual mode (Ctrl+V)
        state.CurrentSelection = new SelectionRange(
            VisualType.Block,
            state.CursorPosition,
            state.CursorPosition);
        state.SwitchMode(VimMode.Visual);
        return true;
    }

    private bool PasteAfterCursor(VimState state, TsvDocument document)
    {
        if (state.LastYank == null)
            return false;

        var yank = state.LastYank;
        var startPos = state.CursorPosition;

        // Ensure document has enough rows and columns
        int neededRows = startPos.Row + yank.Rows;
        int neededCols = startPos.Column + yank.Columns;

        document.EnsureSize(neededRows, Math.Max(neededCols, document.ColumnCount));

        // Paste values
        for (int r = 0; r < yank.Rows; r++)
        {
            for (int c = 0; c < yank.Columns; c++)
            {
                int targetRow = startPos.Row + r;
                int targetCol = startPos.Column + c;

                if (targetRow < document.RowCount && targetCol < document.Rows[targetRow].Cells.Count)
                {
                    // Paste the value
                    document.Rows[targetRow].Cells[targetCol].Value = yank.Values[r, c];
                }
            }
        }

        return true;
    }

    private bool ClearState(VimState state)
    {
        state.ResetState();
        return true;
    }
}
