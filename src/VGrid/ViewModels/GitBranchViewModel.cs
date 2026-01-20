using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using VGrid.Helpers;
using VGrid.Services;

namespace VGrid.ViewModels;

/// <summary>
/// ViewModel for Git branch management
/// </summary>
public class GitBranchViewModel : ViewModelBase
{
    private readonly IGitService _gitService;
    private readonly string _repoRoot;
    private string? _currentBranch;
    private string? _selectedLocalBranch;
    private string? _selectedRemoteBranch;
    private bool _isLoading;
    private string _statusMessage = string.Empty;
    private int _aheadCount;
    private int _behindCount;

    public GitBranchViewModel(string repoRoot, IGitService gitService)
    {
        _repoRoot = repoRoot;
        _gitService = gitService;

        LocalBranches = new ObservableCollection<string>();
        RemoteBranches = new ObservableCollection<string>();

        CheckoutCommand = new RelayCommand(async () => await CheckoutAsync(), () => !string.IsNullOrEmpty(SelectedLocalBranch) && SelectedLocalBranch != CurrentBranch && !IsLoading);
        CheckoutRemoteCommand = new RelayCommand(async () => await CheckoutRemoteAsync(), () => !string.IsNullOrEmpty(SelectedRemoteBranch) && !IsLoading);
        CreateBranchCommand = new RelayCommand(async () => await CreateBranchAsync(), () => !IsLoading);
        DeleteBranchCommand = new RelayCommand(async () => await DeleteBranchAsync(), () => !string.IsNullOrEmpty(SelectedLocalBranch) && SelectedLocalBranch != CurrentBranch && !IsLoading);
        FetchCommand = new RelayCommand(async () => await FetchAsync(), () => !IsLoading);
        PullCommand = new RelayCommand(async () => await PullAsync(), () => !IsLoading);
        PushCommand = new RelayCommand(async () => await PushAsync(), () => !IsLoading);
        RefreshCommand = new RelayCommand(async () => await LoadBranchesAsync(), () => !IsLoading);

        // Load branches on initialization
        _ = LoadBranchesAsync();
    }

    public ObservableCollection<string> LocalBranches { get; }
    public ObservableCollection<string> RemoteBranches { get; }

    public string? CurrentBranch
    {
        get => _currentBranch;
        set
        {
            if (SetProperty(ref _currentBranch, value))
            {
                OnPropertyChanged(nameof(CurrentBranchDisplay));
            }
        }
    }

    public string CurrentBranchDisplay => string.IsNullOrEmpty(CurrentBranch) ? "No branch" : CurrentBranch;

