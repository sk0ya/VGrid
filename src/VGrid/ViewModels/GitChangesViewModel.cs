using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using VGrid.Helpers;
using VGrid.Models;
using VGrid.Services;

namespace VGrid.ViewModels;

/// <summary>
/// ViewModel for Git changes tab - tracks uncommitted TSV files and supports commit operations
/// </summary>
public class GitChangesViewModel : ViewModelBase, IDisposable
{
    private readonly IGitService _gitService;
    private readonly StatusBarViewModel? _statusBarViewModel;
    private string? _repositoryPath;
    private string _commitMessage = string.Empty;
    private bool _isRefreshing;
    private string? _statusMessage;
    private bool _isGitAvailable = true;
    private System.Threading.Timer? _autoRefreshTimer;
    private bool _disposed;

    public GitChangesViewModel(IGitService gitService, StatusBarViewModel? statusBarViewModel = null)
    {
        _gitService = gitService ?? throw new ArgumentNullException(nameof(gitService));
        _statusBarViewModel = statusBarViewModel;

        UncommittedFiles = new ObservableCollection<UncommittedFile>();

        // Initialize commands
        CommitCommand = new RelayCommand(async () => await CommitSelectedFilesAsync(), CanCommit);
        EditFileCommand = new RelayCommand<UncommittedFile>(EditFile);
        ShowDiffCommand = new RelayCommand<UncommittedFile>(ShowDiff);
        OpenFolderCommand = new RelayCommand<UncommittedFile>(OpenFolder);
        OpenInEditorCommand = new RelayCommand<UncommittedFile>(OpenInEditor);
        RevertFileCommand = new RelayCommand<UncommittedFile>(async file => await RevertFileAsync(file));

        // Check if Git is available
        CheckGitAvailabilityAsync();
    }

    /// <summary>
    /// Collection of uncommitted files in the repository
    /// </summary>
    public ObservableCollection<UncommittedFile> UncommittedFiles { get; }

    /// <summary>
    /// Commit message text
    /// </summary>
    public string CommitMessage
    {
        get => _commitMessage;
        set
        {
            if (SetProperty(ref _commitMessage, value))
            {
                CommitCommand.RaiseCanExecuteChanged();
            }
        }
    }

    /// <summary>
    /// Whether the view is currently refreshing
    /// </summary>
    public bool IsRefreshing
    {
        get => _isRefreshing;
        set => SetProperty(ref _isRefreshing, value);
    }

    /// <summary>
    /// Status message to display (e.g., "Not a Git repository")
    /// </summary>
    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Whether Git is available on the system
    /// </summary>
    public bool IsGitAvailable
    {
        get => _isGitAvailable;
        set => SetProperty(ref _isGitAvailable, value);
    }

    /// <summary>
    /// Command to commit selected files
    /// </summary>
    public RelayCommand CommitCommand { get; }

    /// <summary>
    /// Command to edit file in VGrid
    /// </summary>
    public RelayCommand<UncommittedFile> EditFileCommand { get; }

    /// <summary>
    /// Command to show diff
    /// </summary>
    public RelayCommand<UncommittedFile> ShowDiffCommand { get; }

    /// <summary>
    /// Command to open folder in explorer
    /// </summary>
    public RelayCommand<UncommittedFile> OpenFolderCommand { get; }

    /// <summary>
    /// Command to open in default editor
    /// </summary>
    public RelayCommand<UncommittedFile> OpenInEditorCommand { get; }

    /// <summary>
    /// Command to revert file changes
    /// </summary>
    public RelayCommand<UncommittedFile> RevertFileCommand { get; }

    /// <summary>
    /// Sets the repository path and triggers a refresh
    /// </summary>
    public void SetRepositoryPath(string? path)
    {
        _repositoryPath = path;

        // Stop existing timer
        StopAutoRefresh();

        // Start auto-refresh if path is set
        if (!string.IsNullOrEmpty(path))
        {
            _ = RefreshUncommittedFilesAsync();
            StartAutoRefresh();
        }
        else
        {
            UncommittedFiles.Clear();
        }
    }

