using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VGrid.ViewModels;

namespace VGrid.Views;

public partial class GitHistoryOverlayPanel : System.Windows.Controls.UserControl
{
    public GitHistoryOverlayPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is GitHistoryViewModel oldVm)
        {
            oldVm.DiffRequested -= ViewModel_DiffRequested;
            CommitListBox.SelectionChanged -= CommitListBox_SelectionChanged;
        }

        if (e.NewValue is GitHistoryViewModel newVm)
        {
            newVm.DiffRequested += ViewModel_DiffRequested;
            CommitListBox.SelectionChanged += CommitListBox_SelectionChanged;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseOverlay();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseOverlay();
            e.Handled = true;
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    private void CloseOverlay()
    {
        var window = Window.GetWindow(this);
        if (window?.DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.CloseGitHistoryOverlay();
        }
    }

    private void CommitListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not GitHistoryViewModel viewModel) return;

        viewModel.SelectedCommits.Clear();
        foreach (var item in CommitListBox.SelectedItems)
        {
            if (item is Models.GitCommit commit)
            {
                viewModel.SelectedCommits.Add(commit);
            }
        }
    }

    private void CommitListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not GitHistoryViewModel viewModel) return;

        if (CommitListBox.SelectedItem is Models.GitCommit commit)
        {
            viewModel.SelectedCommits.Clear();
            viewModel.SelectedCommits.Add(commit);

            if (viewModel.ViewDiffVsParentCommand.CanExecute(null))
            {
                viewModel.ViewDiffVsParentCommand.Execute(null);
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
            Owner = Window.GetWindow(this)
        };

        diffWindow.Show();
    }
}