    public string? SelectedLocalBranch
    {
        get => _selectedLocalBranch;
        set
        {
            if (SetProperty(ref _selectedLocalBranch, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string? SelectedRemoteBranch
    {
        get => _selectedRemoteBranch;
        set
        {
            if (SetProperty(ref _selectedRemoteBranch, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public int AheadCount
    {
        get => _aheadCount;
        set
        {
            if (SetProperty(ref _aheadCount, value))
            {
                OnPropertyChanged(nameof(TrackingStatusDisplay));
            }
        }
    }

    public int BehindCount
    {
        get => _behindCount;
        set
        {
            if (SetProperty(ref _behindCount, value))
            {
                OnPropertyChanged(nameof(TrackingStatusDisplay));
            }
        }
    }

    public string TrackingStatusDisplay
    {
        get
        {
            if (AheadCount == 0 && BehindCount == 0)
                return string.Empty;
            return $"↑{AheadCount} ↓{BehindCount}";
        }
    }

    public ICommand CheckoutCommand { get; }
    public ICommand CheckoutRemoteCommand { get; }
    public ICommand CreateBranchCommand { get; }
    public ICommand DeleteBranchCommand { get; }
    public ICommand FetchCommand { get; }
    public ICommand PullCommand { get; }
    public ICommand PushCommand { get; }
    public ICommand RefreshCommand { get; }

    /// <summary>
    /// Event raised when branch is changed
    /// </summary>
    public event EventHandler<string>? BranchChanged;

    public async Task LoadBranchesAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading branches...";

        try
        {
            // Get current branch
            CurrentBranch = await _gitService.GetCurrentBranchAsync(_repoRoot);

            // Get local branches
            var localBranches = await _gitService.GetLocalBranchesAsync(_repoRoot);
            LocalBranches.Clear();
            foreach (var branch in localBranches)
            {
                LocalBranches.Add(branch);
            }

            // Get remote branches
            var remoteBranches = await _gitService.GetRemoteBranchesAsync(_repoRoot);
            RemoteBranches.Clear();
            foreach (var branch in remoteBranches)
            {
                RemoteBranches.Add(branch);
            }

            // Get tracking status
            var (ahead, behind) = await _gitService.GetTrackingStatusAsync(_repoRoot);
            AheadCount = ahead;
            BehindCount = behind;

            StatusMessage = $"Loaded {LocalBranches.Count} local and {RemoteBranches.Count} remote branches";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CheckoutAsync()
    {
        if (string.IsNullOrEmpty(SelectedLocalBranch))
            return;

        IsLoading = true;
        StatusMessage = $"Switching to {SelectedLocalBranch}...";

        try
        {
            var (success, message) = await _gitService.CheckoutBranchAsync(_repoRoot, SelectedLocalBranch);
            StatusMessage = message;

            if (success)
            {
                CurrentBranch = SelectedLocalBranch;
                BranchChanged?.Invoke(this, SelectedLocalBranch);

                // Update tracking status
                var (ahead, behind) = await _gitService.GetTrackingStatusAsync(_repoRoot);
                AheadCount = ahead;
                BehindCount = behind;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CheckoutRemoteAsync()
    {
        if (string.IsNullOrEmpty(SelectedRemoteBranch))
            return;

        // Extract local branch name from remote branch (e.g., "origin/feature" -> "feature")
        var localBranchName = SelectedRemoteBranch;
        var slashIndex = SelectedRemoteBranch.IndexOf('/');
        if (slashIndex >= 0)
        {
            localBranchName = SelectedRemoteBranch.Substring(slashIndex + 1);
        }

        // Check if local branch already exists
        if (LocalBranches.Contains(localBranchName))
        {
            // Just checkout existing local branch
            SelectedLocalBranch = localBranchName;
            await CheckoutAsync();
            return;
        }

        IsLoading = true;
        StatusMessage = $"Creating and switching to {localBranchName}...";

        try
        {
            // Create new branch tracking remote
            var (exitCode, output, error) = await RunGitCommandAsync($"checkout -b \"{localBranchName}\" \"{SelectedRemoteBranch}\"");

            if (exitCode == 0)
            {
                StatusMessage = $"Switched to new branch '{localBranchName}'";
                await LoadBranchesAsync();
                BranchChanged?.Invoke(this, localBranchName);
            }
            else
            {
                StatusMessage = $"Error: {error}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<(int exitCode, string output, string error)> RunGitCommandAsync(string arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = _repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var process = new System.Diagnostics.Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, output, error);
    }

    private async Task CreateBranchAsync()
    {
        // Show input dialog
        var dialog = new Window
        {
            Title = "Create New Branch",
            Width = 400,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive),
            ResizeMode = ResizeMode.NoResize
        };

        var stackPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(15) };

        stackPanel.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = "Branch Name:",
            Margin = new Thickness(0, 0, 0, 5)
        });

        var textBox = new System.Windows.Controls.TextBox { Margin = new Thickness(0, 0, 0, 10) };
        stackPanel.Children.Add(textBox);

        var checkBox = new System.Windows.Controls.CheckBox
        {
            Content = "Switch to new branch",
            IsChecked = true,
            Margin = new Thickness(0, 0, 0, 15)
        };
        stackPanel.Children.Add(checkBox);

        var buttonPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var okButton = new System.Windows.Controls.Button
        {
            Content = "Create",
            Width = 75,
            Margin = new Thickness(0, 0, 5, 0),
            IsDefault = true
        };

        var cancelButton = new System.Windows.Controls.Button
        {
            Content = "Cancel",
            Width = 75,
            IsCancel = true
        };

        bool dialogResult = false;
        okButton.Click += (s, e) => { dialogResult = true; dialog.Close(); };
        cancelButton.Click += (s, e) => { dialog.Close(); };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        stackPanel.Children.Add(buttonPanel);

        dialog.Content = stackPanel;
        textBox.Focus();
        dialog.ShowDialog();

        if (!dialogResult || string.IsNullOrWhiteSpace(textBox.Text))
            return;

        var branchName = textBox.Text.Trim();
        var checkout = checkBox.IsChecked == true;

        IsLoading = true;
        StatusMessage = $"Creating branch {branchName}...";

        try
        {
            var (success, message) = await _gitService.CreateBranchAsync(_repoRoot, branchName, checkout);
            StatusMessage = message;

            if (success)
            {
                await LoadBranchesAsync();
                if (checkout)
                {
                    BranchChanged?.Invoke(this, branchName);
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteBranchAsync()
    {
        if (string.IsNullOrEmpty(SelectedLocalBranch))
            return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete branch '{SelectedLocalBranch}'?",
            "Delete Branch",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        IsLoading = true;
        StatusMessage = $"Deleting branch {SelectedLocalBranch}...";

        try
        {
            var (success, message) = await _gitService.DeleteBranchAsync(_repoRoot, SelectedLocalBranch);
            StatusMessage = message;

            if (success)
            {
                await LoadBranchesAsync();
            }
            else if (message.Contains("not fully merged"))
            {
                // Ask to force delete
                var forceResult = MessageBox.Show(
                    $"Branch '{SelectedLocalBranch}' is not fully merged.\n\nDo you want to force delete it?",
                    "Force Delete Branch",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (forceResult == MessageBoxResult.Yes)
                {
                    var (forceSuccess, forceMessage) = await _gitService.DeleteBranchAsync(_repoRoot, SelectedLocalBranch, true);
                    StatusMessage = forceMessage;

                    if (forceSuccess)
                    {
                        await LoadBranchesAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task FetchAsync()
    {
        IsLoading = true;
        StatusMessage = "Fetching from remote...";

        try
        {
            var (success, message) = await _gitService.FetchAsync(_repoRoot);
            StatusMessage = message;

            if (success)
            {
                await LoadBranchesAsync();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task PullAsync()
    {
        IsLoading = true;
        StatusMessage = "Pulling from remote...";

        try
        {
            var (success, message) = await _gitService.PullAsync(_repoRoot);
            StatusMessage = message;

            if (success)
            {
                // Update tracking status
                var (ahead, behind) = await _gitService.GetTrackingStatusAsync(_repoRoot);
                AheadCount = ahead;
                BehindCount = behind;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task PushAsync()
    {
        IsLoading = true;
        StatusMessage = "Pushing to remote...";

        try
        {
            var (success, message) = await _gitService.PushAsync(_repoRoot);
            StatusMessage = message;

            if (success)
            {
                // Update tracking status
                var (ahead, behind) = await _gitService.GetTrackingStatusAsync(_repoRoot);
                AheadCount = ahead;
                BehindCount = behind;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
