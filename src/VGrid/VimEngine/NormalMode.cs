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

            // Search
            Key.OemQuestion when !modifiers.HasFlag(ModifierKeys.Shift) => StartSearch(state), // '/' key
            Key.N when modifiers.HasFlag(ModifierKeys.Shift) => NavigateToNextMatch(state, false), // 'N'
            Key.N => NavigateToNextMatch(state, true), // 'n'

            // File movement
            Key.G when state.PendingKeys.Keys.LastOrDefault() == Key.G => MoveToFirstLine(state),
            Key.G when state.PendingKeys.Keys.Count == 0 => HandlePendingG(state),

            // Yank operations
            Key.Y when state.PendingKeys.Keys.LastOrDefault() == Key.Y => YankLine(state, document),
            Key.Y when state.PendingKeys.Keys.Count == 0 => HandlePendingY(state),

            // Word text object for yank/delete (yiw, yaw, diw, daw)
            Key.W when state.PendingKeys.Keys.Count == 2 && state.PendingKeys.Keys[0] == Key.Y &&
                      (state.PendingKeys.Keys[1] == Key.I || state.PendingKeys.Keys[1] == Key.A) => YankWord(state, document),
            Key.W when state.PendingKeys.Keys.Count == 2 && state.PendingKeys.Keys[0] == Key.D &&
                      (state.PendingKeys.Keys[1] == Key.I || state.PendingKeys.Keys[1] == Key.A) => DeleteWord(state, document),
            Key.I when state.PendingKeys.Keys.Count == 1 && (state.PendingKeys.Keys[0] == Key.Y || state.PendingKeys.Keys[0] == Key.D) => HandleTextObject(state, Key.I),
            Key.A when state.PendingKeys.Keys.Count == 1 && (state.PendingKeys.Keys[0] == Key.Y || state.PendingKeys.Keys[0] == Key.D) => HandleTextObject(state, Key.A),

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

            // Delete operations
            Key.D when state.PendingKeys.Keys.LastOrDefault() == Key.D => DeleteLine(state, document),
            Key.D when state.PendingKeys.Keys.Count == 0 => HandlePendingD(state),

            // Undo operation
            Key.U => Undo(state),

            // Placeholder keys for future implementation
            Key.B =>true,
            Key.C =>true,
            Key.E =>true,
            Key.F =>true,
            Key.G =>true,
            Key.M =>true,
            Key.Q =>true,
            Key.R =>true,
            Key.S =>true,
            Key.T =>true,
            Key.W when state.PendingKeys.Keys.Count < 2 =>true, // W is only a placeholder when not part of yiw/yaw/diw/daw
            Key.X =>true,
            Key.Z =>true,

            _ => false
        };

        // Clear count prefix after command execution
        // Don't clear for multi-key commands when building sequences
        bool isMultiKeySequence = (key == Key.G && state.PendingKeys.Keys.Count > 0) ||
                                   (key == Key.Y && state.PendingKeys.Keys.Count > 0) ||
                                   (key == Key.D && state.PendingKeys.Keys.Count > 0) ||
                                   ((key == Key.I || key == Key.A) && state.PendingKeys.Keys.Count > 1) ||
                                   (key == Key.W && state.PendingKeys.Keys.Count > 1);

        if (handled && !isMultiKeySequence)
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
        // Set caret position to start of cell when entering insert mode with 'i'
        state.CellEditCaretPosition = CellEditCaretPosition.Start;
        state.SwitchMode(VimMode.Insert);
        return true;
    }

    private bool SwitchToInsertModeAfter(VimState state, TsvDocument document)
    {
        // Set caret position to end of cell when entering insert mode with 'a'
        state.CellEditCaretPosition = CellEditCaretPosition.End;
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
            return true; // Key is handled, but nothing to paste

        var yank = state.LastYank;
        var startPos = state.CursorPosition;

        // Handle line-wise paste (insert new rows below cursor)
        if (yank.SourceType == VisualType.Line)
        {
            // Insert new rows below the current row
            for (int r = 0; r < yank.Rows; r++)
            {
                int insertRow = startPos.Row + 1 + r;
                document.InsertRow(insertRow);

                // Fill the new row with yanked values
                var row = document.Rows[insertRow];
                for (int c = 0; c < yank.Columns && c < row.Cells.Count; c++)
                {
                    row.Cells[c].Value = yank.Values[r, c];
                }
            }

            // Move cursor to the first inserted row
            state.CursorPosition = new GridPosition(startPos.Row + 1, startPos.Column);
            return true;
        }

        // Handle block-wise paste (insert new columns to the right of cursor)
        if (yank.SourceType == VisualType.Block)
        {
            // Insert new columns to the right of the current column
            for (int c = 0; c < yank.Columns; c++)
            {
                int insertCol = startPos.Column + 1 + c;
                document.InsertColumn(insertCol);

                // Fill the new column with yanked values
                for (int r = 0; r < yank.Rows && r < document.RowCount; r++)
                {
                    var row = document.Rows[r];
                    if (insertCol < row.Cells.Count)
                    {
                        row.Cells[insertCol].Value = yank.Values[r, c];
                    }
                }
            }

            // Move cursor to the first inserted column
            state.CursorPosition = new GridPosition(startPos.Row, startPos.Column + 1);
            return true;
        }

        // Handle character-wise paste (overwrite values)
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

    private bool HandlePendingY(VimState state)
    {
        // Add 'y' to pending keys, wait for second 'y'
        state.PendingKeys.Add(Key.Y);
        return true;
    }

    private bool YankLine(VimState state, TsvDocument document)
    {
        // Yank entire current row
        if (state.CursorPosition.Row >= document.RowCount)
        {
            state.PendingKeys.Clear();
            return true; // Key is handled, but no row to yank
        }

        var row = document.Rows[state.CursorPosition.Row];
        var columnCount = row.Cells.Count;
        string[,] values = new string[1, columnCount];

        for (int c = 0; c < columnCount; c++)
        {
            values[0, c] = row.Cells[c].Value;
        }

        // Store yanked content
        state.LastYank = new YankedContent
        {
            Values = values,
            SourceType = VisualType.Line,
            Rows = 1,
            Columns = columnCount
        };

        // Clear pending keys
        state.PendingKeys.Clear();
        return true;
    }

    private bool HandlePendingD(VimState state)
    {
        // Add 'd' to pending keys, wait for second 'd'
        state.PendingKeys.Add(Key.D);
        return true;
    }

    private bool DeleteLine(VimState state, TsvDocument document)
    {
        // Delete entire row (not just clear cells)
        if (state.CursorPosition.Row >= document.RowCount)
        {
            state.PendingKeys.Clear();
            return true; // Key is handled, but no row to delete
        }

        var row = document.Rows[state.CursorPosition.Row];
        var columnCount = row.Cells.Count;

        // First, yank the line (like Vim: delete = yank + delete)
        string[,] values = new string[1, columnCount];
        for (int c = 0; c < columnCount; c++)
        {
            values[0, c] = row.Cells[c].Value;
        }

        state.LastYank = new YankedContent
        {
            Values = values,
            SourceType = VisualType.Line,
            Rows = 1,
            Columns = columnCount
        };

        // Then delete the row completely
        var command = new Commands.DeleteRowCommand(document, state.CursorPosition.Row);

        // Execute through command history if available
        if (state.CommandHistory != null)
        {
            state.CommandHistory.Execute(command);
        }
        else
        {
            command.Execute();
        }

        // Keep cursor at same row (which now shows the next row)
        // Clamp to valid range
        if (state.CursorPosition.Row >= document.RowCount && document.RowCount > 0)
        {
            state.CursorPosition = new GridPosition(document.RowCount - 1, state.CursorPosition.Column);
        }

        // Clear pending keys
        state.PendingKeys.Clear();
        return true;
    }

    private bool Undo(VimState state)
    {
        if (state.CommandHistory != null && state.CommandHistory.CanUndo)
        {
            state.CommandHistory.Undo();
        }
        return true; // Key is always handled, even if there's nothing to undo
    }

    private bool StartSearch(VimState state)
    {
        state.SwitchMode(VimMode.Command);
        return true;
    }

    private bool NavigateToNextMatch(VimState state, bool forward)
    {
        if (!state.IsSearchActive || state.SearchResults.Count == 0)
            return true; // Key is handled, but no search results to navigate

        state.NavigateToNextMatch(forward);
        return true;
    }

    private bool HandleTextObject(VimState state, Key textObjectKey)
    {
        // Add text object modifier (i or a) to pending keys
        state.PendingKeys.Add(textObjectKey);
        return true;
    }

    private bool YankWord(VimState state, TsvDocument document)
    {
        // In TSV editor, "word" = current cell
        // yiw and yaw both yank the current cell value
        if (state.CursorPosition.Row >= document.RowCount)
        {
            state.PendingKeys.Clear();
            return true;
        }

        var cell = document.GetCell(state.CursorPosition);
        if (cell == null)
        {
            state.PendingKeys.Clear();
            return true;
        }

        // Store yanked content as a single cell
        string[,] values = new string[1, 1];
        values[0, 0] = cell.Value;

        state.LastYank = new YankedContent
        {
            Values = values,
            SourceType = VisualType.Character,
            Rows = 1,
            Columns = 1
        };

        state.PendingKeys.Clear();
        return true;
    }

    private bool DeleteWord(VimState state, TsvDocument document)
    {
        // In TSV editor, "word" = current cell
        // diw and daw both yank and clear the current cell value
        if (state.CursorPosition.Row >= document.RowCount)
        {
            state.PendingKeys.Clear();
            return true;
        }

        var cell = document.GetCell(state.CursorPosition);
        if (cell == null)
        {
            state.PendingKeys.Clear();
            return true;
        }

        // First, yank the cell value
        string[,] values = new string[1, 1];
        values[0, 0] = cell.Value;

        state.LastYank = new YankedContent
        {
            Values = values,
            SourceType = VisualType.Character,
            Rows = 1,
            Columns = 1
        };

        // Then clear the cell value
        var command = new Commands.EditCellCommand(document, state.CursorPosition, string.Empty);

        // Execute through command history if available
        if (state.CommandHistory != null)
        {
            state.CommandHistory.Execute(command);
        }
        else
        {
            command.Execute();
        }

        state.PendingKeys.Clear();
        return true;
    }
}
