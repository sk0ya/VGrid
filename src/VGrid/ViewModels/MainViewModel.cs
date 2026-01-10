using System.IO;
using System.Windows;
using System.Windows.Input;
using VGrid.Helpers;
using VGrid.Models;
using VGrid.Services;
using VGrid.VimEngine;
using WpfCommand = System.Windows.Input.ICommand;
using CommandHistory = VGrid.Commands.CommandHistory;

namespace VGrid.ViewModels;

/// <summary>
/// Main ViewModel for the application
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly ITsvFileService _fileService;
    private readonly CommandHistory _commandHistory;
    private string? _currentFilePath;

    public MainViewModel()
    {
        _fileService = new TsvFileService();
        _commandHistory = new CommandHistory();

        GridViewModel = new TsvGridViewModel(_commandHistory);
        StatusBarViewModel = new StatusBarViewModel();
        VimState = new VimState();

        // Initialize commands
        NewFileCommand = new RelayCommand(NewFile);
        OpenFileCommand = new RelayCommand(OpenFile);
        SaveFileCommand = new RelayCommand(SaveFile, CanSaveFile);
        SaveAsFileCommand = new RelayCommand(SaveFileAs);
        ExitCommand = new RelayCommand(Exit);
        UndoCommand = new RelayCommand(Undo, CanUndo);
        RedoCommand = new RelayCommand(Redo, CanRedo);

        // Subscribe to Vim state changes
        VimState.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(VimState.CurrentMode))
            {
                StatusBarViewModel.UpdateMode(VimState.CurrentMode);
            }
            else if (e.PropertyName == nameof(VimState.CursorPosition))
            {
                StatusBarViewModel.UpdatePosition(VimState.CursorPosition.Row, VimState.CursorPosition.Column);
                GridViewModel.CursorPosition = VimState.CursorPosition;
            }
        };

        // Initialize status bar
        StatusBarViewModel.UpdateMode(VimMode.Normal);
        StatusBarViewModel.UpdatePosition(0, 0);
    }

    public TsvGridViewModel GridViewModel { get; }
    public StatusBarViewModel StatusBarViewModel { get; }
    public VimState VimState { get; }

    public WpfCommand NewFileCommand { get; }
    public WpfCommand OpenFileCommand { get; }
    public WpfCommand SaveFileCommand { get; }
    public WpfCommand SaveAsFileCommand { get; }
    public WpfCommand ExitCommand { get; }
    public WpfCommand UndoCommand { get; }
    public WpfCommand RedoCommand { get; }

    public string? CurrentFilePath
    {
        get => _currentFilePath;
        private set => SetProperty(ref _currentFilePath, value);
    }

    public string WindowTitle
    {
        get
        {
            var fileName = string.IsNullOrEmpty(CurrentFilePath)
                ? "Untitled"
                : Path.GetFileName(CurrentFilePath);

            var isDirty = GridViewModel.Document.IsDirty ? "*" : "";
            return $"{fileName}{isDirty} - VGrid";
        }
    }

    private void NewFile()
    {
        if (GridViewModel.Document.IsDirty)
        {
            var result = MessageBox.Show(
                "Do you want to save changes?",
                "VGrid",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
                return;

            if (result == MessageBoxResult.Yes)
            {
                SaveFile();
            }
        }

        GridViewModel.NewDocument();
        CurrentFilePath = null;
        OnPropertyChanged(nameof(WindowTitle));
        StatusBarViewModel.ShowMessage("New file created");
    }

    private async void OpenFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "TSV Files (*.tsv)|*.tsv|Text Files (*.txt)|*.txt|Tab-separated Files (*.tab)|*.tab|All Files (*.*)|*.*",
            Title = "Open TSV File"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var document = await _fileService.LoadAsync(dialog.FileName);
                GridViewModel.LoadDocument(document);
                CurrentFilePath = dialog.FileName;
                OnPropertyChanged(nameof(WindowTitle));
                StatusBarViewModel.ShowMessage($"Opened: {dialog.FileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private bool CanSaveFile()
    {
        return !string.IsNullOrEmpty(CurrentFilePath);
    }

    private async void SaveFile()
    {
        if (string.IsNullOrEmpty(CurrentFilePath))
        {
            SaveFileAs();
            return;
        }

        try
        {
            await _fileService.SaveAsync(GridViewModel.Document, CurrentFilePath);
            OnPropertyChanged(nameof(WindowTitle));
            StatusBarViewModel.ShowMessage($"Saved: {CurrentFilePath}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SaveFileAs()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "TSV Files (*.tsv)|*.tsv|Text Files (*.txt)|*.txt|Tab-separated Files (*.tab)|*.tab|All Files (*.*)|*.*",
            Title = "Save TSV File",
            DefaultExt = ".tsv"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _fileService.SaveAsync(GridViewModel.Document, dialog.FileName);
                CurrentFilePath = dialog.FileName;
                OnPropertyChanged(nameof(WindowTitle));
                StatusBarViewModel.ShowMessage($"Saved: {dialog.FileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Exit()
    {
        Application.Current.Shutdown();
    }

    private bool CanUndo()
    {
        return _commandHistory.CanUndo;
    }

    private void Undo()
    {
        if (_commandHistory.CanUndo)
        {
            _commandHistory.Undo();
            StatusBarViewModel.ShowMessage("Undo");
        }
    }

    private bool CanRedo()
    {
        return _commandHistory.CanRedo;
    }

    private void Redo()
    {
        if (_commandHistory.CanRedo)
        {
            _commandHistory.Redo();
            StatusBarViewModel.ShowMessage("Redo");
        }
    }
}
