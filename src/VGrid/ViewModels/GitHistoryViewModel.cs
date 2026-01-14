using System.Collections.ObjectModel;
using VGrid.Helpers;
using VGrid.Models;
using VGrid.Services;

namespace VGrid.ViewModels;

/// <summary>
/// ViewModel for Git history window (commit list only)
/// </summary>
public class GitHistoryViewModel : ViewModelBase
{
    private readonly IGitService _gitService;
    private readonly string _folderPath;
    private readonly string _repoRoot;

    public GitHistoryViewModel(string folderPath, string repoRoot, IGitService gitService)
    {
        _folderPath = folderPath;
        _repoRoot = repoRoot;
        _gitService = gitService;

        Commits = new ObservableCollection<GitCommit>();
        SelectedCommits = new ObservableCollection<GitCommit>();

        ViewDiffVsWorkingCommand = new RelayCommand(ViewDiffVsWorking, CanViewDiffVsWorking);
        ViewDiffVsParentCommand = new RelayCommand(ViewDiffVsParent, CanViewDiffVsParent);
        ViewDiffBetweenCommitsCommand = new RelayCommand(ViewDiffBetweenCommits, CanViewDiffBetweenCommits);
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));

        // Load commits on initialization
        _ = LoadCommitsAsync();
    }

    public ObservableCollection<GitCommit> Commits { get; }
    public ObservableCollection<GitCommit> SelectedCommits { get; }

    public RelayCommand ViewDiffVsWorkingCommand { get; }
    public RelayCommand ViewDiffVsParentCommand { get; }
    public RelayCommand ViewDiffBetweenCommitsCommand { get; }
    public RelayCommand CloseCommand { get; }

    public event EventHandler? CloseRequested;
    public event EventHandler<DiffRequestEventArgs>? DiffRequested;

    private async Task LoadCommitsAsync()
    {
        var commits = await _gitService.GetFolderHistoryAsync(_folderPath);
        Commits.Clear();
        foreach (var commit in commits)
        {
            Commits.Add(commit);
        }

        if (Commits.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine($"GitHistoryViewModel: No commits found for {_folderPath}");
            System.Windows.MessageBox.Show(
                $"No Git history found for this folder.\n\nFolder: {_folderPath}\n\nThis may occur if:\n- No files in this folder have been committed yet\n- The folder path is incorrect\n- Git is not properly configured",
                "No History",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }

    private bool CanViewDiffVsWorking()
    {
        return SelectedCommits.Count == 1;
    }

    private void ViewDiffVsWorking()
    {
        if (SelectedCommits.Count != 1)
            return;

        var commit = SelectedCommits[0];
        DiffRequested?.Invoke(this, new DiffRequestEventArgs(_repoRoot, commit.Hash, null));
    }

    private bool CanViewDiffVsParent()
    {
        return SelectedCommits.Count == 1;
    }

    private async void ViewDiffVsParent()
    {
        if (SelectedCommits.Count != 1)
            return;

        var commit = SelectedCommits[0];
        var parentHash = await _gitService.GetParentCommitAsync(commit.Hash, _repoRoot);

        if (parentHash == null)
        {
            System.Windows.MessageBox.Show(
                "This is the initial commit with no parent.",
                "No Parent",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        DiffRequested?.Invoke(this, new DiffRequestEventArgs(_repoRoot, parentHash, commit.Hash));
    }

    private bool CanViewDiffBetweenCommits()
    {
        return SelectedCommits.Count == 2;
    }

    private void ViewDiffBetweenCommits()
    {
        if (SelectedCommits.Count != 2)
            return;

        // Compare older (first selected) vs newer (second selected)
        var commit1 = SelectedCommits[0];
        var commit2 = SelectedCommits[1];

        DiffRequested?.Invoke(this, new DiffRequestEventArgs(_repoRoot, commit1.Hash, commit2.Hash));
    }
}

/// <summary>
/// Event args for diff request
/// </summary>
public class DiffRequestEventArgs : EventArgs
{
    public string RepoRoot { get; }
    public string? Commit1Hash { get; }
    public string? Commit2Hash { get; }

    public DiffRequestEventArgs(string repoRoot, string? commit1Hash, string? commit2Hash)
    {
        RepoRoot = repoRoot;
        Commit1Hash = commit1Hash;
        Commit2Hash = commit2Hash;
    }
}
