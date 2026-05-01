using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Input;
using VGrid.Models;

namespace VGrid.VimEngine;

/// <summary>
/// Implements Vim Command mode behavior for search and commands
/// </summary>
public class CommandMode : IVimMode
{
    private readonly StringBuilder _inputBuffer = new();

    public string GetModeName() => "COMMAND";

    public void OnEnter(VimState state)
    {
        _inputBuffer.Clear();
        // Set initial pattern to show the trigger character
        state.SearchPattern = state.CurrentCommandType == CommandType.Search ? "/" : ":";
    }

    public void OnExit(VimState state)
    {
        _inputBuffer.Clear();
        state.SearchPattern = string.Empty;
        state.CurrentCommandType = CommandType.Search; // Reset to default
    }

    public bool HandleKey(VimState state, Key key, ModifierKeys modifiers, TsvDocument document)
    {
        // Escape - cancel command, return to Normal
        if (key == Key.Escape)
        {
            state.ClearSearch();
            state.SwitchMode(VimMode.Normal);
            return true;
        }

        // Enter - finalize search/command, return to Normal
        if (key == Key.Enter)
        {
            if (state.CurrentCommandType == CommandType.Search)
            {
                // Search already done incrementally, just notify and switch mode
                state.OnVimSearchActivated();
            }
            else
            {
                ExecuteExCommand(state);
            }
            state.SwitchMode(VimMode.Normal);
            return true;
        }

        // Backspace - delete last character
        if (key == Key.Back)
        {
            if (_inputBuffer.Length > 0)
            {
                _inputBuffer.Length--;
                string prefix = state.CurrentCommandType == CommandType.Search ? "/" : ":";
                state.SearchPattern = prefix + _inputBuffer.ToString();

                // Incremental search on backspace
                if (state.CurrentCommandType == CommandType.Search)
                {
                    ExecuteIncrementalSearch(state, document);
                }
            }
            return true;
        }

        // Handle text input (convert Key to character)
        string? charInput = KeyToChar(key, modifiers);
        if (charInput != null)
        {
            _inputBuffer.Append(charInput);
            string prefix = state.CurrentCommandType == CommandType.Search ? "/" : ":";
            state.SearchPattern = prefix + _inputBuffer.ToString();

            // Incremental search on each character input
            if (state.CurrentCommandType == CommandType.Search)
            {
                ExecuteIncrementalSearch(state, document);
            }
            return true;
        }

        return false;
    }

    /// <summary>
    /// Executes incremental search (highlights matches without moving cursor)
    /// </summary>
    private void ExecuteIncrementalSearch(VimState state, TsvDocument document)
    {
        string pattern = _inputBuffer.ToString();
        if (string.IsNullOrEmpty(pattern))
        {
            state.ClearSearch();
            return;
        }

        // Try regex search first
        List<GridPosition> results;
        try
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            results = document.FindMatches(pattern, isRegex: true);
        }
        catch (ArgumentException)
        {
            // Invalid regex - fall back to literal search
            results = document.FindMatches(pattern, isRegex: false);
        }

        state.SetSearchResults(results);

        // Move cursor to first match if found
        if (results.Count > 0)
        {
            state.CursorPosition = results[0];
        }
    }

    /// <summary>
    /// Executes an ex-command with the current input buffer
    /// </summary>
    private void ExecuteExCommand(VimState state)
    {
        string commandText = _inputBuffer.ToString();

        // Empty command - just return to normal mode
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return;
        }

        // Parse the command
        var command = ExCommandParser.Parse(commandText);

        if (!command.IsValid)
        {
            // Invalid command - show error message
            state.ErrorMessage = command.ErrorMessage;
            return;
        }

        // Execute the command by raising appropriate event
        switch (command.Type)
        {
            case ExCommandType.Write:
                state.OnSaveRequested();
                break;
            case ExCommandType.Quit:
                state.OnQuitRequested(command.Force);
                break;
            case ExCommandType.WriteQuit:
                state.OnSaveRequested();
                state.OnQuitRequested(command.Force);
                break;
        }
    }

    /// <summary>
    /// Converts a WPF Key and ModifierKeys to a character string
    /// </summary>
    private string? KeyToChar(Key key, ModifierKeys modifiers)
    {
        bool isShift = modifiers.HasFlag(ModifierKeys.Shift);

        // Handle letters
        if (key >= Key.A && key <= Key.Z)
        {
            char baseChar = (char)('a' + (key - Key.A));
            return isShift ? baseChar.ToString().ToUpper() : baseChar.ToString();
        }

        // Handle digits
        if (key >= Key.D0 && key <= Key.D9)
        {
            if (isShift)
            {
                // Shift + number = special characters
                return key switch
                {
                    Key.D1 => "!",
                    Key.D2 => "@",
                    Key.D3 => "#",
                    Key.D4 => "$",
                    Key.D5 => "%",
                    Key.D6 => "^",
                    Key.D7 => "&",
                    Key.D8 => "*",
                    Key.D9 => "(",
                    Key.D0 => ")",
                    _ => null
                };
            }
            return ((char)('0' + (key - Key.D0))).ToString();
        }

        // Handle numpad
        if (key >= Key.NumPad0 && key <= Key.NumPad9)
        {
            return ((char)('0' + (key - Key.NumPad0))).ToString();
        }

        // Handle special keys (for regex patterns and general input)
        return key switch
        {
            Key.Space => " ",
            Key.OemPeriod => isShift ? ">" : ".",
            Key.OemComma => isShift ? "<" : ",",
            Key.OemQuestion => isShift ? "?" : "/",
            Key.OemPlus => isShift ? "+" : "=",
            Key.OemMinus => isShift ? "_" : "-",
            Key.OemOpenBrackets => isShift ? "{" : "[",
            Key.OemCloseBrackets => isShift ? "}" : "]",
            Key.OemPipe => isShift ? "|" : "\\",
            Key.Oem1 => isShift ? ":" : ";",
            Key.Oem7 => isShift ? "\"" : "'",
            Key.Oem3 => isShift ? "~" : "`",
            Key.Multiply => "*",
            Key.Add => "+",
            Key.Subtract => "-",
            Key.Divide => "/",
            Key.Decimal => ".",
            _ => null
        };
    }
}
