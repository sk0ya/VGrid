using System;
using System.Windows.Controls;
using System.Windows.Input;
using VGrid.Commands;
using VGrid.ViewModels;
using VGrid.VimEngine;

namespace VGrid.UI;

/// <summary>
/// Handles keyboard input and forwards to VimState
/// </summary>
public class VimInputHandler
{
    private readonly MainViewModel _viewModel;

    public VimInputHandler(MainViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_viewModel?.SelectedTab == null)
            return;

        // Don't handle keys if a TextBox has focus (for file rename or FindReplace panel)
        if (Keyboard.FocusedElement is TextBox textBox)
        {
            // Allow Ctrl+F even when TextBox has focus (to open/focus FindReplace panel)
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var findReplaceVM = _viewModel.SelectedTab.FindReplaceViewModel;
                if (findReplaceVM != null)
                {
                    _viewModel.SelectedTab.VimState.ClearSearch();
                    findReplaceVM.Open();
                    e.Handled = true;
                    return;
                }
            }

            return;
        }

        // Handle Ctrl+F for Find/Replace panel (works regardless of Vim mode)
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            var findReplaceVM = _viewModel.SelectedTab.FindReplaceViewModel;
            if (findReplaceVM != null)
            {
                _viewModel.SelectedTab.VimState.ClearSearch();
                findReplaceVM.Open();
                e.Handled = true;
                return;
            }
        }

        // Handle Ctrl+G for Git History (works regardless of Vim mode)
        if (e.Key == Key.G && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (_viewModel.ViewGitHistoryCommand.CanExecute(null))
            {
                _viewModel.ViewGitHistoryCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }

        // Handle Ctrl+S for Save File (works regardless of Vim mode)
        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (_viewModel.SaveFileCommand.CanExecute(null))
            {
                _viewModel.SaveFileCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }

        // Handle Ctrl+P for Command Palette (works regardless of Vim mode)
        if (e.Key == Key.P && Keyboard.Modifiers == ModifierKeys.Control)
        {
            var commandPaletteVM = _viewModel.SelectedTab.CommandPaletteViewModel;
            if (commandPaletteVM != null)
            {
                if (commandPaletteVM.IsVisible)
                {
                    // If already open, cycle modes
                    commandPaletteVM.CycleMode();
                }
                else
                {
                    // Open the palette
                    commandPaletteVM.Open();
                }
                e.Handled = true;
                return;
            }
        }

        // Handle Ctrl+Shift+E for Select in Folder Tree (works regardless of Vim mode)
        if (e.Key == Key.E && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            // This will be handled by MainWindow
            return;
        }

        // Handle Ctrl+Shift+; for Insert Line Above (works regardless of Vim mode)
        if (e.Key == Key.OemPlus && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            var tab = _viewModel.SelectedTab;
            if (tab != null)
            {
                var document = tab.GridViewModel.Document;
                var vimState = tab.VimState;

                // Insert a new row above the current row
                int insertRow = vimState.CursorPosition.Row;
                int currentColumn = vimState.CursorPosition.Column;
                var command = new InsertRowCommand(document, insertRow);

                // Execute through command history
                vimState.CommandHistory?.Execute(command);

                // Move cursor to the new row (force refresh since row number may be same)
                vimState.CursorPosition = new Models.GridPosition(insertRow, currentColumn);
                vimState.RefreshCursorPositionBinding();

                e.Handled = true;
                return;
            }
        }

        // Handle Ctrl++ to insert current date (Excel-like shortcut on Japanese keyboard, works in Normal and Visual modes)
        if (e.Key == Key.OemPlus && Keyboard.Modifiers == ModifierKeys.Control)
        {
            var tab = _viewModel.SelectedTab;
            if (tab != null)
            {
                string currentDate = DateTime.Now.ToString("yyyy/MM/dd");

                if (tab.VimState.CurrentMode == VimMode.Normal)
                {
                    var command = new EditCellCommand(
                        tab.GridViewModel.Document,
                        tab.VimState.CursorPosition,
                        currentDate);
                    tab.VimState.CommandHistory?.Execute(command);
                    e.Handled = true;
                    return;
                }
                else if (tab.VimState.CurrentMode == VimMode.Visual)
                {
                    if (tab.VimState.CurrentSelection != null)
                    {
                        var command = new EditSelectionCommand(
                            tab.GridViewModel.Document,
                            tab.VimState.CurrentSelection,
                            currentDate);
                        tab.VimState.CommandHistory?.Execute(command);

                        tab.VimState.SwitchMode(VimMode.Normal);
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        // If Vim mode is disabled, handle standard shortcuts (Excel-like behavior)
        if (!_viewModel.IsVimModeEnabled)
        {
            // If editing a cell (TextBox has focus), let TextBox handle standard shortcuts
            if (Keyboard.FocusedElement is TextBox)
                return;

            var tab = _viewModel.SelectedTab;
            var document = tab.GridViewModel.Document;
            var pos = tab.VimState.CursorPosition;

            // Ctrl+A: Select all cells
            if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Select all cells by setting visual selection
                tab.VimState.CurrentSelection = new VimEngine.SelectionRange(
                    VimEngine.VisualType.Block,
                    new Models.GridPosition(0, 0),
                    new Models.GridPosition(document.RowCount - 1, document.ColumnCount - 1));
                e.Handled = true;
                return;
            }

            // Ctrl+C: Copy selected cell(s)
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                string textToCopy;
                if (tab.VimState.CurrentSelection != null)
                {
                    // Copy selection (use StartRow/EndRow/StartColumn/EndColumn for proper ordering)
                    var selection = tab.VimState.CurrentSelection;
                    var lines = new System.Collections.Generic.List<string>();
                    for (int row = selection.StartRow; row <= selection.EndRow; row++)
                    {
                        var cells = new System.Collections.Generic.List<string>();
                        for (int col = selection.StartColumn; col <= selection.EndColumn; col++)
                        {
                            var cell = document.GetCell(row, col);
                            cells.Add(cell?.Value ?? string.Empty);
                        }
                        lines.Add(string.Join("\t", cells));
                    }
                    textToCopy = string.Join("\r\n", lines);
                }
                else
                {
                    // Copy current cell
                    var cell = document.GetCell(pos.Row, pos.Column);
                    textToCopy = cell?.Value ?? string.Empty;
                }
                System.Windows.Clipboard.SetText(textToCopy);
                e.Handled = true;
                return;
            }

            // Ctrl+V: Paste
            if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    var text = System.Windows.Clipboard.GetText();
                    var clipboardLines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    // Remove trailing empty line if exists (from copy ending with newline)
                    if (clipboardLines.Length > 1 && string.IsNullOrEmpty(clipboardLines[clipboardLines.Length - 1]))
                    {
                        clipboardLines = clipboardLines.Take(clipboardLines.Length - 1).ToArray();
                    }

                    // Parse clipboard into YankedContent
                    var clipboardData = clipboardLines.Select(line => line.Split('\t')).ToArray();
                    int rowCount = clipboardData.Length;
                    int colCount = clipboardData.Max(row => row.Length);

                    var yank = new VimEngine.YankedContent
                    {
                        Rows = rowCount,
                        Columns = colCount,
                        SourceType = VimEngine.VisualType.Character,
                        Values = new string[rowCount, colCount]
                    };
                    for (int r = 0; r < rowCount; r++)
                    {
                        for (int c = 0; c < colCount; c++)
                        {
                            yank.Values[r, c] = c < clipboardData[r].Length ? clipboardData[r][c] : string.Empty;
                        }
                    }

                    if (tab.VimState.CurrentSelection != null)
                    {
                        // Use PasteOverSelectionCommand (same as Vim visual mode paste)
                        var pasteCommand = new Commands.PasteOverSelectionCommand(document, tab.VimState.CurrentSelection, yank);
                        tab.VimState.CommandHistory?.Execute(pasteCommand);
                        tab.VimState.CurrentSelection = null;
                    }
                    else
                    {
                        // No selection: create a 1x1 selection at current position and paste
                        var selection = new VimEngine.SelectionRange(
                            VimEngine.VisualType.Character,
                            pos,
                            new Models.GridPosition(pos.Row + rowCount - 1, pos.Column + colCount - 1));
                        var pasteCommand = new Commands.PasteOverSelectionCommand(document, selection, yank);
                        tab.VimState.CommandHistory?.Execute(pasteCommand);
                    }
                }
                e.Handled = true;
                return;
            }

            // Delete: Clear selected cell(s)
            if (e.Key == Key.Delete)
            {
                if (tab.VimState.CurrentSelection != null)
                {
                    // Delete selection (use StartRow/EndRow/StartColumn/EndColumn for proper ordering)
                    var selection = tab.VimState.CurrentSelection;
                    for (int row = selection.StartRow; row <= selection.EndRow; row++)
                    {
                        for (int col = selection.StartColumn; col <= selection.EndColumn; col++)
                        {
                            var command = new Commands.EditCellCommand(
                                document,
                                new Models.GridPosition(row, col),
                                string.Empty);
                            tab.VimState.CommandHistory?.Execute(command);
                        }
                    }
                    tab.VimState.CurrentSelection = null;
                }
                else
                {
                    // Delete current cell
                    var command = new Commands.EditCellCommand(
                        document,
                        pos,
                        string.Empty);
                    tab.VimState.CommandHistory?.Execute(command);
                }
                e.Handled = true;
                return;
            }

            return;
        }

        var currentTab = _viewModel.SelectedTab;
        var currentMode = currentTab.VimState.CurrentMode;

        // Get the actual key - handle IME processed keys
        Key actualKey = e.Key;
        if (e.Key == Key.ImeProcessed)
        {
            actualKey = e.ImeProcessedKey;
        }

        // Special handling for ':' key in Normal mode to ensure it enters Command mode
        if (currentMode == VimMode.Normal && actualKey == Key.Oem1)
        {
            currentTab.VimState.CurrentCommandType = CommandType.ExCommand;
            currentTab.VimState.SwitchMode(VimMode.Command);
            e.Handled = true;
            return;
        }

        // Special handling for '/' key in Normal mode to ensure it enters Search mode
        if (currentMode == VimMode.Normal &&
            actualKey == Key.OemQuestion &&
            !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            currentTab.VimState.CurrentCommandType = CommandType.Search;
            currentTab.VimState.SwitchMode(VimMode.Command);
            e.Handled = true;
            return;
        }

        // Handle key through Vim state of the selected tab
        var handled = currentTab.VimState.HandleKey(
            actualKey,
            Keyboard.Modifiers,
            currentTab.GridViewModel.Document);

        if (handled)
        {
            e.Handled = true;
        }
    }
}
