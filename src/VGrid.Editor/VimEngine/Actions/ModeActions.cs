using VGrid.Commands;
using VGrid.Models;
using VGrid.VimEngine.KeyBinding;

namespace VGrid.VimEngine.Actions;

/// <summary>
/// Mode switching Vim actions
/// </summary>
public static class ModeActions
{
    public class SwitchToInsertAction : IVimAction
    {
        public string Name => "switch_to_insert";

        public bool Execute(VimActionContext context)
        {
            context.State.CellEditCaretPosition = CellEditCaretPosition.Start;
            context.State.PendingInsertType = ChangeType.Insert;
            context.State.InsertModeStartPosition = context.State.CursorPosition;
            context.State.SwitchMode(VimMode.Insert);
            return true;
        }
    }

    public class SwitchToInsertLineStartAction : IVimAction
    {
        public string Name => "switch_to_insert_line_start";

        public bool Execute(VimActionContext context)
        {
            // Move to the left cell and enter Insert mode (Shift+I behavior)
            var newPos = context.State.CursorPosition.MoveLeft(1).Clamp(context.Document);
            context.State.CursorPosition = newPos;
            context.State.CellEditCaretPosition = CellEditCaretPosition.End;
            context.State.SwitchMode(VimMode.Insert);
            return true;
        }
    }

    public class SwitchToAppendAction : IVimAction
    {
        public string Name => "switch_to_append";

        public bool Execute(VimActionContext context)
        {
            context.State.CellEditCaretPosition = CellEditCaretPosition.End;
            context.State.PendingInsertType = ChangeType.InsertAfter;
            context.State.InsertModeStartPosition = context.State.CursorPosition;
            context.State.SwitchMode(VimMode.Insert);
            return true;
        }
    }

    public class SwitchToAppendLineEndAction : IVimAction
    {
        public string Name => "switch_to_append_line_end";

        public bool Execute(VimActionContext context)
        {
            // Move to the right cell and enter Insert mode (Shift+A behavior)
            var newPos = context.State.CursorPosition.MoveRight(1).Clamp(context.Document);
            context.State.CursorPosition = newPos;
            context.State.CellEditCaretPosition = CellEditCaretPosition.End;
            context.State.SwitchMode(VimMode.Insert);
            return true;
        }
    }

    public class SwitchToInsertBelowAction : IVimAction
    {
        public string Name => "switch_to_insert_below";

        public bool Execute(VimActionContext context)
        {
            var state = context.State;
            var document = context.Document;

            int insertRow = state.CursorPosition.Row + 1;
            int currentColumn = state.CursorPosition.Column;
            var command = new InsertRowCommand(document, insertRow);

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
    }

    public class SwitchToInsertAboveAction : IVimAction
    {
        public string Name => "switch_to_insert_above";

        public bool Execute(VimActionContext context)
        {
            var state = context.State;
            var document = context.Document;

            int insertRow = state.CursorPosition.Row;
            int currentColumn = state.CursorPosition.Column;
            var command = new InsertRowCommand(document, insertRow);

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
    }

    public class SwitchToVisualAction : IVimAction
    {
        public string Name => "switch_to_visual";

        public bool Execute(VimActionContext context)
        {
            context.State.CurrentSelection = new SelectionRange(
                VisualType.Character,
                context.State.CursorPosition,
                context.State.CursorPosition);
            context.State.SwitchMode(VimMode.Visual);
            return true;
        }
    }

    public class SwitchToVisualLineAction : IVimAction
    {
        public string Name => "switch_to_visual_line";

        public bool Execute(VimActionContext context)
        {
            context.State.CurrentSelection = new SelectionRange(
                VisualType.Line,
                context.State.CursorPosition,
                context.State.CursorPosition);
            context.State.SwitchMode(VimMode.Visual);
            return true;
        }
    }

    public class SwitchToVisualBlockAction : IVimAction
    {
        public string Name => "switch_to_visual_block";

        public bool Execute(VimActionContext context)
        {
            context.State.CurrentSelection = new SelectionRange(
                VisualType.Block,
                context.State.CursorPosition,
                context.State.CursorPosition);
            context.State.SwitchMode(VimMode.Visual);
            return true;
        }
    }

    public class SwitchToCommandAction : IVimAction
    {
        public string Name => "switch_to_command";

        public bool Execute(VimActionContext context)
        {
            context.State.CurrentCommandType = CommandType.ExCommand;
            context.State.SwitchMode(VimMode.Command);
            return true;
        }
    }

    public class StartSearchAction : IVimAction
    {
        public string Name => "start_search";

        public bool Execute(VimActionContext context)
        {
            context.State.CurrentCommandType = CommandType.Search;
            context.State.SwitchMode(VimMode.Command);
            return true;
        }
    }

    public class SwitchToNormalAction : IVimAction
    {
        public string Name => "switch_to_normal";

        public bool Execute(VimActionContext context)
        {
            context.State.SwitchMode(VimMode.Normal);
            return true;
        }
    }
}