    /// <summary>
    /// Starts automatic refresh timer (every 5 seconds)
    /// </summary>
    private void StartAutoRefresh()
    {
        // Create timer that fires every 5 seconds
        _autoRefreshTimer = new System.Threading.Timer(
            async _ => await RefreshUncommittedFilesAsync(),
            null,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5)
        );
    }

    /// <summary>
    /// Stops automatic refresh timer
    /// </summary>
    private void StopAutoRefresh()
    {
        _autoRefreshTimer?.Dispose();
        _autoRefreshTimer = null;
    }

    /// <summary>
    /// Refreshes the list of uncommitted TSV files
    /// </summary>
    private async Task RefreshUncommittedFilesAsync()
    {
        if (string.IsNullOrEmpty(_repositoryPath))
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                UncommittedFiles.Clear();
                StatusMessage = "No folder selected";
            });
            return;
        }

        if (!IsGitAvailable)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                UncommittedFiles.Clear();
                StatusMessage = "Git is not installed or not available in PATH";
            });
            return;
        }

        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => IsRefreshing = true);
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => StatusMessage = null);

        try
        {
            // Check if in a Git repository
            var isInRepo = await _gitService.IsInGitRepositoryAsync(_repositoryPath);
            if (!isInRepo)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    UncommittedFiles.Clear();
                    StatusMessage = "Not a Git repository";
                });
                return;
            }

            // Get repository root
            var repoRoot = await _gitService.GetRepositoryRootAsync(_repositoryPath);
            if (string.IsNullOrEmpty(repoRoot))
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    UncommittedFiles.Clear();
                    StatusMessage = "Could not find Git repository root";
                });
                return;
            }

            // Get uncommitted files
            var uncommittedFiles = await _gitService.GetUncommittedFilesAsync(repoRoot);

            // Filter for TSV files only (.tsv, .txt, .tab)
            var tsvExtensions = new[] { ".tsv", ".txt", ".tab" };
            var filteredFiles = uncommittedFiles
                .Where(f => tsvExtensions.Contains(Path.GetExtension(f.filePath).ToLowerInvariant()))
                .ToList();

            // Update the collection on UI thread
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                UncommittedFiles.Clear();
                foreach (var (filePath, status) in filteredFiles)
                {
                    var relativePath = Path.GetRelativePath(repoRoot, filePath);
                    UncommittedFiles.Add(new UncommittedFile
                    {
                        FilePath = filePath,
                        RelativePath = relativePath,
                        Status = status
                    });
                }

                if (UncommittedFiles.Count == 0)
                {
                    StatusMessage = "No uncommitted TSV files";
                }
            });
        }
        catch (Exception ex)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                StatusMessage = $"Error: {ex.Message}";
            });
        }
        finally
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => IsRefreshing = false);
        }
    }

    /// <summary>
    /// Commits the selected files
    /// </summary>
    private async Task CommitSelectedFilesAsync()
    {
        if (string.IsNullOrEmpty(_repositoryPath))
            return;

        var selectedFiles = UncommittedFiles.Where(f => f.IsSelected).ToList();
        if (!selectedFiles.Any())
        {
            _statusBarViewModel?.ShowMessage("No files selected for commit");
            return;
        }

        if (string.IsNullOrWhiteSpace(CommitMessage))
        {
            _statusBarViewModel?.ShowMessage("Please enter a commit message");
            return;
        }

        try
        {
            IsRefreshing = true;

            // Get repository root
            var repoRoot = await _gitService.GetRepositoryRootAsync(_repositoryPath);
            if (string.IsNullOrEmpty(repoRoot))
            {
                _statusBarViewModel?.ShowMessage("Could not find Git repository root");
                return;
            }

            // Stage selected files
            var filePaths = selectedFiles.Select(f => f.FilePath);
            var stageSuccess = await _gitService.StageFilesAsync(repoRoot, filePaths);
            if (!stageSuccess)
            {
                _statusBarViewModel?.ShowMessage("Failed to stage files for commit");
                return;
            }

            // Commit
            var commitSuccess = await _gitService.CommitAsync(repoRoot, CommitMessage);
            if (!commitSuccess)
            {
                _statusBarViewModel?.ShowMessage("Failed to create commit");
                return;
            }

            // Success - show message and refresh
            _statusBarViewModel?.ShowMessage($"Successfully committed {selectedFiles.Count} file(s)");

            // Clear commit message and refresh file list
            CommitMessage = string.Empty;
            await RefreshUncommittedFilesAsync();
        }
        catch (Exception ex)
        {
            _statusBarViewModel?.ShowMessage($"Error during commit: {ex.Message}");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>
    /// Determines whether commit can be executed
    /// </summary>
    private bool CanCommit()
    {
        return !string.IsNullOrWhiteSpace(CommitMessage) &&
               UncommittedFiles.Any(f => f.IsSelected) &&
               !IsRefreshing;
    }

    /// <summary>
    /// Checks if Git is available on the system
    /// </summary>
    private async void CheckGitAvailabilityAsync()
    {
        try
        {
            IsGitAvailable = await _gitService.IsGitAvailableAsync();
            if (!IsGitAvailable)
            {
                StatusMessage = "Git is not installed or not available in PATH";
            }
        }
        catch
        {
            IsGitAvailable = false;
            StatusMessage = "Git is not installed or not available in PATH";
        }
    }

    /// <summary>
    /// Opens file in VGrid for editing
    /// </summary>
    private void EditFile(UncommittedFile? file)
    {
        if (file == null || string.IsNullOrEmpty(file.FilePath))
            return;

        // Trigger file open event - MainViewModel will handle this
        FileOpenRequested?.Invoke(this, file.FilePath);
    }

    /// <summary>
    /// Shows diff for the file
    /// </summary>
    private void ShowDiff(UncommittedFile? file)
    {
        if (file == null || string.IsNullOrEmpty(file.FilePath))
            return;

        try
        {
            // Use git diff command to show changes
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"diff \"{file.FilePath}\"",
                WorkingDirectory = Path.GetDirectoryName(file.FilePath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(output))
                {
                    // Show diff in a message box for now (could be improved with a dedicated diff viewer)
                    System.Windows.MessageBox.Show(output, $"Diff: {file.FileName}",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    _statusBarViewModel?.ShowMessage("No differences to show");
                }
            }
        }
        catch (Exception ex)
        {
            _statusBarViewModel?.ShowMessage($"Error showing diff: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens the file's folder in Windows Explorer
    /// </summary>
    private void OpenFolder(UncommittedFile? file)
    {
        if (file == null || string.IsNullOrEmpty(file.FilePath))
            return;

        try
        {
            var folder = Path.GetDirectoryName(file.FilePath);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                System.Diagnostics.Process.Start("explorer.exe", folder);
            }
        }
        catch (Exception ex)
        {
            _statusBarViewModel?.ShowMessage($"Error opening folder: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens the file in default editor
    /// </summary>
    private void OpenInEditor(UncommittedFile? file)
    {
        if (file == null || string.IsNullOrEmpty(file.FilePath))
            return;

        try
        {
            if (File.Exists(file.FilePath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = file.FilePath,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            _statusBarViewModel?.ShowMessage($"Error opening file: {ex.Message}");
        }
    }

    /// <summary>
    /// Reverts changes to the file
    /// </summary>
    private async Task RevertFileAsync(UncommittedFile? file)
    {
        if (file == null || string.IsNullOrEmpty(file.FilePath))
            return;

        // Confirm with user
        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to revert changes to '{file.FileName}'?\n\nThis cannot be undone.",
            "Revert Changes",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes)
            return;

        try
        {
            var repoRoot = await _gitService.GetRepositoryRootAsync(file.FilePath);
            if (string.IsNullOrEmpty(repoRoot))
            {
                _statusBarViewModel?.ShowMessage("Could not find Git repository root");
                return;
            }

            var relativePath = Path.GetRelativePath(repoRoot, file.FilePath).Replace('\\', '/');

            // Use git checkout to revert the file
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"checkout HEAD \"{relativePath}\"",
                WorkingDirectory = repoRoot,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = System.Diagnostics.Process.Start(startInfo);
            if (process != null)
            {
                var error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    _statusBarViewModel?.ShowMessage($"Reverted changes to {file.FileName}");
                    await RefreshUncommittedFilesAsync();
                }
                else
                {
                    _statusBarViewModel?.ShowMessage($"Failed to revert: {error}");
                }
            }
        }
        catch (Exception ex)
        {
            _statusBarViewModel?.ShowMessage($"Error reverting file: {ex.Message}");
        }
    }

    /// <summary>
    /// Event fired when a file should be opened in VGrid
    /// </summary>
    public event EventHandler<string>? FileOpenRequested;

    /// <summary>
    /// Disposes resources (timer)
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        StopAutoRefresh();
        _disposed = true;
    }
}
