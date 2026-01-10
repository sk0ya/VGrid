using System.Windows.Input;
using VGrid.Models;

namespace VGrid.VimEngine;

/// <summary>
/// Implements Vim Insert mode behavior
/// </summary>
public class InsertMode : IVimMode
{
    public string GetModeName() => "INSERT";

    public void OnEnter(VimState state)
    {
        // Mark the current cell as editing
        // This will be handled by the UI layer
    }

    public void OnExit(VimState state)
    {
        // Cell editing complete
    }

    public bool HandleKey(VimState state, Key key, ModifierKeys modifiers, TsvDocument document)
    {
        // Handle escape to return to normal mode
        if (key == Key.Escape)
        {
            state.SwitchMode(VimMode.Normal);
            return true;
        }

        // Handle arrow keys for navigation within insert mode
        switch (key)
        {
            case Key.Left:
                state.CursorPosition = state.CursorPosition.MoveLeft(1).Clamp(document);
                return true;
            case Key.Right:
                state.CursorPosition = state.CursorPosition.MoveRight(1).Clamp(document);
                return true;
            case Key.Up:
                state.CursorPosition = state.CursorPosition.MoveUp(1).Clamp(document);
                return true;
            case Key.Down:
                state.CursorPosition = state.CursorPosition.MoveDown(1).Clamp(document);
                return true;
        }

        // All other keys (text input, backspace, delete, etc.) will be handled by the UI layer
        // The grid control will manage actual text input
        return false;
    }
}
