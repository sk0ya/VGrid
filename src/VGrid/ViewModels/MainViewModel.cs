using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.WindowsAPICodePack.Dialogs;
using VGrid.Commands;
using VGrid.Helpers;
using VGrid.Models;
using VGrid.Services;
using VGrid.VimEngine;
using WpfCommand = System.Windows.Input.ICommand;

namespace VGrid.ViewModels;

/// <summary>
/// Main ViewModel for the application
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly ITsvFileService _fileService;
    private readonly ISettingsService _settingsService;
    private readonly IGitService _gitService;
    private readonly IColumnWidthService _columnWidthService;
    private TabItemViewModel? _selectedTab;
    private string? _selectedFolderPath;
    private bool _isVimModeEnabled = true;
    private string _filterText = string.Empty;

    public MainViewModel()
    {
        _fileService = new TsvFileService();
        _settingsService = new SettingsService();
        _gitService = new GitService();
        _columnWidthService = new ColumnWidthService();

        Tabs = new ObservableCollection<TabItemViewModel>();
        StatusBarViewModel = new StatusBarViewModel();
        GitChangesViewModel = new GitChangesViewModel(_gitService, StatusBarViewModel);

        // Subscribe to GitChangesViewModel events
        GitChangesViewModel.FileOpenRequested += OnFileOpenRequested;

        // Initialize commands
        NewFileCommand = new RelayCommand(NewFile);
        OpenFileCommand = new RelayCommand(OpenFile);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        SaveFileCommand = new RelayCommand(SaveFile, CanSaveFile);
        SaveAsFileCommand = new RelayCommand(SaveFileAs);
        CloseTabCommand = new RelayCommand<TabItemViewModel>(CloseTab);
        ExitCommand = new RelayCommand(Exit);
        InsertRowAboveCommand = new RelayCommand<int>(InsertRowAbove);
        InsertRowBelowCommand = new RelayCommand<int>(InsertRowBelow);
        InsertColumnLeftCommand = new RelayCommand<int>(InsertColumnLeft);
        InsertColumnRightCommand = new RelayCommand<int>(InsertColumnRight);
        ToggleVimModeCommand = new RelayCommand(ToggleVimMode);
        ViewGitHistoryCommand = new RelayCommand(async () => await ViewGitHistoryAsync(), CanViewGitHistory);
        OpenFileInExplorerCommand = new RelayCommand(OpenFileInExplorer, CanOpenFileInExplorer);

        // Subscribe to SelectedFolderPath changes to update GitChangesViewModel
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SelectedFolderPath))
            {
                GitChangesViewModel.SetRepositoryPath(SelectedFolderPath);
            }
        };

        // Session restoration will be done after window loads
        // Don't create a new file here - let RestoreSessionAsync handle it
    }

    public ObservableCollection<TabItemViewModel> Tabs { get; }
    public StatusBarViewModel StatusBarViewModel { get; }
    public GitChangesViewModel GitChangesViewModel { get; }

    /// <summary>
    /// Event raised when scrolling to center is requested from a VimState
    /// </summary>
    public event EventHandler? OnScrollToCenterRequested;

    /// <summary>
    /// Event raised when a tab is closed (Phase 2 optimization: notify DataGridManager for cleanup)
    /// </summary>
    public event EventHandler<TabItemViewModel>? TabClosed;

    public IColumnWidthService ColumnWidthService => _columnWidthService;

    public TabItemViewModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetProperty(ref _selectedTab, value))
            {
                UpdateStatusBarForTab(value);
            }
        }
    }

    public string? SelectedFolderPath
    {
        get => _selectedFolderPath;
        set => SetProperty(ref _selectedFolderPath, value);
    }

    public bool IsVimModeEnabled
    {
        get => _isVimModeEnabled;
        set
        {
            if (SetProperty(ref _isVimModeEnabled, value))
            {
                StatusBarViewModel.ShowMessage(value ? "Vim mode enabled" : "Vim mode disabled");
            }
        }
    }

    public string FilterText
    {
        get => _filterText;
        set => SetProperty(ref _filterText, value);
    }

    public WpfCommand NewFileCommand { get; }
    public WpfCommand OpenFileCommand { get; }
    public WpfCommand OpenFolderCommand { get; }
    public WpfCommand SaveFileCommand { get; }
    public WpfCommand SaveAsFileCommand { get; }
    public WpfCommand CloseTabCommand { get; }
    public WpfCommand ExitCommand { get; }
    public WpfCommand InsertRowAboveCommand { get; }
    public WpfCommand InsertRowBelowCommand { get; }
    public WpfCommand InsertColumnLeftCommand { get; }
    public WpfCommand InsertColumnRightCommand { get; }
    public WpfCommand ToggleVimModeCommand { get; }
    public WpfCommand ViewGitHistoryCommand { get; }
    public WpfCommand OpenFileInExplorerCommand { get; }

    public string WindowTitle => "VGrid - TSV Editor with Vim Keybindings";

    /// <summary>
    /// Handles file open request from GitChangesViewModel
    /// </summary>
    private async void OnFileOpenRequested(object? sender, string filePath)
    {
        await OpenFileAsync(filePath);
    }

    private void NewFile()
    {
        var commandHistory = new CommandHistory();
        var document = TsvDocument.CreateEmpty();
        var gridViewModel = new TsvGridViewModel(commandHistory);
        gridViewModel.LoadDocument(document);

        var vimState = new VimState
        {
            CommandHistory = commandHistory
        };

        var tab = new TabItemViewModel($"Untitled{Tabs.Count + 1}.tsv", document, vimState, gridViewModel);

        // Subscribe to Vim state changes
        vimState.PropertyChanged += (s, e) =>
        {
            if (tab == SelectedTab)
            {
                if (e.PropertyName == nameof(VimState.CurrentMode))
                {
                    UpdateStatusBarMode(vimState);
                }
                else if (e.PropertyName == nameof(VimState.CursorPosition))
                {
                    StatusBarViewModel.UpdatePosition(vimState.CursorPosition.Row, vimState.CursorPosition.Column);
                    gridViewModel.CursorPosition = vimState.CursorPosition;
                }
                else if (e.PropertyName == nameof(VimState.SearchPattern))
                {
                    StatusBarViewModel.MessageText = vimState.SearchPattern;
                }
                else if (e.PropertyName == nameof(VimState.IsSearchActive))
                {
                    if (!vimState.IsSearchActive)
                    {
                        StatusBarViewModel.ClearMessage();
                        ClearSearchHighlighting(tab.Document);
                    }
                    else
                    {
                        UpdateSearchHighlighting(tab);
                    }
                }
                else if (e.PropertyName == nameof(VimState.CurrentMatchIndex))
                {
                    UpdateSearchHighlighting(tab);
                }
                else if (e.PropertyName == nameof(VimState.ErrorMessage))
                {
                    if (!string.IsNullOrEmpty(vimState.ErrorMessage))
                    {
                        StatusBarViewModel.ShowMessage(vimState.ErrorMessage);
                    }
                }
            }
        };

        // Subscribe to file operation events
        vimState.SaveRequested += (s, e) =>
        {
            if (tab == SelectedTab)
            {
                SaveFile();
            }
        };

        vimState.QuitRequested += (s, forceQuit) =>
        {
            if (tab == SelectedTab)
            {
                if (forceQuit)
                {
                    ForceCloseTab(tab);
                }
                else
                {
                    CloseTab(tab);
                }
            }
        };

        vimState.YankPerformed += (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] YankPerformed in tab: {tab.Header}");
            // Clear LastYank in all OTHER tabs (not the current one)
            int clearedCount = 0;
            foreach (var otherTab in Tabs)
            {
                if (otherTab != tab && otherTab.VimState != null)
                {
                    otherTab.VimState.LastYank = null;
                    clearedCount++;
                }
            }
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Cleared LastYank in {clearedCount} other tabs");
        };

        vimState.PreviousTabRequested += (s, e) =>
        {
            if (tab == SelectedTab)
            {
                SwitchToPreviousTab();
            }
        };

        vimState.NextTabRequested += (s, e) =>
        {
            if (tab == SelectedTab)
            {
                SwitchToNextTab();
            }
        };

        vimState.ScrollToCenterRequested += (s, e) =>
        {
            if (tab == SelectedTab)
            {
                OnScrollToCenterRequested?.Invoke(this, EventArgs.Empty);
            }
        };

        Tabs.Add(tab);
        SelectedTab = tab;
    }

    private async void OpenFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "TSV Files (*.tsv)|*.tsv|Text Files (*.txt)|*.txt|Tab-separated Files (*.tab)|*.tab|All Files (*.*)|*.*",
            Title = "Open TSV File",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var fileName in dialog.FileNames)
            {
                await OpenFileAsync(fileName);
            }
        }
    }

    public async System.Threading.Tasks.Task OpenFileAsync(string filePath)
    {
        // Check if file is already open
        var existingTab = Tabs.FirstOrDefault(t => t.FilePath == filePath);
        if (existingTab != null)
        {
            SelectedTab = existingTab;
            return;
        }

        try
        {
            var document = await _fileService.LoadAsync(filePath);
            var commandHistory = new CommandHistory();
            var gridViewModel = new TsvGridViewModel(commandHistory);
            gridViewModel.LoadDocument(document);

            var vimState = new VimState
            {
                CommandHistory = commandHistory
            };

            var tab = new TabItemViewModel(filePath, document, vimState, gridViewModel);

            // Subscribe to Vim state changes
            vimState.PropertyChanged += (s, e) =>
            {
                if (tab == SelectedTab)
                {
                    if (e.PropertyName == nameof(VimState.CurrentMode))
                    {
                        StatusBarViewModel.UpdateMode(vimState.CurrentMode);
                    }
                    else if (e.PropertyName == nameof(VimState.CursorPosition))
                    {
                        StatusBarViewModel.UpdatePosition(vimState.CursorPosition.Row, vimState.CursorPosition.Column);
                        gridViewModel.CursorPosition = vimState.CursorPosition;
                    }
                    else if (e.PropertyName == nameof(VimState.SearchPattern))
                    {
                        StatusBarViewModel.MessageText = vimState.SearchPattern;
                    }
                    else if (e.PropertyName == nameof(VimState.IsSearchActive))
                    {
                        if (!vimState.IsSearchActive)
                        {
                            StatusBarViewModel.ClearMessage();
                            ClearSearchHighlighting(tab.Document);
                        }
                        else
                        {
                            UpdateSearchHighlighting(tab);
                        }
                    }
                    else if (e.PropertyName == nameof(VimState.CurrentMatchIndex))
                    {
                        UpdateSearchHighlighting(tab);
                    }
                    else if (e.PropertyName == nameof(VimState.ErrorMessage))
                    {
                        if (!string.IsNullOrEmpty(vimState.ErrorMessage))
                        {
                            StatusBarViewModel.ShowMessage(vimState.ErrorMessage);
                        }
                    }
                }
            };

            // Subscribe to file operation events
            vimState.SaveRequested += (s, e) =>
            {
                if (tab == SelectedTab)
                {
                    SaveFile();
                }
            };

            vimState.QuitRequested += (s, forceQuit) =>
            {
                if (tab == SelectedTab)
                {
                    if (forceQuit)
                    {
                        ForceCloseTab(tab);
                    }
                    else
                    {
                        CloseTab(tab);
                    }
                }
            };

            vimState.YankPerformed += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] YankPerformed in tab: {tab.Header}");
                // Clear LastYank in all OTHER tabs (not the current one)
                int clearedCount = 0;
                foreach (var otherTab in Tabs)
                {
                    if (otherTab != tab && otherTab.VimState != null)
                    {
                        otherTab.VimState.LastYank = null;
                        clearedCount++;
                    }
                }
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] Cleared LastYank in {clearedCount} other tabs");
            };

            vimState.PreviousTabRequested += (s, e) =>
            {
                if (tab == SelectedTab)
                {
                    SwitchToPreviousTab();
                }
            };

            vimState.NextTabRequested += (s, e) =>
            {
                if (tab == SelectedTab)
                {
                    SwitchToNextTab();
                }
            };

            vimState.ScrollToCenterRequested += (s, e) =>
            {
                if (tab == SelectedTab)
                {
                    OnScrollToCenterRequested?.Invoke(this, EventArgs.Empty);
                }
            };

            Tabs.Add(tab);
            SelectedTab = tab;
            StatusBarViewModel.ShowMessage($"Opened: {filePath}");

            // Update SelectedFolderPath to the repository root if the file is in a Git repository
            await UpdateFolderPathForFileAsync(filePath);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Updates the SelectedFolderPath to the Git repository root of the given file
    /// </summary>
    private async System.Threading.Tasks.Task UpdateFolderPathForFileAsync(string filePath)
    {
        try
        {
            // Check if the file is in a Git repository
            if (await _gitService.IsInGitRepositoryAsync(filePath))
            {
                // Get the repository root
                var repoRoot = await _gitService.GetRepositoryRootAsync(filePath);
                if (!string.IsNullOrEmpty(repoRoot))
                {
                    // Update SelectedFolderPath to the repository root
                    SelectedFolderPath = repoRoot;
                }
            }
        }
        catch
        {
            // Silently fail - this is just a convenience feature
        }
    }

    private void OpenFolder()
    {
        var dialog = new CommonOpenFileDialog
        {
            Title = "Select folder to explore",
            IsFolderPicker = true
        };

        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            SelectedFolderPath = dialog.FileName;
        }
    }

    private bool CanSaveFile()
    {
        return SelectedTab != null && !string.IsNullOrEmpty(SelectedTab.FilePath) &&
               !SelectedTab.FilePath.StartsWith("Untitled");
    }

    public async void SaveFile()
    {
        if (SelectedTab == null)
            return;

        if (string.IsNullOrEmpty(SelectedTab.FilePath) || SelectedTab.FilePath.StartsWith("Untitled"))
        {
            SaveFileAs();
            return;
        }

        try
        {
            await _fileService.SaveAsync(SelectedTab.Document, SelectedTab.FilePath);
            StatusBarViewModel.ShowMessage($"Saved: {SelectedTab.FilePath}");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SaveFileAs()
    {
        if (SelectedTab == null)
            return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "TSV Files (*.tsv)|*.tsv|Text Files (*.txt)|*.txt|Tab-separated Files (*.tab)|*.tab|All Files (*.*)|*.*",
            Title = "Save TSV File",
            DefaultExt = ".tsv",
            FileName = Path.GetFileName(SelectedTab.FilePath)
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _fileService.SaveAsync(SelectedTab.Document, dialog.FileName);
                SelectedTab.FilePath = dialog.FileName;
                SelectedTab.Header = Path.GetFileName(dialog.FileName);
                StatusBarViewModel.ShowMessage($"Saved: {dialog.FileName}");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public void CloseTab(TabItemViewModel? tab)
    {
        if (tab == null)
            return;

        if (tab.IsDirty)
        {
            var result = System.Windows.MessageBox.Show(
                $"Do you want to save changes to {tab.Header}?",
                "VGrid",
                System.Windows.MessageBoxButton.YesNoCancel,
                System.Windows.MessageBoxImage.Question);

            if (result == System.Windows.MessageBoxResult.Cancel)
                return;

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                var previousSelected = SelectedTab;
                SelectedTab = tab;
                SaveFile();
                SelectedTab = previousSelected;
            }
        }

        Tabs.Remove(tab);

        // Phase 2 optimization: Notify listeners to cleanup cached handlers
        TabClosed?.Invoke(this, tab);

        // If we closed the selected tab, select another
        if (SelectedTab == tab || SelectedTab == null)
        {
            SelectedTab = Tabs.LastOrDefault();
        }

        // If no tabs left, create a new one
        if (Tabs.Count == 0)
        {
            NewFile();
        }
    }

    /// <summary>
    /// Closes a tab without prompting for save (for :q!)
    /// </summary>
    private void ForceCloseTab(TabItemViewModel? tab)
    {
        if (tab == null)
            return;

        Tabs.Remove(tab);

        // Phase 2 optimization: Notify listeners to cleanup cached handlers
        TabClosed?.Invoke(this, tab);

        // If we closed the selected tab, select another
        if (SelectedTab == tab || SelectedTab == null)
        {
            SelectedTab = Tabs.LastOrDefault();
        }

        // If no tabs left, create a new one
        if (Tabs.Count == 0)
        {
            NewFile();
        }
    }

    private void Exit()
    {
        System.Windows.Application.Current.Shutdown();
    }

    private void InsertRowAbove(int rowIndex)
    {
        if (SelectedTab == null)
            return;

        SelectedTab.GridViewModel.InsertRow(rowIndex);
        SelectedTab.VimState.CursorPosition = new Models.GridPosition(rowIndex, 0);
        StatusBarViewModel.ShowMessage($"Inserted row at {rowIndex}");
    }

    private void InsertRowBelow(int rowIndex)
    {
        if (SelectedTab == null)
            return;

        int insertIndex = rowIndex + 1;
        SelectedTab.GridViewModel.InsertRow(insertIndex);
        SelectedTab.VimState.CursorPosition = new Models.GridPosition(insertIndex, 0);
        StatusBarViewModel.ShowMessage($"Inserted row at {insertIndex}");
    }

    private void InsertColumnLeft(int columnIndex)
    {
        if (SelectedTab == null)
            return;

        SelectedTab.GridViewModel.InsertColumn(columnIndex);
        SelectedTab.VimState.CursorPosition = new Models.GridPosition(0, columnIndex);
        StatusBarViewModel.ShowMessage($"Inserted column at {columnIndex}");
    }

    private void InsertColumnRight(int columnIndex)
    {
        if (SelectedTab == null)
            return;

        int insertIndex = columnIndex + 1;
        SelectedTab.GridViewModel.InsertColumn(insertIndex);
        SelectedTab.VimState.CursorPosition = new Models.GridPosition(0, insertIndex);
        StatusBarViewModel.ShowMessage($"Inserted column at {insertIndex}");
    }

    private void ToggleVimMode()
    {
        IsVimModeEnabled = !IsVimModeEnabled;
        StatusBarViewModel.ShowMessage(IsVimModeEnabled ? "Vim mode enabled" : "Vim mode disabled");
    }

    private void UpdateStatusBarForTab(TabItemViewModel? tab)
    {
        if (tab == null)
            return;

        UpdateStatusBarMode(tab.VimState);
        StatusBarViewModel.UpdatePosition(tab.VimState.CursorPosition.Row, tab.VimState.CursorPosition.Column);
    }

    private void UpdateStatusBarMode(VimState vimState)
    {
        // Get the mode display name from VimState (handles VISUAL LINE, VISUAL BLOCK, etc.)
        var modeText = vimState.GetModeDisplayName();
        StatusBarViewModel.UpdateMode(vimState.CurrentMode, modeText);
    }

    private void ClearSearchHighlighting(TsvDocument document)
    {
        foreach (var row in document.Rows)
        {
            foreach (var cell in row.Cells)
            {
                cell.IsSearchMatch = false;
            }
        }
    }

    private void UpdateSearchHighlighting(TabItemViewModel tab)
    {
        // Clear all previous highlighting
        ClearSearchHighlighting(tab.Document);

        // Highlight current match only
        if (tab.VimState.CurrentMatchIndex >= 0 &&
            tab.VimState.CurrentMatchIndex < tab.VimState.SearchResults.Count)
        {
            var matchPos = tab.VimState.SearchResults[tab.VimState.CurrentMatchIndex];
            var cell = tab.Document.Rows[matchPos.Row].Cells[matchPos.Column];
            cell.IsSearchMatch = true;
        }
    }

    public async System.Threading.Tasks.Task RestoreSessionAsync()
    {
        var session = _settingsService.LoadSession();
        if (session == null || session.OpenFiles.Count == 0)
        {
            // No session to restore, create a new file
            NewFile();
            return;
        }

        // Restore Vim mode setting
        IsVimModeEnabled = session.IsVimModeEnabled;

        // Restore files on background thread
        int validTabCount = 0;
        foreach (var filePath in session.OpenFiles)
        {
            if (File.Exists(filePath))
            {
                await OpenFileAsync(filePath);
                validTabCount++;
            }
        }

        // If no valid tabs were restored, create a new file
        if (validTabCount == 0)
        {
            NewFile();
            return;
        }

        // Restore selected tab
        if (session.SelectedTabIndex >= 0 && session.SelectedTabIndex < Tabs.Count)
        {
            SelectedTab = Tabs[session.SelectedTabIndex];
        }

        // Restore folder path
        if (!string.IsNullOrEmpty(session.SelectedFolderPath) &&
            Directory.Exists(session.SelectedFolderPath))
        {
            SelectedFolderPath = session.SelectedFolderPath;
        }
    }

    public void SaveSession()
    {
        var session = new SessionSettings
        {
            OpenFiles = Tabs
                .Where(t => !string.IsNullOrEmpty(t.FilePath) &&
                           !t.FilePath.StartsWith("Untitled"))
                .Select(t => t.FilePath!)
                .ToList(),
            SelectedTabIndex = SelectedTab != null ? Tabs.IndexOf(SelectedTab) : 0,
            SelectedFolderPath = SelectedFolderPath,
            IsVimModeEnabled = IsVimModeEnabled
        };

        _settingsService.SaveSession(session);
    }

    private bool CanViewGitHistory()
    {
        return !string.IsNullOrEmpty(SelectedFolderPath);
    }

    private async System.Threading.Tasks.Task ViewGitHistoryAsync()
    {
        if (string.IsNullOrEmpty(SelectedFolderPath))
            return;

        // Check if git is available
        if (!await _gitService.IsGitAvailableAsync())
        {
            System.Windows.MessageBox.Show(
                "Git is not available. Please install Git and ensure it's in your PATH.",
                "Git Not Found",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // Check if folder is in a git repository
        if (!await _gitService.IsInGitRepositoryAsync(SelectedFolderPath))
        {
            System.Windows.MessageBox.Show(
                "This folder is not in a Git repository.",
                "Not in Git Repository",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            var repoRoot = await _gitService.GetRepositoryRootAsync(SelectedFolderPath);
            if (string.IsNullOrEmpty(repoRoot))
            {
                System.Windows.MessageBox.Show(
                    "Could not determine Git repository root.",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var viewModel = new GitHistoryViewModel(
                SelectedFolderPath,
                repoRoot,
                _gitService);

            var window = new Views.GitHistoryWindow(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            window.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error opening Git history: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private bool CanOpenFileInExplorer()
    {
        return SelectedTab != null &&
               !string.IsNullOrEmpty(SelectedTab.FilePath) &&
               !SelectedTab.FilePath.StartsWith("Untitled") &&
               File.Exists(SelectedTab.FilePath);
    }

    private void OpenFileInExplorer()
    {
        // This is handled directly in MainWindow to access the FolderTreeView
        // The command is kept for future extensibility
    }

    /// <summary>
    /// Switches to the previous tab
    /// </summary>
    private void SwitchToPreviousTab()
    {
        if (Tabs.Count <= 1)
            return;

        int currentIndex = SelectedTab != null ? Tabs.IndexOf(SelectedTab) : -1;
        if (currentIndex <= 0)
        {
            // Wrap around to the last tab
            SelectedTab = Tabs[Tabs.Count - 1];
        }
        else
        {
            SelectedTab = Tabs[currentIndex - 1];
        }
    }

    /// <summary>
    /// Switches to the next tab
    /// </summary>
    private void SwitchToNextTab()
    {
        if (Tabs.Count <= 1)
            return;

        int currentIndex = SelectedTab != null ? Tabs.IndexOf(SelectedTab) : -1;
        if (currentIndex < 0 || currentIndex >= Tabs.Count - 1)
        {
            // Wrap around to the first tab
            SelectedTab = Tabs[0];
        }
        else
        {
            SelectedTab = Tabs[currentIndex + 1];
        }
    }
}
