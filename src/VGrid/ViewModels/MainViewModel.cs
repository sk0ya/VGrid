using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
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
    private TabItemViewModel? _selectedTab;
    private string? _selectedFolderPath;

    public MainViewModel()
    {
        _fileService = new TsvFileService();

        Tabs = new ObservableCollection<TabItemViewModel>();
        StatusBarViewModel = new StatusBarViewModel();

        // Initialize commands
        NewFileCommand = new RelayCommand(NewFile);
        OpenFileCommand = new RelayCommand(OpenFile);
        OpenFolderCommand = new RelayCommand(OpenFolder);
        SaveFileCommand = new RelayCommand(SaveFile, CanSaveFile);
        SaveAsFileCommand = new RelayCommand(SaveFileAs);
        CloseTabCommand = new RelayCommand<TabItemViewModel>(CloseTab);
        ExitCommand = new RelayCommand(Exit);

        // Initialize with one empty tab
        NewFile();
    }

    public ObservableCollection<TabItemViewModel> Tabs { get; }
    public StatusBarViewModel StatusBarViewModel { get; }

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

    public WpfCommand NewFileCommand { get; }
    public WpfCommand OpenFileCommand { get; }
    public WpfCommand OpenFolderCommand { get; }
    public WpfCommand SaveFileCommand { get; }
    public WpfCommand SaveAsFileCommand { get; }
    public WpfCommand CloseTabCommand { get; }
    public WpfCommand ExitCommand { get; }

    public string WindowTitle => "VGrid - TSV Editor with Vim Keybindings";

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
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select folder to explore",
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            SelectedFolderPath = dialog.SelectedPath;
        }
    }

    private bool CanSaveFile()
    {
        return SelectedTab != null && !string.IsNullOrEmpty(SelectedTab.FilePath) &&
               !SelectedTab.FilePath.StartsWith("Untitled");
    }

    private async void SaveFile()
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

    private void CloseTab(TabItemViewModel? tab)
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
}
