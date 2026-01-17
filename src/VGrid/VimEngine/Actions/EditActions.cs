using VGrid.Commands;
using VGrid.Models;
using VGrid.VimEngine.KeyBinding;

namespace VGrid.VimEngine.Actions;

/// <summary>
/// Edit-related Vim actions
/// </summary>
public static class EditActions
{
    public class DeleteLineAction : IVimAction
    {
        public string Name => "delete_line";

        public bool Execute(VimActionContext context)
        {
            var state = context.State;
            var document = context.Document;

            if (state.CursorPosition.Row >= document.RowCount)
                return true;

            var row = document.Rows[state.CursorPosition.Row];
            var columnCount = row.Cells.Count;

            // Yank the line first
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

            ClipboardHelper.CopyToClipboard(state.LastYank);
            state.OnYankPerformed();

            // Delete the row
            var command = new DeleteRowCommand(document, state.CursorPosition.Row);
            if (state.CommandHistory != null)
            {
                state.CommandHistory.Execute(command);
            }
            else
            {
                command.Execute();
            }

            // Adjust cursor position
            if (state.CursorPosition.Row >= document.RowCount && document.RowCount > 0)
            {
                state.CursorPosition = new GridPosition(document.RowCount - 1, state.CursorPosition.Column);
            }

            state.LastChange = new LastChange
            {
                Type = ChangeType.DeleteRow,
                Count = context.Count
            };

            return true;
        }
    }

    public class DeleteCellAction : IVimAction
    {
        public string Name => "delete_cell";

        public bool Execute(VimActionContext context)
        {
            var state = context.State;
            var document = context.Document;

            if (state.CursorPosition.Row >= document.RowCount)
                return true;

            var cell = document.GetCell(state.CursorPosition);
            if (cell == null)
                return true;

            // Yank the cell
            string[,] values = new string[1, 1];
            values[0, 0] = cell.Value;

            state.LastYank = new YankedContent
            {
                Values = values,
                SourceType = VisualType.Character,
                Rows = 1,
                Columns = 1
            };

            ClipboardHelper.CopyToClipboard(state.LastYank);
            state.OnYankPerformed();

            // Clear the cell
            var command = new EditCellCommand(document, state.CursorPosition, string.Empty);
            if (state.CommandHistory != null)
            {
                state.CommandHistory.Execute(command);
            }
            else
            {
                command.Execute();
            }

            state.LastChange = new LastChange
            {
                Type = ChangeType.DeleteCell,
                Count = context.Count
            };

            return true;
        }
    }

    public class DeleteWordAction : IVimAction
    {
        public string Name => "delete_word";

        public bool Execute(VimActionContext context)
        {
            // Same as delete_cell in TSV context
            return new DeleteCellAction().Execute(context);
        }
    }

    public class DeleteSelectionAction : IVimAction
    {
        public string Name => "delete_selection";

        public bool Execute(VimActionContext context)
        {
            var state = context.State;
            var document = context.Document;

            if (state.CurrentSelection == null)
                return true;

            var selection = state.CurrentSelection;

            // Yank the selection
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
                SourceType = selection.Type,
                Rows = rows,
                Columns = cols
            };

            ClipboardHelper.CopyToClipboard(state.LastYank);
            state.OnYankPerformed();

            // Delete the selection
            var command = new DeleteSelectionCommand(document, selection);
            if (state.CommandHistory != null)
            {
                state.CommandHistory.Execute(command);
            }
            else
            {
                command.Execute();
            }

            state.LastChange = new LastChange
            {
                Type = ChangeType.VisualDelete,
                Count = 1,
                BulkEditRange = new SelectionRange(selection.Type, selection.Start, selection.End)
            };

