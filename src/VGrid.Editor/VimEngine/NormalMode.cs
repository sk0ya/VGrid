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
        state.CurrentSelection = null;
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

        // Handle paste operations (P/p) - must be before switch expression
        if (key == Key.P)
        {
            // Check Shift key state directly for more reliable detection
            bool isShiftPressed = modifiers.HasFlag(ModifierKeys.Shift);

            // Also check physical Shift key state (only works in UI thread)
            try
            {
                isShiftPressed = isShiftPressed ||
                                 System.Windows.Input.Keyboard.IsKeyDown(Key.LeftShift) ||
                                 System.Windows.Input.Keyboard.IsKeyDown(Key.RightShift);
            }
            catch
            {
                // In non-UI thread (e.g., unit tests), rely on modifiers parameter only
            }

            if (isShiftPressed)
            {
                return PasteBeforeCursor(state, document);
            }
            else
            {
                return PasteAfterCursor(state, document);
            }
        }

        // Handle Ctrl+C to copy current cell
        if (key == Key.C && modifiers.HasFlag(ModifierKeys.Control))
        {
            return YankCurrentCell(state, document);
        }

        // Handle Ctrl+V to paste
        if (key == Key.V && modifiers.HasFlag(ModifierKeys.Control) && !modifiers.HasFlag(ModifierKeys.Shift))
        {
            return PasteAfterCursor(state, document);
        }

        // Handle Ctrl+Shift+V for block visual mode (column selection)
        if (key == Key.V && modifiers.HasFlag(ModifierKeys.Control) && modifiers.HasFlag(ModifierKeys.Shift))
        {
            return SwitchToVisualBlockMode(state);
        }

        // Handle Ctrl+R for redo
        if (key == Key.R && modifiers.HasFlag(ModifierKeys.Control))
        {
            return Redo(state, document);
        }

        // Handle navigation keys
        bool handled = key switch
        {
            // Basic movement (hjkl)
            Key.H when modifiers.HasFlag(ModifierKeys.Shift) => MoveToLineStart(state),
            Key.H => MoveLeft(state, document, count),
            Key.J when modifiers.HasFlag(ModifierKeys.Shift) => MoveDown(state, document, 10),
            Key.J => MoveDown(state, document, count),
            Key.K when modifiers.HasFlag(ModifierKeys.Shift) => MoveUp(state, document, 10),
            Key.K => MoveUp(state, document, count),
            Key.L when modifiers.HasFlag(ModifierKeys.Shift) => MoveToLastNonEmptyColumn(state, document),
            Key.L => MoveRight(state, document, count),

            // Line movement
            Key.D0 when state.CountPrefix == null => MoveToFirstRowFirstColumn(state),
            Key.D4 when modifiers.HasFlag(ModifierKeys.Shift) => MoveToLastNonEmptyColumn(state, document), // $ (Shift+4)

            // Tab navigation
            Key.OemComma when modifiers.HasFlag(ModifierKeys.Shift) => SwitchToPreviousTab(state), // <
            Key.OemPeriod when modifiers.HasFlag(ModifierKeys.Shift) => SwitchToNextTab(state), // >

            // Dot command (repeat last change)
            Key.OemPeriod when !modifiers.HasFlag(ModifierKeys.Shift) => RepeatLastChange(state, document, count),

            // Search
            Key.OemQuestion when !modifiers.HasFlag(ModifierKeys.Shift) => StartSearch(state), // '/' key
            Key.N when modifiers.HasFlag(ModifierKeys.Shift) => NavigateToNextMatch(state, false), // 'N'
            Key.N => NavigateToNextMatch(state, true), // 'n'

            // Ex-commands
            // Key.Oem1 is ':' on Japanese keyboard and ';' on US keyboard
            // Both with/without Shift are treated as ':' for command mode
            Key.Oem1 => StartExCommand(state), // ':' key

            // File movement
            Key.G when modifiers.HasFlag(ModifierKeys.Shift) => MoveToLastLine(state, document),
            Key.G when state.CountPrefix.HasValue => MoveToSpecificLine(state, document, state.CountPrefix.Value),
            Key.G when state.PendingKeys.Keys.LastOrDefault() == Key.G => MoveToFirstLine(state),
            Key.G when state.PendingKeys.Keys.Count == 0 => HandlePendingG(state),

            // Leader key (Space) operations
            Key.W when state.PendingKeys.Keys.LastOrDefault() == Key.Space => SaveFile(state),
            Key.Space when state.PendingKeys.Keys.Count == 0 => HandlePendingSpace(state),

            // Yank operations
            Key.Y when state.PendingKeys.Keys.LastOrDefault() == Key.Y => YankLine(state, document),
            Key.Y when state.PendingKeys.Keys.Count == 0 => HandlePendingY(state),

            // Word text object for yank/delete/change (yiw, yaw, diw, daw, ciw, caw)
            Key.W when state.PendingKeys.Keys.Count == 2 && state.PendingKeys.Keys[0] == Key.Y &&
                      (state.PendingKeys.Keys[1] == Key.I || state.PendingKeys.Keys[1] == Key.A) => YankWord(state, document),
            Key.W when state.PendingKeys.Keys.Count == 2 && state.PendingKeys.Keys[0] == Key.D &&
                      (state.PendingKeys.Keys[1] == Key.I || state.PendingKeys.Keys[1] == Key.A) => DeleteWord(state, document),
            Key.W when state.PendingKeys.Keys.Count == 2 && state.PendingKeys.Keys[0] == Key.C &&
                      (state.PendingKeys.Keys[1] == Key.I || state.PendingKeys.Keys[1] == Key.A) => ChangeWord(state, document),
            Key.I when state.PendingKeys.Keys.Count == 1 && (state.PendingKeys.Keys[0] == Key.Y || state.PendingKeys.Keys[0] == Key.D || state.PendingKeys.Keys[0] == Key.C) => HandleTextObject(state, Key.I),
            Key.A when state.PendingKeys.Keys.Count == 1 && (state.PendingKeys.Keys[0] == Key.Y || state.PendingKeys.Keys[0] == Key.D || state.PendingKeys.Keys[0] == Key.C) => HandleTextObject(state, Key.A),

            // Mode switching
            Key.I when modifiers.HasFlag(ModifierKeys.Shift) => MoveLeftAndInsert(state, document),
            Key.I => SwitchToInsertMode(state),
            Key.A when modifiers.HasFlag(ModifierKeys.Shift) => MoveRightAndInsert(state, document),
            Key.A => SwitchToInsertModeAfter(state, document),
            Key.O when modifiers.HasFlag(ModifierKeys.Shift) => InsertLineAbove(state, document),
            Key.O => InsertLineBelow(state, document),
            Key.V when modifiers.HasFlag(ModifierKeys.Shift) => SwitchToVisualLineMode(state),
            Key.V => SwitchToVisualMode(state),

            // Escape (should stay in normal mode, but clear state)
            Key.Escape => ClearState(state),

            // Delete operations
            Key.D when state.PendingKeys.Keys.LastOrDefault() == Key.D => DeleteLine(state, document),
            Key.D when state.PendingKeys.Keys.Count == 0 => HandlePendingD(state),
            Key.X => DeleteCurrentCell(state, document),
            Key.Delete => DeleteCurrentCell(state, document),

            // Undo operation
            Key.U => Undo(state, document),

            // Word movement
            Key.W when state.PendingKeys.Keys.Count < 2 => MoveToNextNonEmptyCell(state, document),
            Key.B => MoveToPreviousNonEmptyCell(state, document),

            // Scroll commands
            Key.Z when state.PendingKeys.Keys.LastOrDefault() == Key.Z => ScrollToCenter(state),
            Key.Z when state.PendingKeys.Keys.Count == 0 => HandlePendingZ(state),

            // Change operations
            Key.C when state.PendingKeys.Keys.LastOrDefault() == Key.C => ChangeLine(state, document),
            Key.C when state.PendingKeys.Keys.Count == 0 => HandlePendingC(state),

            // Align columns (format all columns to equal width)
            Key.OemPlus when !modifiers.HasFlag(ModifierKeys.Shift) => AlignAllColumns(state, document), // '=' key

            // Paragraph movement (empty row navigation)
            Key.OemCloseBrackets when modifiers.HasFlag(ModifierKeys.Shift) => MoveToNextEmptyRow(state, document, count), // '}'
            Key.OemOpenBrackets when modifiers.HasFlag(ModifierKeys.Shift) => MoveToPreviousEmptyRow(state, document, count), // '{'

            // Placeholder keys for future implementation
            Key.E =>true,
            Key.F =>true,
            Key.G =>true,
            Key.M =>true,
            Key.Q =>true,
            Key.R =>true,
            Key.S =>true,
            Key.T =>true,

            _ => false
        };

        // Clear count prefix after command execution
        // Don't clear for multi-key commands when building sequences
        bool isMultiKeySequence = (key == Key.G && state.PendingKeys.Keys.Count > 0) ||
                                   (key == Key.Y && state.PendingKeys.Keys.Count > 0) ||
                                   (key == Key.D && state.PendingKeys.Keys.Count > 0) ||
                                   (key == Key.C && state.PendingKeys.Keys.Count > 0) ||
                                   (key == Key.Space && state.PendingKeys.Keys.Count > 0) ||
                                   (key == Key.Z && state.PendingKeys.Keys.Count > 0) ||
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

    private bool MoveToFirstRowFirstColumn(VimState state)
    {
        state.CursorPosition = new GridPosition(0, 0);
        return true;
    }

    private bool MoveToLineEnd(VimState state, TsvDocument document)
    {
        state.CursorPosition = state.CursorPosition.MoveToLineEnd(document);
        return true;
    }

    private bool MoveToLastNonEmptyColumn(VimState state, TsvDocument document)
    {
        // Find the last non-empty cell in the current row
        int currentRow = state.CursorPosition.Row;
        if (currentRow >= document.RowCount)
            return true;

        var row = document.Rows[currentRow];
        int lastNonEmptyCol = -1;

        for (int col = row.Cells.Count - 1; col >= 0; col--)
        {
            if (!string.IsNullOrEmpty(row.Cells[col].Value))
            {
                lastNonEmptyCol = col;
                break;
            }
        }

        // If found, move to that column; otherwise stay at current position
        if (lastNonEmptyCol >= 0)
        {
            state.CursorPosition = new GridPosition(currentRow, lastNonEmptyCol);
        }

        return true;
    }

    private bool MoveToFirstLine(VimState state)
    {
        state.CursorPosition = state.CursorPosition.MoveToFirstRow();
        state.PendingKeys.Clear();
        return true;
    }

    private bool MoveToLastLine(VimState state, TsvDocument document)
    {
        // Move to the last row that has content (non-empty cells)
        if (document.RowCount == 0)
            return true;

        // Search backwards from the last row to find a row with content
        int lastNonEmptyRow = -1;
        for (int row = document.RowCount - 1; row >= 0; row--)
        {
            var rowObj = document.Rows[row];
            bool hasContent = rowObj.Cells.Any(cell => !string.IsNullOrEmpty(cell.Value));
            if (hasContent)
            {
                lastNonEmptyRow = row;
                break;
            }
        }

        // If found a row with content, move there; otherwise move to first row
        int targetRow = lastNonEmptyRow >= 0 ? lastNonEmptyRow : 0;
        state.CursorPosition = new GridPosition(targetRow, state.CursorPosition.Column).Clamp(document);

        return true;
    }

    private bool MoveToSpecificLine(VimState state, TsvDocument document, int lineNumber)
    {
        // Move to a specific line number (1-based)
        // Vim uses 1-based line numbers, so convert to 0-based index
        int targetRow = lineNumber - 1;

        // Clamp to valid range (0 to RowCount-1)
        if (targetRow < 0)
            targetRow = 0;
        else if (targetRow >= document.RowCount)
            targetRow = document.RowCount - 1;

        state.CursorPosition = new GridPosition(targetRow, state.CursorPosition.Column).Clamp(document);

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
        state.PendingInsertType = ChangeType.Insert;
        state.InsertModeStartPosition = state.CursorPosition;
        state.SwitchMode(VimMode.Insert);
        return true;
    }

    private bool SwitchToInsertModeAfter(VimState state, TsvDocument document)
    {
        // Set caret position to end of cell when entering insert mode with 'a'
        state.CellEditCaretPosition = CellEditCaretPosition.End;
        state.PendingInsertType = ChangeType.InsertAfter;
        state.InsertModeStartPosition = state.CursorPosition;
        state.SwitchMode(VimMode.Insert);
        return true;
    }

    private bool InsertLineBelow(VimState state, TsvDocument document)
    {
        // Insert a new row below the current row using command for undo support
        int insertRow = state.CursorPosition.Row + 1;
        int currentColumn = state.CursorPosition.Column;
        var command = new Commands.InsertRowCommand(document, insertRow);

        // Execute through command history if available
        if (state.CommandHistory != null)
        {
            state.CommandHistory.Execute(command);
        }
        else
        {
            command.Execute();
        }

        state.CursorPosition = new GridPosition(insertRow, currentColumn);
        state.PendingInsertType = ChangeType.InsertLineBelow;
        state.InsertModeStartPosition = state.CursorPosition;
        state.SwitchMode(VimMode.Insert);
        return true;
    }

    private bool InsertLineAbove(VimState state, TsvDocument document)
    {
        // Insert a new row above the current row using command for undo support
        int insertRow = state.CursorPosition.Row;
        int currentColumn = state.CursorPosition.Column;
        var command = new Commands.InsertRowCommand(document, insertRow);

        // Execute through command history if available
        if (state.CommandHistory != null)
        {
            state.CommandHistory.Execute(command);
        }
        else
        {
            command.Execute();
        }

        state.CursorPosition = new GridPosition(insertRow, currentColumn);
        state.PendingInsertType = ChangeType.InsertLineAbove;
        state.InsertModeStartPosition = state.CursorPosition;
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
        // Try to use LastYank, or fallback to clipboard
        var yank = state.LastYank ?? ClipboardHelper.ReadFromClipboard();

        if (yank == null)
            return true; // Key is handled, but nothing to paste

        var startPos = state.CursorPosition;

        // Snapshot the yank content for dot command replay
        var yankSnapshot = new YankedContent
        {
            Values = (string[,])yank.Values.Clone(),
            SourceType = yank.SourceType,
            Rows = yank.Rows,
            Columns = yank.Columns
        };

        // Create and execute paste command (paste after: pasteBefore = false)
        var command = new Commands.PasteCommand(document, startPos, yank, pasteBefore: false);

        // Execute through command history if available
        if (state.CommandHistory != null)
        {
            state.CommandHistory.Execute(command);
        }
        else
        {
            command.Execute();
        }

        // Trigger column width update for affected columns
        if (command.AffectedColumns.Any())
        {
            state.OnColumnWidthUpdateRequested(command.AffectedColumns);
        }

        // Move cursor based on paste type
        if (yank.SourceType == VisualType.Line)
        {
            // Move cursor to the first inserted row
            state.CursorPosition = new GridPosition(startPos.Row + 1, startPos.Column);
        }
        else if (yank.SourceType == VisualType.Block)
        {
            // Move cursor to the first inserted column
            state.CursorPosition = new GridPosition(startPos.Row, startPos.Column + 1);
        }

        // Record the change for dot command
        state.LastChange = new LastChange
        {
            Type = ChangeType.PasteAfter,
            Count = state.CountPrefix ?? 1,
            PastedContent = yankSnapshot,
            PasteBefore = false
        };

        return true;
    }

    private bool PasteBeforeCursor(VimState state, TsvDocument document)
    {
        // Try to use LastYank, or fallback to clipboard
        var yank = state.LastYank ?? ClipboardHelper.ReadFromClipboard();

        if (yank == null)
            return true; // Key is handled, but nothing to paste

        var startPos = state.CursorPosition;

        // Snapshot the yank content for dot command replay
        var yankSnapshot = new YankedContent
        {
            Values = (string[,])yank.Values.Clone(),
            SourceType = yank.SourceType,
            Rows = yank.Rows,
            Columns = yank.Columns
        };

        // For character paste, move cursor left before pasting
        if (yank.SourceType == VisualType.Character)
        {
            // Move cursor one cell to the left
            var newPos = startPos.MoveLeft(1).Clamp(document);
            state.CursorPosition = newPos;
            startPos = newPos;
        }

        // Create and execute paste command (paste before: pasteBefore = true)
        var command = new Commands.PasteCommand(document, startPos, yank, pasteBefore: true);

        // Execute through command history if available
        if (state.CommandHistory != null)
        {
            state.CommandHistory.Execute(command);
        }
        else
        {
            command.Execute();
        }

        // Trigger column width update for affected columns
        if (command.AffectedColumns.Any())
        {
            state.OnColumnWidthUpdateRequested(command.AffectedColumns);
        }

        // Move cursor based on paste type
        if (yank.SourceType == VisualType.Line)
        {
            // Move cursor to the first inserted row (same as current position since we inserted above)
            state.CursorPosition = new GridPosition(startPos.Row, startPos.Column);
        }
        else if (yank.SourceType == VisualType.Block)
        {
            // Move cursor to the first inserted column (same as current position since we inserted to the left)
            state.CursorPosition = new GridPosition(startPos.Row, startPos.Column);
        }

        // Record the change for dot command
        state.LastChange = new LastChange
        {
            Type = ChangeType.PasteBefore,
            Count = state.CountPrefix ?? 1,
            PastedContent = yankSnapshot,
            PasteBefore = true
        };

        return true;
    }

    private bool ClearState(VimState state)
    {
        // If state is already clear and search is active, clear search (Esc twice)
        if (state.PendingKeys.Keys.Count == 0 && state.CountPrefix == null && state.IsSearchActive)
        {
            state.ClearSearch();
            return true;
        }

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

        // Copy to system clipboard
        ClipboardHelper.CopyToClipboard(state.LastYank);

        // Notify that a yank was performed (so other tabs can clear their LastYank)
        state.OnYankPerformed();

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

        // Copy to system clipboard
        ClipboardHelper.CopyToClipboard(state.LastYank);

        // Notify that a yank was performed
        state.OnYankPerformed();

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

        // Record the change for dot command
        state.LastChange = new LastChange
        {
            Type = ChangeType.DeleteRow,
            Count = state.CountPrefix ?? 1
        };

        // Clear pending keys
        state.PendingKeys.Clear();

        // Force cursor position refresh to trigger scroll into view
        state.RefreshCursorPositionBinding();

        return true;
    }

    private bool Undo(VimState state, TsvDocument document)
    {
        if (state.CommandHistory != null && state.CommandHistory.CanUndo)
        {
            state.CommandHistory.Undo();

            // Trigger column width update for all columns after undo
            var allColumns = Enumerable.Range(0, document.ColumnCount);
            state.OnColumnWidthUpdateRequested(allColumns);
        }
        return true; // Key is always handled, even if there's nothing to undo
    }

    private bool Redo(VimState state, TsvDocument document)
    {
        if (state.CommandHistory != null && state.CommandHistory.CanRedo)
        {
            state.CommandHistory.Redo();

            // Trigger column width update for all columns after redo
            var allColumns = Enumerable.Range(0, document.ColumnCount);
            state.OnColumnWidthUpdateRequested(allColumns);
        }
        return true; // Key is always handled, even if there's nothing to redo
    }

    private bool StartSearch(VimState state)
    {
        state.SwitchMode(VimMode.Command);
        return true;
    }

    private bool StartExCommand(VimState state)
    {
        state.CurrentCommandType = CommandType.ExCommand;
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

        // Copy to system clipboard
        ClipboardHelper.CopyToClipboard(state.LastYank);

        // Notify that a yank was performed
        state.OnYankPerformed();

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

        // Copy to system clipboard
        ClipboardHelper.CopyToClipboard(state.LastYank);

        // Notify that a yank was performed
        state.OnYankPerformed();

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

        // Record the change for dot command
        state.LastChange = new LastChange
        {
            Type = ChangeType.DeleteWord,
            Count = state.CountPrefix ?? 1
        };

        state.PendingKeys.Clear();
        return true;
    }

    private bool ChangeWord(VimState state, TsvDocument document)
    {
        // In TSV editor, "word" = current cell
        // ciw and caw both yank, clear the current cell value, and enter insert mode
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

        // Copy to system clipboard
        ClipboardHelper.CopyToClipboard(state.LastYank);

        // Notify that a yank was performed
        state.OnYankPerformed();

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

        // Clear pending keys before switching mode
        state.PendingKeys.Clear();

        // Enter insert mode at the start of the cell
        state.CellEditCaretPosition = CellEditCaretPosition.Start;
        state.PendingInsertType = ChangeType.ChangeWord;
        state.InsertModeStartPosition = state.CursorPosition;
        state.SwitchMode(VimMode.Insert);

        return true;
    }

    private bool YankCurrentCell(VimState state, TsvDocument document)
    {
        // Yank current cell with Ctrl+C
        if (state.CursorPosition.Row >= document.RowCount)
        {
            return true;
        }

        var cell = document.GetCell(state.CursorPosition);
        if (cell == null)
        {
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

        // Copy to system clipboard
        ClipboardHelper.CopyToClipboard(state.LastYank);

        // Notify that a yank was performed
        state.OnYankPerformed();

        return true;
    }

    private bool DeleteCurrentCell(VimState state, TsvDocument document)
    {
        // Delete current cell with 'x' (yank and clear)
        if (state.CursorPosition.Row >= document.RowCount)
        {
            return true;
        }

        var cell = document.GetCell(state.CursorPosition);
        if (cell == null)
        {
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

        // Copy to system clipboard
        ClipboardHelper.CopyToClipboard(state.LastYank);

        // Notify that a yank was performed
        state.OnYankPerformed();

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

        // Record the change for dot command
        state.LastChange = new LastChange
        {
            Type = ChangeType.DeleteCell,
            Count = state.CountPrefix ?? 1
        };

        return true;
    }

    private bool MoveToNextNonEmptyCell(VimState state, TsvDocument document)
    {
        // Start searching from the cell after the current position
        int startRow = state.CursorPosition.Row;
        int startCol = state.CursorPosition.Column + 1;

        // Search in the current row first
        for (int col = startCol; col < document.ColumnCount; col++)
        {
            var cell = document.GetCell(startRow, col);
            if (cell != null && !string.IsNullOrEmpty(cell.Value))
            {
                state.CursorPosition = new GridPosition(startRow, col);
                return true;
            }
        }

        // Search in subsequent rows
        for (int row = startRow + 1; row < document.RowCount; row++)
        {
            for (int col = 0; col < document.ColumnCount; col++)
            {
                var cell = document.GetCell(row, col);
                if (cell != null && !string.IsNullOrEmpty(cell.Value))
                {
                    state.CursorPosition = new GridPosition(row, col);
                    return true;
                }
            }
        }

        // No non-empty cell found, stay at current position
        return true;
    }

    private bool MoveToPreviousNonEmptyCell(VimState state, TsvDocument document)
    {
        // Start searching from the cell before the current position
        int startRow = state.CursorPosition.Row;
        int startCol = state.CursorPosition.Column - 1;

        // Search in the current row first (backwards)
        for (int col = startCol; col >= 0; col--)
        {
            var cell = document.GetCell(startRow, col);
            if (cell != null && !string.IsNullOrEmpty(cell.Value))
            {
                state.CursorPosition = new GridPosition(startRow, col);
                return true;
            }
        }

        // Search in previous rows (backwards)
        for (int row = startRow - 1; row >= 0; row--)
        {
            for (int col = document.ColumnCount - 1; col >= 0; col--)
            {
                var cell = document.GetCell(row, col);
                if (cell != null && !string.IsNullOrEmpty(cell.Value))
                {
                    state.CursorPosition = new GridPosition(row, col);
                    return true;
                }
            }
        }

        // No non-empty cell found, stay at current position
        return true;
    }

    private bool HandlePendingSpace(VimState state)
    {
        // Add Space to pending keys, wait for next key (e.g., 'w' for save)
        state.PendingKeys.Add(Key.Space);
        return true;
    }

    private bool SaveFile(VimState state)
    {
        // Trigger save operation via VimState event
        state.OnSaveRequested();
        state.PendingKeys.Clear();
        return true;
    }

    private bool MoveLeftAndInsert(VimState state, TsvDocument document)
    {
        // Move to the left cell and enter Insert mode (Shift+I)
        var newPos = state.CursorPosition.MoveLeft(1).Clamp(document);
        state.CursorPosition = newPos;

        // Set caret position to start of cell
        state.CellEditCaretPosition = CellEditCaretPosition.End;
        state.SwitchMode(VimMode.Insert);
        return true;
    }

    private bool MoveRightAndInsert(VimState state, TsvDocument document)
    {
        // Move to the right cell and enter Insert mode (Shift+A)
        var newPos = state.CursorPosition.MoveRight(1).Clamp(document);
        state.CursorPosition = newPos;

        // Set caret position to end of cell
        state.CellEditCaretPosition = CellEditCaretPosition.End;
        state.SwitchMode(VimMode.Insert);
        return true;
    }

    private bool SwitchToPreviousTab(VimState state)
    {
        // Trigger previous tab operation via VimState event
        state.OnPreviousTabRequested();
        return true;
    }

    private bool SwitchToNextTab(VimState state)
    {
        // Trigger next tab operation via VimState event
        state.OnNextTabRequested();
        return true;
    }

    /// <summary>
    /// Repeats the last change operation (dot command)
    /// </summary>
    private bool RepeatLastChange(VimState state, TsvDocument document, int repeatCount)
    {
        if (state.LastChange == null || state.LastChange.Type == ChangeType.None)
        {
            return true;  // No change to repeat
        }

        var change = state.LastChange;

        // Multiply the original count by the repeat count
        // Original: 3dd (delete 3 lines)
        // Replay: 2. (repeat twice: delete 2×3=6 lines)
        int effectiveCount = change.Count * repeatCount;

        switch (change.Type)
        {
            case ChangeType.DeleteCell:
                return RepeatDeleteCell(state, document, effectiveCount);

            case ChangeType.DeleteRow:
                return RepeatDeleteRow(state, document, effectiveCount);

            case ChangeType.DeleteWord:
                return RepeatDeleteWord(state, document, effectiveCount);

            case ChangeType.PasteAfter:
            case ChangeType.PasteBefore:
                return RepeatPaste(state, document, change, effectiveCount);

            case ChangeType.Insert:
            case ChangeType.InsertAfter:
                return RepeatInsert(state, document, change, effectiveCount);

            case ChangeType.InsertLineBelow:
            case ChangeType.InsertLineAbove:
                return RepeatInsertLine(state, document, change, effectiveCount);

            case ChangeType.ChangeLine:
                return RepeatChangeLine(state, document, change, effectiveCount);

            case ChangeType.ChangeWord:
                return RepeatChangeWord(state, document, change, effectiveCount);

            default:
                return true;
        }
    }

    private bool RepeatDeleteCell(VimState state, TsvDocument document, int count)
    {
        // Repeat delete cell 'count' times
        for (int i = 0; i < count; i++)
        {
            if (state.CursorPosition.Row >= document.RowCount)
                break;

            DeleteCurrentCell(state, document);
        }

        return true;
    }

    private bool RepeatDeleteRow(VimState state, TsvDocument document, int count)
    {
        // Delete 'count' rows starting from current position
        for (int i = 0; i < count; i++)
        {
            if (state.CursorPosition.Row >= document.RowCount)
                break;

            // Store the count prefix temporarily
            var oldCountPrefix = state.CountPrefix;
            state.CountPrefix = 1;  // Delete one row at a time

            DeleteLine(state, document);

            // Restore original count prefix
            state.CountPrefix = oldCountPrefix;
        }

        return true;
    }

    private bool RepeatDeleteWord(VimState state, TsvDocument document, int count)
    {
        // Repeat delete word 'count' times
        for (int i = 0; i < count; i++)
        {
            if (state.CursorPosition.Row >= document.RowCount)
                break;

            DeleteWord(state, document);
        }

        return true;
    }

    private bool RepeatPaste(VimState state, TsvDocument document, LastChange change, int count)
    {
        if (change.PastedContent == null)
            return true;

        // Temporarily set LastYank to the saved content
        var previousYank = state.LastYank;
        state.LastYank = change.PastedContent;

        // Store the count prefix temporarily
        var oldCountPrefix = state.CountPrefix;
        state.CountPrefix = 1;  // Paste one at a time

        // Perform paste 'count' times
        for (int i = 0; i < count; i++)
        {
            if (change.PasteBefore)
            {
                PasteBeforeCursor(state, document);
            }
            else
            {
                PasteAfterCursor(state, document);
            }
        }

        // Restore original count prefix
        state.CountPrefix = oldCountPrefix;

        // Restore previous yank
        state.LastYank = previousYank;

        return true;
    }

    private bool RepeatInsert(VimState state, TsvDocument document, LastChange change, int count)
    {
        if (string.IsNullOrEmpty(change.InsertedText))
            return true;

        var affectedColumns = new HashSet<int>();

        // For insert operations (i, a), apply the inserted text to current cell
        // based on the caret position (Start = prepend, End = append)
        for (int i = 0; i < count; i++)
        {
            var currentPos = state.CursorPosition;

            if (currentPos.Row >= document.RowCount)
                break;

            var currentCell = document.GetCell(currentPos);
            string currentValue = currentCell?.Value ?? string.Empty;

            // Apply the inserted text based on original caret position
            string newValue;
            if (change.CaretPosition == CellEditCaretPosition.Start)
            {
                // 'i' - insert at beginning
                newValue = change.InsertedText + currentValue;
            }
            else
            {
                // 'a' - append at end
                newValue = currentValue + change.InsertedText;
            }

            var command = new Commands.EditCellCommand(document, currentPos, newValue);

            if (state.CommandHistory != null)
            {
                state.CommandHistory.Execute(command);
            }
            else
            {
                command.Execute();
            }

            affectedColumns.Add(currentPos.Column);

            // Move right after each insert (for multiple repeats)
            if (i < count - 1)
            {
                state.CursorPosition = currentPos.MoveRight(1).Clamp(document);
            }
        }

        // Trigger column width update for affected columns
        if (affectedColumns.Count > 0)
        {
            state.OnColumnWidthUpdateRequested(affectedColumns);
        }

        return true;
    }

    private bool RepeatInsertLine(VimState state, TsvDocument document, LastChange change, int count)
    {
        if (string.IsNullOrEmpty(change.InsertedText))
            return true;

        var currentPos = state.CursorPosition;

        // For insert line operations (o, O), apply the text 'count' times
        for (int i = 0; i < count; i++)
        {
            int insertRow;

            if (change.Type == ChangeType.InsertLineBelow)
            {
                // Insert a new row below the current row
                insertRow = currentPos.Row + 1 + i;
            }
            else // ChangeType.InsertLineAbove
            {
                // Insert a new row above the current row
                insertRow = currentPos.Row + i;
            }

            var insertRowCommand = new Commands.InsertRowCommand(document, insertRow);

            if (state.CommandHistory != null)
            {
                state.CommandHistory.Execute(insertRowCommand);
            }
            else
            {
                insertRowCommand.Execute();
            }

            // Set the cell value at current column to the inserted text
            var editCommand = new Commands.EditCellCommand(document,
                new GridPosition(insertRow, currentPos.Column), change.InsertedText);

            if (state.CommandHistory != null)
            {
                state.CommandHistory.Execute(editCommand);
            }
            else
            {
                editCommand.Execute();
            }

            // Update cursor position to the last inserted row
            if (i == count - 1)
            {
                state.CursorPosition = new GridPosition(insertRow, currentPos.Column);
            }
        }

        // Trigger column width update for the affected column
        state.OnColumnWidthUpdateRequested(new[] { currentPos.Column });

        return true;
    }

    private bool RepeatChangeLine(VimState state, TsvDocument document, LastChange change, int count)
    {
        if (string.IsNullOrEmpty(change.InsertedText))
            return true;

        var currentPos = state.CursorPosition;

        // For change line operations (cc), apply the text 'count' times
        for (int i = 0; i < count; i++)
        {
            if (currentPos.Row >= document.RowCount)
                break;

            // Clear all cells in the current row
            var row = document.Rows[currentPos.Row];
            var columnCount = row.Cells.Count;
            var positions = new List<GridPosition>();

            for (int c = 0; c < columnCount; c++)
            {
                positions.Add(new GridPosition(currentPos.Row, c));
            }

            var clearCommand = new Commands.BulkEditCellsCommand(document, positions, string.Empty);

            if (state.CommandHistory != null)
            {
                state.CommandHistory.Execute(clearCommand);
            }
            else
            {
                clearCommand.Execute();
            }

            // Set the first cell value to the inserted text
            var editCommand = new Commands.EditCellCommand(document,
                new GridPosition(currentPos.Row, 0), change.InsertedText);

            if (state.CommandHistory != null)
            {
                state.CommandHistory.Execute(editCommand);
            }
            else
            {
                editCommand.Execute();
            }

            // Move down to next row for next iteration
            if (i < count - 1)
            {
                currentPos = currentPos.MoveDown(1).Clamp(document);
            }
        }

        // Trigger column width update for the first column (where text was inserted)
        state.OnColumnWidthUpdateRequested(new[] { 0 });

        return true;
    }

    private bool RepeatChangeWord(VimState state, TsvDocument document, LastChange change, int count)
    {
        if (string.IsNullOrEmpty(change.InsertedText))
            return true;

        var affectedColumns = new HashSet<int>();

        // For change word operations (ciw, caw), apply the text 'count' times at current position
        for (int i = 0; i < count; i++)
        {
            var currentPos = state.CursorPosition;

            if (currentPos.Row >= document.RowCount)
                break;

            // Simply set the cell value to the inserted text
            var command = new Commands.EditCellCommand(document, currentPos, change.InsertedText);

            if (state.CommandHistory != null)
            {
                state.CommandHistory.Execute(command);
            }
            else
            {
                command.Execute();
            }

            affectedColumns.Add(currentPos.Column);

            // Move right after each change (for multiple repeats)
            if (i < count - 1)
            {
                state.CursorPosition = currentPos.MoveRight(1).Clamp(document);
            }
        }

        // Trigger column width update for affected columns
        if (affectedColumns.Count > 0)
        {
            state.OnColumnWidthUpdateRequested(affectedColumns);
        }

        return true;
    }

    private bool HandlePendingZ(VimState state)
    {
        // Add 'z' to pending keys, wait for second 'z'
        state.PendingKeys.Add(Key.Z);
        return true;
    }

    private bool ScrollToCenter(VimState state)
    {
        // Trigger scroll to center operation via VimState event
        System.Diagnostics.Debug.WriteLine("[NormalMode] ScrollToCenter called");
        state.OnScrollToCenterRequested();
        state.PendingKeys.Clear();
        return true;
    }

    private bool HandlePendingC(VimState state)
    {
        // Add 'c' to pending keys, wait for second 'c'
        state.PendingKeys.Add(Key.C);
        return true;
    }

    private bool ChangeLine(VimState state, TsvDocument document)
    {
        // Clear entire row (not delete) and enter insert mode
        if (state.CursorPosition.Row >= document.RowCount)
        {
            state.PendingKeys.Clear();
            return true; // Key is handled, but no row to change
        }

        var row = document.Rows[state.CursorPosition.Row];
        var columnCount = row.Cells.Count;

        // First, yank the line (like Vim: change = yank + delete + insert)
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

        // Copy to system clipboard
        ClipboardHelper.CopyToClipboard(state.LastYank);

        // Notify that a yank was performed
        state.OnYankPerformed();

        // Then clear all cells in the row (not delete the row)
        var positions = new List<GridPosition>();
        for (int c = 0; c < columnCount; c++)
        {
            positions.Add(new GridPosition(state.CursorPosition.Row, c));
        }

        var command = new Commands.BulkEditCellsCommand(document, positions, string.Empty);

        // Execute through command history if available
        if (state.CommandHistory != null)
        {
            state.CommandHistory.Execute(command);
        }
        else
        {
            command.Execute();
        }

        // Move cursor to first column and enter insert mode
        state.CursorPosition = new GridPosition(state.CursorPosition.Row, 0);
        state.CellEditCaretPosition = CellEditCaretPosition.Start;
        state.PendingInsertType = ChangeType.ChangeLine;
        state.InsertModeStartPosition = state.CursorPosition;

        // Clear pending keys before switching mode
        state.PendingKeys.Clear();

        // Switch to insert mode
        state.SwitchMode(VimMode.Insert);

        return true;
    }

    private bool AlignAllColumns(VimState state, TsvDocument document)
    {
        // Align all columns by padding cells with spaces to match the maximum width in each column
        if (document.RowCount == 0 || document.ColumnCount == 0)
            return true; // Key is handled, but nothing to align

        var command = new Commands.AlignColumnsCommand(document);

        // Execute through command history if available
        if (state.CommandHistory != null)
        {
            state.CommandHistory.Execute(command);
        }
        else
        {
            command.Execute();
        }

        return true;
    }

    private bool MoveToNextEmptyRow(VimState state, TsvDocument document, int count)
    {
        // Move to the next empty row (all cells are empty or whitespace)
        // Repeat 'count' times
        int currentRow = state.CursorPosition.Row;

        for (int i = 0; i < count; i++)
        {
            int nextEmptyRow = FindNextEmptyRow(document, currentRow);
            if (nextEmptyRow >= 0)
            {
                currentRow = nextEmptyRow;
            }
            else
            {
                // No more empty rows found, move to the last row
                currentRow = document.RowCount - 1;
                break;
            }
        }

        state.CursorPosition = new GridPosition(currentRow, state.CursorPosition.Column).Clamp(document);
        return true;
    }

    private bool MoveToPreviousEmptyRow(VimState state, TsvDocument document, int count)
    {
        // Move to the previous empty row (all cells are empty or whitespace)
        // Repeat 'count' times
        int currentRow = state.CursorPosition.Row;

        for (int i = 0; i < count; i++)
        {
            int prevEmptyRow = FindPreviousEmptyRow(document, currentRow);
            if (prevEmptyRow >= 0)
            {
                currentRow = prevEmptyRow;
            }
            else
            {
                // No more empty rows found, move to the first row
                currentRow = 0;
                break;
            }
        }

        state.CursorPosition = new GridPosition(currentRow, state.CursorPosition.Column).Clamp(document);
        return true;
    }

    private int FindNextEmptyRow(TsvDocument document, int startRow)
    {
        // Search for the next empty row after startRow
        for (int row = startRow + 1; row < document.RowCount; row++)
        {
            if (IsEmptyRow(document, row))
            {
                return row;
            }
        }
        return -1; // No empty row found
    }

    private int FindPreviousEmptyRow(TsvDocument document, int startRow)
    {
        // Search for the previous empty row before startRow
        for (int row = startRow - 1; row >= 0; row--)
        {
            if (IsEmptyRow(document, row))
            {
                return row;
            }
        }
        return -1; // No empty row found
    }

    private bool IsEmptyRow(TsvDocument document, int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= document.RowCount)
            return false;

        var row = document.Rows[rowIndex];
        return row.Cells.All(cell => string.IsNullOrWhiteSpace(cell.Value));
    }
}
