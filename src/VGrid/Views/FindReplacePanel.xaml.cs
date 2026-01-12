using System.Windows;
using VGrid.ViewModels;

namespace VGrid.Views;

public partial class FindReplacePanel : System.Windows.Controls.UserControl
{
    public FindReplacePanel()
    {
        InitializeComponent();

        // Handle panel visibility changes for focus management
        IsVisibleChanged += OnIsVisibleChanged;

        // Handle Enter key in search box to find next
        SearchTextBox.PreviewKeyDown += SearchTextBox_PreviewKeyDown;

        // Monitor DataContext changes to access ViewModel
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Subscribe to IsVisible property changes in ViewModel
        if (e.OldValue is FindReplaceViewModel oldVm)
        {
            oldVm.PropertyChanged -= ViewModel_PropertyChanged;
        }

        if (e.NewValue is FindReplaceViewModel newVm)
        {
            newVm.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FindReplaceViewModel.IsVisible))
        {
            if (DataContext is FindReplaceViewModel vm && vm.IsVisible)
            {
                // Focus search textbox when panel becomes visible
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    SearchTextBox.Focus();
                    System.Windows.Input.Keyboard.Focus(SearchTextBox);
                    SearchTextBox.SelectAll();
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is bool isVisible && isVisible)
        {
            // Focus search textbox when panel becomes visible
            Dispatcher.BeginInvoke(new Action(() =>
            {
                SearchTextBox.Focus();
                System.Windows.Input.Keyboard.Focus(SearchTextBox);
                SearchTextBox.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void SearchTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && DataContext is FindReplaceViewModel viewModel)
        {
            // Enter key finds next match
            if (viewModel.FindNextCommand.CanExecute(null))
            {
                viewModel.FindNextCommand.Execute(null);
            }
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Escape && DataContext is FindReplaceViewModel vm)
        {
            // Escape key closes panel
            if (vm.CloseCommand.CanExecute(null))
            {
                vm.CloseCommand.Execute(null);
            }
            e.Handled = true;
        }
    }
}
