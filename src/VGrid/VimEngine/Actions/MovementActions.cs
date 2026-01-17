using VGrid.Models;
using VGrid.VimEngine.KeyBinding;

namespace VGrid.VimEngine.Actions;

/// <summary>
/// Movement-related Vim actions
/// </summary>
public static class MovementActions
{
    public class MoveLeftAction : IVimAction
    {
        public string Name => "move_left";

        public bool Execute(VimActionContext context)
        {
            var newPos = context.State.CursorPosition.MoveLeft(context.Count).Clamp(context.Document);
            context.State.CursorPosition = newPos;
            return true;
        }
    }

    public class MoveRightAction : IVimAction
    {
        public string Name => "move_right";

        public bool Execute(VimActionContext context)
        {
            var newPos = context.State.CursorPosition.MoveRight(context.Count).Clamp(context.Document);
            context.State.CursorPosition = newPos;
            return true;
        }
    }

    public class MoveUpAction : IVimAction
    {
        public string Name => "move_up";

        public bool Execute(VimActionContext context)
        {
            var newPos = context.State.CursorPosition.MoveUp(context.Count).Clamp(context.Document);
            context.State.CursorPosition = newPos;
            return true;
        }
    }

    public class MoveDownAction : IVimAction
    {
        public string Name => "move_down";

        public bool Execute(VimActionContext context)
        {
            var newPos = context.State.CursorPosition.MoveDown(context.Count).Clamp(context.Document);
            context.State.CursorPosition = newPos;
            return true;
        }
    }

    public class MoveUp10Action : IVimAction
    {
        public string Name => "move_up_10";

        public bool Execute(VimActionContext context)
        {
            var newPos = context.State.CursorPosition.MoveUp(10 * context.Count).Clamp(context.Document);
            context.State.CursorPosition = newPos;
            return true;
        }
    }

    public class MoveDown10Action : IVimAction
    {
        public string Name => "move_down_10";

        public bool Execute(VimActionContext context)
        {
            var newPos = context.State.CursorPosition.MoveDown(10 * context.Count).Clamp(context.Document);
            context.State.CursorPosition = newPos;
            return true;
        }
    }

    public class MoveToLineStartAction : IVimAction
    {
        public string Name => "move_to_line_start";

        public bool Execute(VimActionContext context)
        {
            context.State.CursorPosition = context.State.CursorPosition.MoveToLineStart();
            return true;
        }
    }

    public class MoveToFirstCellAction : IVimAction
    {
        public string Name => "move_to_first_cell";

        public bool Execute(VimActionContext context)
        {
            context.State.CursorPosition = new GridPosition(0, 0);
            return true;
        }
    }

    public class MoveToLastColumnAction : IVimAction
    {
        public string Name => "move_to_last_column";

        public bool Execute(VimActionContext context)
        {
            int currentRow = context.State.CursorPosition.Row;
            if (currentRow >= context.Document.RowCount)
                return true;

            var row = context.Document.Rows[currentRow];
            int lastNonEmptyCol = -1;

            for (int col = row.Cells.Count - 1; col >= 0; col--)
            {
                if (!string.IsNullOrEmpty(row.Cells[col].Value))
                {
                    lastNonEmptyCol = col;
                    break;
                }
            }

            if (lastNonEmptyCol >= 0)
            {
                context.State.CursorPosition = new GridPosition(currentRow, lastNonEmptyCol);
            }

            return true;
        }
    }

    public class MoveToFirstLineAction : IVimAction
    {
        public string Name => "move_to_first_line";

        public bool Execute(VimActionContext context)
        {
            context.State.CursorPosition = context.State.CursorPosition.MoveToFirstRow();
            return true;
        }
    }

    public class MoveToLastLineAction : IVimAction
    {
        public string Name => "move_to_last_line";

        public bool Execute(VimActionContext context)
        {
            var document = context.Document;
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

            int targetRow = lastNonEmptyRow >= 0 ? lastNonEmptyRow : 0;
            context.State.CursorPosition = new GridPosition(targetRow, context.State.CursorPosition.Column).Clamp(document);

            return true;
        }
    }