            return true;
        }
    }

    public class YankLineAction : IVimAction
    {
        public string Name => "yank_line";

        public bool Execute(VimActionContext context)
        {
            var state = context.State;
            var document = context.Document;

            if (state.CursorPosition.Row >= document.RowCount)
                return true;

            var row = document.Rows[state.CursorPosition.Row];
            var columnCount = row.Cells.Count;
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

            ClipboardHelper.CopyToClipboard(state.LastYank);
            state.OnYankPerformed();

            return true;
        }
    }

    public class YankCellAction : IVimAction
    {
        public string Name => "yank_cell";

        public bool Execute(VimActionContext context)
        {
            var state = context.State;
            var document = context.Document;

            if (state.CursorPosition.Row >= document.RowCount)
                return true;

            var cell = document.GetCell(state.CursorPosition);
            if (cell == null)
                return true;

            string[,] values = new string[1, 1];
            values[0, 0] = cell.Value;

            state.LastYank = new YankedContent
            {
                Values = values,
                SourceType = VisualType.Character,
                Rows = 1,
                Columns = 1
            };

            ClipboardHelper.CopyToClipboard(state.LastYank);
            state.OnYankPerformed();

            return true;
        }
    }

    public class YankWordAction : IVimAction
    {
        public string Name => "yank_word";

        public bool Execute(VimActionContext context)
        {
            // Same as yank_cell in TSV context
            return new YankCellAction().Execute(context);
        }
    }

    public class YankSelectionAction : IVimAction
    {
        public string Name => "yank_selection";

        public bool Execute(VimActionContext context)
        {
            var state = context.State;
            var document = context.Document;

            if (state.CurrentSelection == null)
                return true;

            var selection = state.CurrentSelection;

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
                SourceType = selection.Type,
                Rows = rows,
                Columns = cols
            };

            ClipboardHelper.CopyToClipboard(state.LastYank);
            state.OnYankPerformed();

            return true;
        }
    }

    public class PasteAfterAction : IVimAction
    {
        public string Name => "paste_after";

        public bool Execute(VimActionContext context)
        {
            var state = context.State;
            var document = context.Document;

            var yank = state.LastYank ?? ClipboardHelper.ReadFromClipboard();
            if (yank == null)
                return true;

            var startPos = state.CursorPosition;

            var command = new PasteCommand(document, startPos, yank, pasteBefore: false);
            if (state.CommandHistory != null)
            {
                state.CommandHistory.Execute(command);
            }
            else
            {
                command.Execute();
            }

            if (command.AffectedColumns.Any())
            {
                state.OnColumnWidthUpdateRequested(command.AffectedColumns);
            }

            // Move cursor based on paste type
            if (yank.SourceType == VisualType.Line)
            {
                state.CursorPosition = new GridPosition(startPos.Row + 1, startPos.Column);
            }
            else if (yank.SourceType == VisualType.Block)
            {
                state.CursorPosition = new GridPosition(startPos.Row, startPos.Column + 1);
            }

            state.LastChange = new LastChange
            {
                Type = ChangeType.PasteAfter,
                Count = context.Count,
                PastedContent = new YankedContent
                {
                    Values = (string[,])yank.Values.Clone(),
                    SourceType = yank.SourceType,
                    Rows = yank.Rows,
                    Columns = yank.Columns
                },
                PasteBefore = false
            };

            return true;
        }
    }

    public class PasteBeforeAction : IVimAction
    {
        public string Name => "paste_before";

        public bool Execute(VimActionContext context)
        {
            var state = context.State;
            var document = context.Document;

            var yank = state.LastYank ?? ClipboardHelper.ReadFromClipboard();
            if (yank == null)
                return true;

            var startPos = state.CursorPosition;

            // For character paste, move cursor left before pasting
            if (yank.SourceType == VisualType.Character)
            {
                var newPos = startPos.MoveLeft(1).Clamp(document);
                state.CursorPosition = newPos;
                startPos = newPos;
            }

            var command = new PasteCommand(document, startPos, yank, pasteBefore: true);
            if (state.CommandHistory != null)
            {
                state.CommandHistory.Execute(command);
            }
            else
            {
                command.Execute();
            }

            if (command.AffectedColumns.Any())
            {
                state.OnColumnWidthUpdateRequested(command.AffectedColumns);
            }

            state.LastChange = new LastChange
            {
                Type = ChangeType.PasteBefore,
                Count = context.Count,
                PastedContent = new YankedContent
                {
                    Values = (string[,])yank.Values.Clone(),
                    SourceType = yank.SourceType,
                    Rows = yank.Rows,
                    Columns = yank.Columns
                },
                PasteBefore = true
            };

            return true;
        }
    }

    public class UndoAction : IVimAction
    {
        public string Name => "undo";

        public bool Execute(VimActionContext context)
        {
            if (context.State.CommandHistory != null && context.State.CommandHistory.CanUndo)
            {
                context.State.CommandHistory.Undo();
            }
            return true;
        }
    }

    public class RedoAction : IVimAction
    {
        public string Name => "redo";

        public bool Execute(VimActionContext context)
        {
            if (context.State.CommandHistory != null && context.State.CommandHistory.CanRedo)
            {
                context.State.CommandHistory.Redo();
            }
            return true;
        }
    }

    public class AlignSelectionAction : IVimAction
    {
        public string Name => "align_selection";

        public bool Execute(VimActionContext context)
        {
            var document = context.Document;

            if (document.RowCount == 0 || document.ColumnCount == 0)
                return true;

            var command = new AlignColumnsCommand(document);
            if (context.State.CommandHistory != null)
            {
                context.State.CommandHistory.Execute(command);
            }
            else
            {
                command.Execute();
            }

            return true;
        }
    }

    public class ChangeLineAction : IVimAction
    {
        public string Name => "change_line";

        public bool Execute(VimActionContext context)
        {
            var state = context.State;
            var document = context.Document;

            if (state.CursorPosition.Row >= document.RowCount)
                return true;

            var row = document.Rows[state.CursorPosition.Row];
            var columnCount = row.Cells.Count;

            // Yank the line first
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

            ClipboardHelper.CopyToClipboard(state.LastYank);
            state.OnYankPerformed();

            // Clear all cells in the row
            var positions = new List<GridPosition>();
            for (int c = 0; c < columnCount; c++)
            {
                positions.Add(new GridPosition(state.CursorPosition.Row, c));
            }

            var command = new BulkEditCellsCommand(document, positions, string.Empty);
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
            state.SwitchMode(VimMode.Insert);

            return true;
        }
    }

    public class ChangeWordAction : IVimAction
    {
        public string Name => "change_word";

        public bool Execute(VimActionContext context)
        {
            var state = context.State;
            var document = context.Document;

            if (state.CursorPosition.Row >= document.RowCount)
                return true;

            var cell = document.GetCell(state.CursorPosition);
            if (cell == null)
                return true;

            // Yank the cell
            string[,] values = new string[1, 1];
            values[0, 0] = cell.Value;

            state.LastYank = new YankedContent
            {
                Values = values,
                SourceType = VisualType.Character,
                Rows = 1,
                Columns = 1
            };

            ClipboardHelper.CopyToClipboard(state.LastYank);
            state.OnYankPerformed();

            // Clear the cell
            var command = new EditCellCommand(document, state.CursorPosition, string.Empty);
            if (state.CommandHistory != null)
            {
                state.CommandHistory.Execute(command);
            }
            else
            {
                command.Execute();
            }

            // Enter insert mode
            state.CellEditCaretPosition = CellEditCaretPosition.Start;
            state.PendingInsertType = ChangeType.ChangeWord;
            state.InsertModeStartPosition = state.CursorPosition;
            state.SwitchMode(VimMode.Insert);

            return true;
        }
    }
}
