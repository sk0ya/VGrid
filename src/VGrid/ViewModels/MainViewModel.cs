using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
    private readonly ITemplateService _templateService;
    private readonly IVimrcService _vimrcService;
    private TabItemViewModel? _selectedTab;
    private string? _selectedFolderPath;
    private bool _isVimModeEnabled = true;
    private string _filterText = string.Empty;
    private ObservableCollection<TemplateInfo> _templates = new ObservableCollection<TemplateInfo>();
    private SidebarView _selectedSidebarView = SidebarView.Explorer;
    private bool _isSidebarOpen = true;
    private string _selectedColorTheme = "Light";
    private readonly List<string> _colorThemes = new() { "Light", "Dark" };
    private string _repositoryInfo = string.Empty;

    public MainViewModel()
    {
        _fileService = new TsvFileService();
        _settingsService = new SettingsService();
        _gitService = new GitService();
        _columnWidthService = new ColumnWidthService();
        _templateService = new TemplateService();
        _vimrcService = new VimrcService();

        // Load vimrc configuration
        _vimrcService.Load();

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
        DeleteRowCommand = new RelayCommand<int>(DeleteRow);
        DeleteColumnCommand = new RelayCommand<int>(DeleteColumn);
        ToggleVimModeCommand = new RelayCommand(ToggleVimMode);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        ViewGitHistoryCommand = new RelayCommand(async () => await ViewGitHistoryAsync(), CanViewGitHistory);
        ViewGitBranchesCommand = new RelayCommand(async () => await ViewGitBranchesAsync(), CanViewGitHistory);
        GitFetchCommand = new RelayCommand(async () => await GitFetchAsync(), CanViewGitHistory);
        GitPullCommand = new RelayCommand(async () => await GitPullAsync(), CanViewGitHistory);
        GitPushCommand = new RelayCommand(async () => await GitPushAsync(), CanViewGitHistory);
        OpenFileInExplorerCommand = new RelayCommand(OpenFileInExplorer, CanOpenFileInExplorer);
        OpenTemplateCommand = new RelayCommand<TemplateInfo>(OpenTemplate, CanOpenTemplate);
        NewTemplateCommand = new RelayCommand(NewTemplate);
        DeleteTemplateCommand = new RelayCommand<TemplateInfo>(DeleteTemplate, CanDeleteTemplate);
        RefreshTemplatesCommand = new RelayCommand(RefreshTemplates);
        OpenTemplateFolderCommand = new RelayCommand(OpenTemplateFolder);

        // Subscribe to SelectedFolderPath changes to update GitChangesViewModel
        PropertyChanged += async (s, e) =>
        {
            if (e.PropertyName == nameof(SelectedFolderPath))
            {
                GitChangesViewModel.SetRepositoryPath(SelectedFolderPath);
                await UpdateGitStatusAsync();
            }
        };

        // Load templates
        RefreshTemplates();

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
    public ITemplateService TemplateService => _templateService;

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

    public SidebarView SelectedSidebarView
    {
        get => _selectedSidebarView;
        set => SetProperty(ref _selectedSidebarView, value);
    }

    public bool IsSidebarOpen
    {
        get => _isSidebarOpen;
        set => SetProperty(ref _isSidebarOpen, value);
    }

    public List<string> ColorThemes => _colorThemes;

    public string SelectedColorTheme
    {
        get => _selectedColorTheme;
        set
        {
            if (SetProperty(ref _selectedColorTheme, value))
            {
                ThemeService.Instance.CurrentTheme = value == "Dark" ? ThemeType.Dark : ThemeType.Light;

                // Refresh DataGrid header bindings for all tabs
                foreach (var tab in Tabs)
                {
                    tab.VimState.RefreshCursorPositionBinding();
                }
            }
        }
    }

    /// <summary>
    /// サイドバービューを選択する。同じビューが選択された場合はサイドバーを開閉トグル
    /// </summary>
    public void SelectSidebarView(SidebarView view)
    {
        if (SelectedSidebarView == view)
        {
            // 同じビューをクリックした場合はトグル
            IsSidebarOpen = !IsSidebarOpen;
        }
        else
        {
            // 別のビューを選択した場合は切り替えて開く
            SelectedSidebarView = view;
            IsSidebarOpen = true;
        }
    }

    public ObservableCollection<TemplateInfo> Templates
    {
        get => _templates;
        set => SetProperty(ref _templates, value);
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
    public WpfCommand DeleteRowCommand { get; }
    public WpfCommand DeleteColumnCommand { get; }
    public WpfCommand ToggleVimModeCommand { get; }
    public WpfCommand ToggleThemeCommand { get; }
    public WpfCommand ViewGitHistoryCommand { get; }
    public WpfCommand ViewGitBranchesCommand { get; }
    public WpfCommand GitFetchCommand { get; }
    public WpfCommand GitPullCommand { get; }
    public WpfCommand GitPushCommand { get; }
    public WpfCommand OpenFileInExplorerCommand { get; }
    public WpfCommand OpenTemplateCommand { get; }
    public WpfCommand NewTemplateCommand { get; }
    public WpfCommand DeleteTemplateCommand { get; }
    public WpfCommand RefreshTemplatesCommand { get; }
    public WpfCommand OpenTemplateFolderCommand { get; }

    public string WindowTitle => "VGrid";

    public string RepositoryInfo
    {
        get => _repositoryInfo;
        set => SetProperty(ref _repositoryInfo, value);
    }

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
            CommandHistory = commandHistory,
            KeyBindingConfig = _vimrcService.Config
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
                CommandHistory = commandHistory,
                KeyBindingConfig = _vimrcService.Config
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
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

            // Check if this is a template file
            if (_templateService.IsTemplateFile(SelectedTab.FilePath))
            {
                RefreshTemplates();
                StatusBarViewModel.ShowMessage($"Saved template: {Path.GetFileName(SelectedTab.FilePath)}");
            }
            else
            {
                StatusBarViewModel.ShowMessage($"Saved: {SelectedTab.FilePath}");
            }
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

    /// <summary>
    /// Checks for unsaved changes across all tabs and prompts user to save before closing application.
    /// </summary>
    /// <returns>True if safe to close (saved or user chose not to save), False if user cancelled</returns>
    public bool ConfirmCloseApplication()
    {
        var dirtyTabs = Tabs.Where(t => t.IsDirty).ToList();

        if (dirtyTabs.Count == 0)
            return true; // No unsaved changes, safe to close

        // Show confirmation dialog
        var message = dirtyTabs.Count == 1
            ? $"Do you want to save changes to {dirtyTabs[0].Header}?"
            : $"You have {dirtyTabs.Count} unsaved file(s). Do you want to save them before closing?";

        var result = System.Windows.MessageBox.Show(
            message,
            "VGrid",
            System.Windows.MessageBoxButton.YesNoCancel,
            System.Windows.MessageBoxImage.Question);

        if (result == System.Windows.MessageBoxResult.Cancel)
            return false; // User cancelled, abort close

        if (result == System.Windows.MessageBoxResult.No)
            return true; // User chose to discard changes

        // result == Yes: Save all dirty tabs
        foreach (var tab in dirtyTabs)
        {
            var previousSelected = SelectedTab;
            SelectedTab = tab;

            if (string.IsNullOrEmpty(tab.FilePath) || tab.FilePath.StartsWith("Untitled"))
            {
                // Untitled file needs SaveAs dialog
                SaveFileAs(); // Shows dialog and saves (already implemented)
                if (tab.IsDirty) // User cancelled SaveAs dialog
                {
                    SelectedTab = previousSelected;
                    return false; // Abort close
                }
            }
            else
            {
                // Save existing file
                SaveFile(); // Already implemented
            }

            SelectedTab = previousSelected;
        }

        return true; // All saves completed
    }

    private void Exit()
    {
        // Close the main window instead of direct shutdown
        // This will trigger MainWindow_Closing event which checks for unsaved changes
        System.Windows.Application.Current.MainWindow?.Close();
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

    private void DeleteRow(int rowIndex)
    {
        if (SelectedTab == null)
            return;

        if (SelectedTab.Document.RowCount <= 1)
        {
            StatusBarViewModel.ShowMessage("Cannot delete the last row");
            return;
        }

        SelectedTab.GridViewModel.DeleteRow(rowIndex);

        // Adjust cursor position if needed
        int newRowIndex = Math.Min(rowIndex, SelectedTab.Document.RowCount - 1);
        SelectedTab.VimState.CursorPosition = new Models.GridPosition(newRowIndex, SelectedTab.VimState.CursorPosition.Column);
        StatusBarViewModel.ShowMessage($"Deleted row {rowIndex}");
    }

    private void DeleteColumn(int columnIndex)
    {
        if (SelectedTab == null)
            return;

        if (SelectedTab.Document.ColumnCount <= 1)
        {
            StatusBarViewModel.ShowMessage("Cannot delete the last column");
            return;
        }

        SelectedTab.GridViewModel.DeleteColumn(columnIndex);

        // Adjust cursor position if needed
        int newColumnIndex = Math.Min(columnIndex, SelectedTab.Document.ColumnCount - 1);
        SelectedTab.VimState.CursorPosition = new Models.GridPosition(SelectedTab.VimState.CursorPosition.Row, newColumnIndex);
        StatusBarViewModel.ShowMessage($"Deleted column {columnIndex}");
    }

    /// <summary>
    /// Deletes multiple selected rows
    /// </summary>
    public void DeleteSelectedRows(IEnumerable<int> rowIndices)
    {
        if (SelectedTab == null)
            return;

        var sortedIndices = rowIndices.OrderByDescending(i => i).ToList();

        if (SelectedTab.Document.RowCount - sortedIndices.Count < 1)
        {
            StatusBarViewModel.ShowMessage("Cannot delete all rows");
            return;
        }

        foreach (var rowIndex in sortedIndices)
        {
            if (rowIndex >= 0 && rowIndex < SelectedTab.Document.RowCount)
            {
                SelectedTab.GridViewModel.DeleteRow(rowIndex);
            }
        }

        // Clear selection and adjust cursor
        SelectedTab.VimState.ClearRowSelections();
        SelectedTab.VimState.SwitchMode(VimEngine.VimMode.Normal);
        int newRowIndex = Math.Min(sortedIndices.Min(), SelectedTab.Document.RowCount - 1);
        SelectedTab.VimState.CursorPosition = new Models.GridPosition(newRowIndex, SelectedTab.VimState.CursorPosition.Column);
        StatusBarViewModel.ShowMessage($"Deleted {sortedIndices.Count} row(s)");
    }

    /// <summary>
    /// Deletes multiple selected columns
    /// </summary>
    public void DeleteSelectedColumns(IEnumerable<int> columnIndices)
    {
        if (SelectedTab == null)
            return;

        var sortedIndices = columnIndices.OrderByDescending(i => i).ToList();

        if (SelectedTab.Document.ColumnCount - sortedIndices.Count < 1)
        {
            StatusBarViewModel.ShowMessage("Cannot delete all columns");
            return;
        }

        foreach (var columnIndex in sortedIndices)
        {
            if (columnIndex >= 0 && columnIndex < SelectedTab.Document.ColumnCount)
            {
                SelectedTab.GridViewModel.DeleteColumn(columnIndex);
            }
        }

        // Clear selection and adjust cursor
        SelectedTab.VimState.ClearColumnSelections();
        SelectedTab.VimState.SwitchMode(VimEngine.VimMode.Normal);
        int newColumnIndex = Math.Min(sortedIndices.Min(), SelectedTab.Document.ColumnCount - 1);
        SelectedTab.VimState.CursorPosition = new Models.GridPosition(SelectedTab.VimState.CursorPosition.Row, newColumnIndex);
        StatusBarViewModel.ShowMessage($"Deleted {sortedIndices.Count} column(s)");
    }

    private void ToggleVimMode()
    {
        IsVimModeEnabled = !IsVimModeEnabled;
        StatusBarViewModel.ShowMessage(IsVimModeEnabled ? "Vim mode enabled" : "Vim mode disabled");
    }

    private void ToggleTheme()
    {
        SelectedColorTheme = SelectedColorTheme == "Light" ? "Dark" : "Light";
        StatusBarViewModel.ShowMessage($"{SelectedColorTheme} theme enabled");
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

        // Restore theme setting
        SelectedColorTheme = session.ColorTheme ?? "Light";

        // Restore folder path first (updates folder tree once)
        if (!string.IsNullOrEmpty(session.SelectedFolderPath) &&
            Directory.Exists(session.SelectedFolderPath))
        {
            SelectedFolderPath = session.SelectedFolderPath;
        }

        // Restore files
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
            IsVimModeEnabled = IsVimModeEnabled,
            ColorTheme = SelectedColorTheme
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

    private async System.Threading.Tasks.Task ViewGitBranchesAsync()
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

            var viewModel = new GitBranchViewModel(repoRoot, _gitService);
            viewModel.BranchChanged += async (s, branch) =>
            {
                await UpdateGitStatusAsync();
            };

            var window = new Views.GitBranchWindow(viewModel)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };

            window.Show();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error opening Git branches: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async System.Threading.Tasks.Task GitFetchAsync()
    {
        if (string.IsNullOrEmpty(SelectedFolderPath))
            return;

        var repoRoot = await _gitService.GetRepositoryRootAsync(SelectedFolderPath);
        if (string.IsNullOrEmpty(repoRoot))
            return;

        StatusBarViewModel.ShowMessage("Fetching...");
        var (success, message) = await _gitService.FetchAsync(repoRoot);
        StatusBarViewModel.ShowMessage(message);

        if (success)
        {
            await UpdateGitStatusAsync();
        }
    }

    private async System.Threading.Tasks.Task GitPullAsync()
    {
        if (string.IsNullOrEmpty(SelectedFolderPath))
            return;

        var repoRoot = await _gitService.GetRepositoryRootAsync(SelectedFolderPath);
        if (string.IsNullOrEmpty(repoRoot))
            return;

        StatusBarViewModel.ShowMessage("Pulling...");
        var (success, message) = await _gitService.PullAsync(repoRoot);
        StatusBarViewModel.ShowMessage(message);

        if (success)
        {
            await UpdateGitStatusAsync();
        }
    }

    private async System.Threading.Tasks.Task GitPushAsync()
    {
        if (string.IsNullOrEmpty(SelectedFolderPath))
            return;

        var repoRoot = await _gitService.GetRepositoryRootAsync(SelectedFolderPath);
        if (string.IsNullOrEmpty(repoRoot))
            return;

        StatusBarViewModel.ShowMessage("Pushing...");
        var (success, message) = await _gitService.PushAsync(repoRoot);
        StatusBarViewModel.ShowMessage(message);

        if (success)
        {
            await UpdateGitStatusAsync();
        }
    }

    private async System.Threading.Tasks.Task UpdateGitStatusAsync()
    {
        if (string.IsNullOrEmpty(SelectedFolderPath))
        {
            StatusBarViewModel.ClearGitInfo();
            UpdateWindowTitle(null, null);
            return;
        }

        try
        {
            if (!await _gitService.IsInGitRepositoryAsync(SelectedFolderPath))
            {
                StatusBarViewModel.ClearGitInfo();
                UpdateWindowTitle(SelectedFolderPath, null);
                return;
            }

            var repoRoot = await _gitService.GetRepositoryRootAsync(SelectedFolderPath);
            if (string.IsNullOrEmpty(repoRoot))
            {
                StatusBarViewModel.ClearGitInfo();
                UpdateWindowTitle(SelectedFolderPath, null);
                return;
            }

            var branch = await _gitService.GetCurrentBranchAsync(repoRoot);
            var (ahead, behind) = await _gitService.GetTrackingStatusAsync(repoRoot);

            StatusBarViewModel.UpdateGitInfo(branch, ahead, behind);
            UpdateWindowTitle(repoRoot, branch);
        }
        catch
        {
            StatusBarViewModel.ClearGitInfo();
            UpdateWindowTitle(SelectedFolderPath, null);
        }
    }

    private void UpdateWindowTitle(string? folderPath, string? branch)
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            RepositoryInfo = string.Empty;
            return;
        }

        var folderName = Path.GetFileName(folderPath);
        if (string.IsNullOrEmpty(folderName))
        {
            folderName = folderPath;
        }

        if (!string.IsNullOrEmpty(branch))
        {
            RepositoryInfo = $"{folderName} [{branch}]";
        }
        else
        {
            RepositoryInfo = folderName;
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

    #region Template Management

    /// <summary>
    /// テンプレート一覧を更新
    /// </summary>
    private void RefreshTemplates()
    {
        var templates = _templateService.GetAvailableTemplates();
        Templates.Clear();
        foreach (var template in templates)
        {
            Templates.Add(template);
        }
    }

    /// <summary>
    /// テンプレートファイルを開く
    /// </summary>
    private async void OpenTemplate(TemplateInfo? template)
    {
        if (template == null)
            return;

        await OpenFileAsync(template.FullPath);
    }

    /// <summary>
    /// テンプレートを開けるかチェック
    /// </summary>
    private bool CanOpenTemplate(TemplateInfo? template)
    {
        return template != null && File.Exists(template.FullPath);
    }

    /// <summary>
    /// 新しいテンプレートを作成
    /// </summary>
    private async void NewTemplate()
    {
        // シンプルなダイアログを作成
        var dialog = new System.Windows.Window
        {
            Title = "New Template",
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current.MainWindow,
            ResizeMode = ResizeMode.NoResize
        };

        var stackPanel = new StackPanel { Margin = new Thickness(10) };
        stackPanel.Children.Add(new TextBlock { Text = "Template Name:", Margin = new Thickness(0, 0, 0, 5) });

        var textBox = new TextBox { Margin = new Thickness(0, 0, 0, 10) };
        stackPanel.Children.Add(textBox);

        var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var okButton = new Button { Content = "OK", Width = 75, Margin = new Thickness(0, 0, 5, 0), IsDefault = true };
        var cancelButton = new Button { Content = "Cancel", Width = 75, IsCancel = true };

        bool dialogResult = false;
        okButton.Click += (s, e) => { dialogResult = true; dialog.Close(); };
        cancelButton.Click += (s, e) => { dialog.Close(); };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        stackPanel.Children.Add(buttonPanel);

        dialog.Content = stackPanel;
        textBox.Focus();
        dialog.ShowDialog();

        if (dialogResult && !string.IsNullOrWhiteSpace(textBox.Text))
        {
            try
            {
                var templatePath = _templateService.CreateNewTemplate(textBox.Text);
                RefreshTemplates();
                await OpenFileAsync(templatePath);
                StatusBarViewModel.ShowMessage($"Created template: {textBox.Text}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create template: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// テンプレートを削除
    /// </summary>
    private void DeleteTemplate(TemplateInfo? template)
    {
        if (template == null)
            return;

        // 確認ダイアログ表示
        var result = MessageBox.Show(
            $"Are you sure you want to delete template '{template.DisplayName}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        // テンプレートが現在開いているかチェック
        if (Tabs.Any(t => t.FilePath == template.FullPath))
        {
            MessageBox.Show(
                "Template is currently open. Please close the tab first.",
                "Cannot Delete",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            _templateService.DeleteTemplate(template.FileName);
            RefreshTemplates();
            StatusBarViewModel.ShowMessage($"Deleted template: {template.DisplayName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to delete template: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// テンプレートを削除できるかチェック
    /// </summary>
    private bool CanDeleteTemplate(TemplateInfo? template)
    {
        return template != null && File.Exists(template.FullPath);
    }

    /// <summary>
    /// テンプレートフォルダを開く
    /// </summary>
    private void OpenTemplateFolder()
    {
        try
        {
            var templateDir = _templateService.GetTemplateDirectoryPath();
            if (Directory.Exists(templateDir))
            {
                System.Diagnostics.Process.Start("explorer.exe", $"\"{templateDir}\"");
            }
            else
            {
                MessageBox.Show("Template folder does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open template folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion
}
