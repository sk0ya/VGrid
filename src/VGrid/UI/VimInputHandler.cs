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

        // Handle Ctrl+Shift+E for Select in Folder Tree (works regardless of Vim mode)
        if (e.Key == Key.E && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            // This will be handled by MainWindow
            return;
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

        // If Vim mode is disabled, let DataGrid handle keys normally
        if (!_viewModel.IsVimModeEnabled)
            return;

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
