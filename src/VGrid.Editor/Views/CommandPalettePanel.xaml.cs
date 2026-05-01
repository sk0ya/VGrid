using System.Windows;
using System.Windows.Input;
using VGrid.ViewModels;

namespace VGrid.Views;

public partial class CommandPalettePanel : System.Windows.Controls.UserControl
{
    public CommandPalettePanel()
    {
        InitializeComponent();

        // Handle panel visibility changes for focus management
        IsVisibleChanged += OnIsVisibleChanged;

        // Handle keyboard navigation
        FilterTextBox.PreviewKeyDown += FilterTextBox_PreviewKeyDown;

        // Monitor DataContext changes to access ViewModel
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Subscribe to property changes in ViewModel
        if (e.OldValue is CommandPaletteViewModel oldVm)
        {
            oldVm.PropertyChanged -= ViewModel_PropertyChanged;
        }

        if (e.NewValue is CommandPaletteViewModel newVm)
        {
            newVm.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommandPaletteViewModel.IsVisible))
        {
            if (DataContext is CommandPaletteViewModel vm && vm.IsVisible)
            {
                // Focus filter textbox when panel becomes visible
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    FilterTextBox.Focus();
                    Keyboard.Focus(FilterTextBox);
                    FilterTextBox.SelectAll();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }
        else if (e.PropertyName == nameof(CommandPaletteViewModel.SelectedIndex))
        {
            // Scroll selected item into view
            ScrollSelectedItemIntoView();
        }
    }

    private void ScrollSelectedItemIntoView()
    {
        if (DataContext is CommandPaletteViewModel vm && vm.SelectedIndex >= 0 && vm.SelectedIndex < vm.FilteredItems.Count)
        {
            var item = vm.FilteredItems[vm.SelectedIndex];
            ActionListBox.ScrollIntoView(item);
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is bool isVisible && isVisible)
        {
            // Focus filter textbox when panel becomes visible
            Dispatcher.BeginInvoke(new Action(() =>
            {
                FilterTextBox.Focus();
                Keyboard.Focus(FilterTextBox);
                FilterTextBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void FilterTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not CommandPaletteViewModel viewModel)
            return;

        switch (e.Key)
        {
            case Key.Enter:
                // Execute selected action
                viewModel.ExecuteSelected();
                e.Handled = true;
                break;

            case Key.Escape:
                // Close panel
                viewModel.Close();
                e.Handled = true;
                break;

            case Key.Up:
                // Move selection up
                viewModel.MoveUp();
                e.Handled = true;
                break;

            case Key.Down:
                // Move selection down
                viewModel.MoveDown();
                e.Handled = true;
                break;

            case Key.J:
                // j key for down (vim style) only when Ctrl is held
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    viewModel.MoveDown();
                    e.Handled = true;
                }
                break;

            case Key.K:
                // k key for up (vim style) only when Ctrl is held
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    viewModel.MoveUp();
                    e.Handled = true;
                }
                break;

            case Key.P:
                // Ctrl+P cycles modes when palette is already open
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    viewModel.CycleMode();
                    e.Handled = true;
                }
                break;

            case Key.Tab:
                // Tab also cycles modes
                if (Keyboard.Modifiers == ModifierKeys.None)
                {
                    viewModel.CycleMode();
                    e.Handled = true;
                }
                else if (Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    // Shift+Tab cycles in reverse
                    // Cycle backwards: All -> Files -> Commands -> All
                    viewModel.CurrentMode = viewModel.CurrentMode switch
                    {
                        CommandPaletteMode.All => CommandPaletteMode.Files,
                        CommandPaletteMode.Commands => CommandPaletteMode.All,
                        CommandPaletteMode.Files => CommandPaletteMode.Commands,
                        _ => CommandPaletteMode.All
                    };
                    e.Handled = true;
                }
                break;
        }
    }

    private void ModeIndicator_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is CommandPaletteViewModel viewModel)
        {
            viewModel.CycleMode();
            // Keep focus on the filter textbox
            FilterTextBox.Focus();
            e.Handled = true;
        }
    }
}