    public class MoveToNextWordAction : IVimAction
    {
        public string Name => "move_to_next_word";

        public bool Execute(VimActionContext context)
        {
            var document = context.Document;
            int startRow = context.State.CursorPosition.Row;
            int startCol = context.State.CursorPosition.Column + 1;

            // Search in the current row first
            for (int col = startCol; col < document.ColumnCount; col++)
            {
                var cell = document.GetCell(startRow, col);
                if (cell != null && !string.IsNullOrEmpty(cell.Value))
                {
                    context.State.CursorPosition = new GridPosition(startRow, col);
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
                        context.State.CursorPosition = new GridPosition(row, col);
                        return true;
                    }
                }
            }

            return true;
        }
    }

    public class MoveToPrevWordAction : IVimAction
    {
        public string Name => "move_to_prev_word";

        public bool Execute(VimActionContext context)
        {
            var document = context.Document;
            int startRow = context.State.CursorPosition.Row;
            int startCol = context.State.CursorPosition.Column - 1;

            // Search in the current row first (backwards)
            for (int col = startCol; col >= 0; col--)
            {
                var cell = document.GetCell(startRow, col);
                if (cell != null && !string.IsNullOrEmpty(cell.Value))
                {
                    context.State.CursorPosition = new GridPosition(startRow, col);
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
                        context.State.CursorPosition = new GridPosition(row, col);
                        return true;
                    }
                }
            }

            return true;
        }
    }

    public class MoveToNextEmptyRowAction : IVimAction
    {
        public string Name => "move_to_next_empty_row";

        public bool Execute(VimActionContext context)
        {
            var document = context.Document;
            int currentRow = context.State.CursorPosition.Row;

            for (int i = 0; i < context.Count; i++)
            {
                int nextEmptyRow = FindNextEmptyRow(document, currentRow);
                if (nextEmptyRow >= 0)
                {
                    currentRow = nextEmptyRow;
                }
                else
                {
                    currentRow = document.RowCount - 1;
                    break;
                }
            }

            context.State.CursorPosition = new GridPosition(currentRow, context.State.CursorPosition.Column).Clamp(document);
            return true;
        }

        private static int FindNextEmptyRow(TsvDocument document, int startRow)
        {
            for (int row = startRow + 1; row < document.RowCount; row++)
            {
                if (IsEmptyRow(document, row))
                {
                    return row;
                }
            }
            return -1;
        }

        private static bool IsEmptyRow(TsvDocument document, int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= document.RowCount)
                return false;

            var row = document.Rows[rowIndex];
            return row.Cells.All(cell => string.IsNullOrWhiteSpace(cell.Value));
        }
    }

    public class MoveToPrevEmptyRowAction : IVimAction
    {
        public string Name => "move_to_prev_empty_row";

        public bool Execute(VimActionContext context)
        {
            var document = context.Document;
            int currentRow = context.State.CursorPosition.Row;

            for (int i = 0; i < context.Count; i++)
            {
                int prevEmptyRow = FindPrevEmptyRow(document, currentRow);
                if (prevEmptyRow >= 0)
                {
                    currentRow = prevEmptyRow;
                }
                else
                {
                    currentRow = 0;
                    break;
                }
            }

            context.State.CursorPosition = new GridPosition(currentRow, context.State.CursorPosition.Column).Clamp(document);
            return true;
        }

        private static int FindPrevEmptyRow(TsvDocument document, int startRow)
        {
            for (int row = startRow - 1; row >= 0; row--)
            {
                if (IsEmptyRow(document, row))
                {
                    return row;
                }
            }
            return -1;
        }

        private static bool IsEmptyRow(TsvDocument document, int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= document.RowCount)
                return false;

            var row = document.Rows[rowIndex];
            return row.Cells.All(cell => string.IsNullOrWhiteSpace(cell.Value));
        }
    }
}
