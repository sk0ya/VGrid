using System.Collections.Generic;
using System.ComponentModel;
using VGrid.Helpers;
using VGrid.Models;
using VGrid.VimEngine;

namespace VGrid.ViewModels;

/// <summary>
/// ViewModel for a single tab (open TSV file)
/// </summary>
public class TabItemViewModel : ViewModelBase
{
    private string _header;
    private bool _isDirty;
    private Dictionary<int, double> _columnWidths = new Dictionary<int, double>();
    private HashSet<int> _manuallyResizedColumns = new HashSet<int>();
    private string _selectedCellContent = string.Empty;
    private string _positionText = "1:1";

    public TabItemViewModel(string filePath, TsvDocument document, VimState vimState, TsvGridViewModel gridViewModel,
        Func<string?>? getFolderPath = null, Action<string>? openFileAction = null)
    {
        FilePath = filePath;
        Document = document;
        VimState = vimState;
        GridViewModel = gridViewModel;
        _header = System.IO.Path.GetFileName(filePath) ?? "Untitled";

        // Initialize FindReplaceViewModel
        if (vimState.CommandHistory != null)
        {
            FindReplaceViewModel = new FindReplaceViewModel(document, vimState, vimState.CommandHistory);

            // Subscribe to VimSearchActivated event to close FindReplace panel (without clearing Vim search highlight)
            vimState.VimSearchActivated += (s, e) => FindReplaceViewModel.Close(clearHighlighting: false);
        }
        else
        {
            throw new ArgumentException("VimState must have CommandHistory initialized", nameof(vimState));
        }

        // Initialize CommandPaletteViewModel
        // Use folder path or fall back to current file's directory
        Func<string?> effectiveGetFolderPath = () =>
        {
            var folderPath = getFolderPath?.Invoke();
            if (!string.IsNullOrEmpty(folderPath))
                return folderPath;

            // Fall back to current file's directory
            if (!string.IsNullOrEmpty(FilePath) && System.IO.File.Exists(FilePath))
                return System.IO.Path.GetDirectoryName(FilePath);

            return null;
        };

        CommandPaletteViewModel = new CommandPaletteViewModel(
            document,
            vimState,
            effectiveGetFolderPath,
            openFileAction);

        // Subscribe to document changes
        document.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TsvDocument.IsDirty))
            {
                IsDirty = document.IsDirty;
            }
        };

        // Subscribe to cursor position changes to update selected cell content
        vimState.PropertyChanged += VimState_PropertyChanged;

        // Initialize selected cell content
        UpdateSelectedCellContent();
    }

    private void VimState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VimState.CursorPosition))
        {
            UpdateSelectedCellContent();
        }
    }

    private void UpdateSelectedCellContent()
    {
        var pos = VimState.CursorPosition;
        PositionText = $"{pos.Row + 1}:{pos.Column + 1}";
        if (pos.Row >= 0 && pos.Row < Document.RowCount &&
            pos.Column >= 0 && pos.Column < Document.ColumnCount)
        {
            SelectedCellContent = Document.Rows[pos.Row].Cells[pos.Column].Value;
        }
        else
        {
            SelectedCellContent = string.Empty;
        }
    }

    /// <summary>
    /// The content of the currently selected cell
    /// </summary>
    public string SelectedCellContent
    {
        get => _selectedCellContent;
        set => SetProperty(ref _selectedCellContent, value);
    }

    /// <summary>
    /// The position text (row:column) of the currently selected cell
    /// </summary>
    public string PositionText
    {
        get => _positionText;
        private set => SetProperty(ref _positionText, value);
    }

    /// <summary>
    /// Refreshes the selected cell content (call when cell value changes)
    /// </summary>
    public void RefreshSelectedCellContent()
    {
        UpdateSelectedCellContent();
    }

    public string FilePath { get; set; }
    public TsvDocument Document { get; }
    public VimState VimState { get; }
    public TsvGridViewModel GridViewModel { get; }
    public FindReplaceViewModel FindReplaceViewModel { get; }
    public CommandPaletteViewModel CommandPaletteViewModel { get; }

    public string Header
    {
        get => _header;
        set => SetProperty(ref _header, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (SetProperty(ref _isDirty, value))
            {
                // Update header to show dirty state
                var fileName = System.IO.Path.GetFileName(FilePath) ?? "Untitled";
                Header = value ? $"{fileName}*" : fileName;
            }
        }
    }

    /// <summary>
    /// Dictionary of column indices to their calculated widths
    /// </summary>
    public Dictionary<int, double> ColumnWidths
    {
        get => _columnWidths;
        set => SetProperty(ref _columnWidths, value);
    }

    /// <summary>
    /// Set of column indices that were manually resized by the user
    /// </summary>
    public HashSet<int> ManuallyResizedColumns => _manuallyResizedColumns;

    /// <summary>
    /// Marks a column as manually resized
    /// </summary>
    public void MarkColumnAsManuallyResized(int columnIndex)
    {
        _manuallyResizedColumns.Add(columnIndex);
    }

    /// <summary>
    /// Resets manual resize tracking (call on file load)
    /// </summary>
    public void ResetManualResizeTracking()
    {
        _manuallyResizedColumns.Clear();
    }
}
