using System.Windows;
using System.Windows.Controls;
using VGrid.ViewModels;

namespace VGrid.Views;

/// <summary>
/// Git history window showing commit list
/// </summary>
public partial class GitHistoryWindow : Window
{
    private readonly GitHistoryViewModel _viewModel;

    public GitHistoryWindow(GitHistoryViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.CloseRequested += (s, e) => Close();
        _viewModel.DiffRequested += ViewModel_DiffRequested;

        // Sync ListBox selection with ViewModel
        CommitListBox.SelectionChanged += CommitListBox_SelectionChanged;
    }

    // Window Control Button Handlers
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }
        else
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void CommitListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _viewModel.SelectedCommits.Clear();
        foreach (var item in CommitListBox.SelectedItems)
        {
            if (item is Models.GitCommit commit)
            {
                _viewModel.SelectedCommits.Add(commit);
            }
        }
    }

    private void CommitListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Check if an item is double-clicked
        if (CommitListBox.SelectedItem is Models.GitCommit commit)
        {
            // Clear selection and select only the double-clicked commit
            _viewModel.SelectedCommits.Clear();
            _viewModel.SelectedCommits.Add(commit);

            // Execute ViewDiffVsParentCommand
            if (_viewModel.ViewDiffVsParentCommand.CanExecute(null))
            {
                _viewModel.ViewDiffVsParentCommand.Execute(null);
            }
        }
    }

    private void ViewModel_DiffRequested(object? sender, DiffRequestEventArgs e)
    {
        var diffViewModel = new DiffViewerViewModel(
            e.RepoRoot,
            e.Commit1Hash,
            e.Commit2Hash,
            new Services.GitService());

        var diffWindow = new DiffViewerWindow(diffViewModel)
        {
            Owner = this
        };

        diffWindow.Show();
    }
}
