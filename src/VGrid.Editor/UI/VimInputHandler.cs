using System.Windows.Controls;
using System.Windows.Input;
using VGrid.Commands;
using VGrid.Editor;
using VGrid.Models;
using VGrid.VimEngine;

namespace VGrid.UI;

/// <summary>
/// Handles keyboard input and forwards to VimState.
/// Ctrl+S triggers VimState.OnSaveRequested(); Ctrl+G raises CustomKeyAction.
/// </summary>
public class VimInputHandler
{
    private readonly IEditorContext _context;

    /// <summary>Raised for host-specific key actions not handled by the library (e.g. Ctrl+G).</summary>
    public event EventHandler<CustomKeyActionEventArgs>? CustomKeyAction;

    public VimInputHandler(IEditorContext context)
    {
        _context = context;
    }

    public void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_context.SelectedTab == null)
            return;

        // Don't handle keys if a TextBox has focus (for file rename or FindReplace panel)
        if (Keyboard.FocusedElement is TextBox)
        {
            // Allow Ctrl+F even when TextBox has focus (to open/focus FindReplace panel)
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var findReplaceVM = _context.SelectedTab.FindReplaceViewModel;
                if (findReplaceVM != null)
                {
                    _context.SelectedTab.VimState.ClearSearch();
                    findReplaceVM.Open();
                    e.Handled = true;
                    return;
                }
            }
            return;
        }

        // Ctrl+F: Find/Replace panel
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            var findReplaceVM = _context.SelectedTab.FindReplaceViewModel;
            if (findReplaceVM != null)
            {
                _context.SelectedTab.VimState.ClearSearch();
                findReplaceVM.Open();
                e.Handled = true;
                return;
            }
        }

        // Ctrl+G: raise as custom action for the host to handle (e.g. git history)
        if (e.Key == Key.G && Keyboard.Modifiers == ModifierKeys.Control)
        {
            CustomKeyAction?.Invoke(this, new CustomKeyActionEventArgs("GitHistory"));
            e.Handled = true;
            return;
        }

        // Ctrl+S: save via VimState event
        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _context.SelectedTab.VimState.OnSaveRequested();
            e.Handled = true;
            return;
        }

        // Ctrl+P: Command Palette
        if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
        {
            var commandPaletteVM = _context.SelectedTab.CommandPaletteViewModel;
            if (commandPaletteVM != null)
            {
                if (commandPaletteVM.IsVisible)
                    commandPaletteVM.CycleMode();
                else
                    commandPaletteVM.Open();
                e.Handled = true;
                return;
            }
        }

        // Ctrl+Shift+E: host-specific (folder tree) - pass through
        if (e.Key == Key.E && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            return;

        // Ctrl+Shift+; (OemPlus): Insert Line Above
        if (e.Key == Key.OemPlus && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            var tab = _context.SelectedTab;
            if (tab != null)
            {
                int insertRow = tab.VimState.CursorPosition.Row;
                int currentColumn = tab.VimState.CursorPosition.Column;
                var command = new InsertRowCommand(tab.GridViewModel.Document, insertRow);
                tab.VimState.CommandHistory?.Execute(command);
                tab.VimState.CursorPosition = new GridPosition(insertRow, currentColumn);
                tab.VimState.RefreshCursorPositionBinding();
                e.Handled = true;
                return;
            }
        }

        // Ctrl++: Insert current date
        if (e.Key == Key.OemPlus && Keyboard.Modifiers == ModifierKeys.Control)
        {
            var tab = _context.SelectedTab;
            if (tab != null)
            {
                string currentDate = DateTime.Now.ToString("yyyy/MM/dd");
                if (tab.VimState.CurrentMode == VimMode.Normal)
                {
                    var command = new EditCellCommand(tab.GridViewModel.Document, tab.VimState.CursorPosition, currentDate);
                    tab.VimState.CommandHistory?.Execute(command);
                    e.Handled = true;
                    return;
                }
                else if (tab.VimState.CurrentMode == VimMode.Visual && tab.VimState.CurrentSelection != null)
                {
                    var command = new EditSelectionCommand(tab.GridViewModel.Document, tab.VimState.CurrentSelection, currentDate);
                    tab.VimState.CommandHistory?.Execute(command);
                    tab.VimState.SwitchMode(VimMode.Normal);
                    e.Handled = true;
                    return;
                }
            }
        }

        // Non-Vim mode: Excel-like shortcuts
        if (!_context.IsVimModeEnabled)
        {
            if (Keyboard.FocusedElement is TextBox)
                return;

            var tab = _context.SelectedTab;
            var document = tab.GridViewModel.Document;
            var pos = tab.VimState.CursorPosition;

            if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
            {
                tab.VimState.CurrentSelection = new SelectionRange(
                    VisualType.Block,
                    new GridPosition(0, 0),
                    new GridPosition(document.RowCount - 1, document.ColumnCount - 1));
                e.Handled = true;
                return;
            }

            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                string textToCopy;
                if (tab.VimState.CurrentSelection != null)
                {
                    var selection = tab.VimState.CurrentSelection;
                    var lines = new List<string>();
                    for (int row = selection.StartRow; row <= selection.EndRow; row++)
                    {
                        var cells = new List<string>();
                        for (int col = selection.StartColumn; col <= selection.EndColumn; col++)
                            cells.Add(document.GetCell(row, col)?.Value ?? string.Empty);
                        lines.Add(string.Join("\t", cells));
                    }
                    textToCopy = string.Join("\r\n", lines);
                }
                else
                {
                    textToCopy = document.GetCell(pos.Row, pos.Column)?.Value ?? string.Empty;
                }
                System.Windows.Clipboard.SetText(textToCopy);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    var text = System.Windows.Clipboard.GetText();
                    var clipboardLines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    if (clipboardLines.Length > 1 && string.IsNullOrEmpty(clipboardLines[^1]))
                        clipboardLines = clipboardLines[..^1];

                    var clipboardData = clipboardLines.Select(line => line.Split('\t')).ToArray();
                    int rowCount = clipboardData.Length;
                    int colCount = clipboardData.Max(r => r.Length);

                    var yank = new YankedContent
                    {
                        Rows = rowCount,
                        Columns = colCount,
                        SourceType = VisualType.Character,
                        Values = new string[rowCount, colCount]
                    };
                    for (int r = 0; r < rowCount; r++)
                        for (int c = 0; c < colCount; c++)
                            yank.Values[r, c] = c < clipboardData[r].Length ? clipboardData[r][c] : string.Empty;

                    if (tab.VimState.CurrentSelection != null)
                    {
                        var pasteCommand = new PasteOverSelectionCommand(document, tab.VimState.CurrentSelection, yank);
                        tab.VimState.CommandHistory?.Execute(pasteCommand);
                        tab.VimState.CurrentSelection = null;
                    }
                    else
                    {
                        var selection = new SelectionRange(
                            VisualType.Character,
                            pos,
                            new GridPosition(pos.Row + rowCount - 1, pos.Column + colCount - 1));
                        var pasteCommand = new PasteOverSelectionCommand(document, selection, yank);
                        tab.VimState.CommandHistory?.Execute(pasteCommand);
                    }
                }
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete)
            {
                if (tab.VimState.CurrentSelection != null)
                {
                    var selection = tab.VimState.CurrentSelection;
                    for (int row = selection.StartRow; row <= selection.EndRow; row++)
                        for (int col = selection.StartColumn; col <= selection.EndColumn; col++)
                            tab.VimState.CommandHistory?.Execute(new EditCellCommand(document, new GridPosition(row, col), string.Empty));
                    tab.VimState.CurrentSelection = null;
                }
                else
                {
                    tab.VimState.CommandHistory?.Execute(new EditCellCommand(document, pos, string.Empty));
                }
                e.Handled = true;
                return;
            }
            return;
        }

        // Vim mode key dispatch
        var currentTab = _context.SelectedTab;
        var currentMode = currentTab.VimState.CurrentMode;

        Key actualKey = e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key;

        if (currentMode == VimMode.Normal && actualKey == Key.Oem1)
        {
            currentTab.VimState.CurrentCommandType = CommandType.ExCommand;
            currentTab.VimState.SwitchMode(VimMode.Command);
            e.Handled = true;
            return;
        }

        if (currentMode == VimMode.Normal &&
            actualKey == Key.OemQuestion &&
            !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            currentTab.VimState.CurrentCommandType = CommandType.Search;
            currentTab.VimState.SwitchMode(VimMode.Command);
            e.Handled = true;
            return;
        }

        if (currentTab.VimState.HandleKey(actualKey, Keyboard.Modifiers, currentTab.GridViewModel.Document))
            e.Handled = true;
    }
}

public class CustomKeyActionEventArgs : EventArgs
{
    public string ActionName { get; }
    public CustomKeyActionEventArgs(string actionName) => ActionName = actionName;
}
