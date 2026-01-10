using System.Collections.ObjectModel;
using VGrid.Commands;
using VGrid.Helpers;
using VGrid.Models;

namespace VGrid.ViewModels;

/// <summary>
/// ViewModel for the TSV grid
/// </summary>
public class TsvGridViewModel : ViewModelBase
{
    private TsvDocument _document;
    private GridPosition _cursorPosition = new(0, 0);
    private string _searchPattern = string.Empty;
    private readonly List<GridPosition> _searchResults = new();
    private readonly CommandHistory _commandHistory;

    public TsvGridViewModel(CommandHistory commandHistory)
    {
        _commandHistory = commandHistory;
        _document = TsvDocument.CreateEmpty();
    }

    /// <summary>
    /// The TSV document
    /// </summary>
    public TsvDocument Document
    {
        get => _document;
        set
        {
            if (SetProperty(ref _document, value))
            {
                OnPropertyChanged(nameof(Rows));
                OnPropertyChanged(nameof(RowCount));
                OnPropertyChanged(nameof(ColumnCount));
            }
        }
    }

    /// <summary>
    /// The rows in the document (for data binding)
    /// </summary>
    public ObservableCollection<Row> Rows => _document.Rows;

    /// <summary>
    /// The current cursor position
    /// </summary>
    public GridPosition CursorPosition
    {
        get => _cursorPosition;
        set => SetProperty(ref _cursorPosition, value);
    }

    /// <summary>
    /// The number of rows
    /// </summary>
    public int RowCount => _document.RowCount;

    /// <summary>
    /// The number of columns
    /// </summary>
    public int ColumnCount => _document.ColumnCount;

    /// <summary>
    /// Search pattern for find/replace
    /// </summary>
    public string SearchPattern
    {
        get => _searchPattern;
        set => SetProperty(ref _searchPattern, value);
    }

    /// <summary>
    /// Search results
    /// </summary>
    public IReadOnlyList<GridPosition> SearchResults => _searchResults.AsReadOnly();

    /// <summary>
    /// Moves the cursor in the specified direction
    /// </summary>
    public void MoveCursor(GridPosition newPosition)
    {
        CursorPosition = newPosition.Clamp(_document);
    }

    /// <summary>
    /// Edits a cell at the specified position
    /// </summary>
    public void EditCell(GridPosition position, string value)
    {
        var command = new EditCellCommand(_document, position, value);
        _commandHistory.Execute(command);
        OnPropertyChanged(nameof(Document));
    }

    /// <summary>
    /// Sorts the grid by the specified column
    /// </summary>
    public void Sort(int columnIndex, bool ascending = true)
    {
        _document.SortByColumn(columnIndex, ascending);
        OnPropertyChanged(nameof(Rows));
    }

    /// <summary>
    /// Loads a new document
    /// </summary>
    public void LoadDocument(TsvDocument document)
    {
        Document = document;
        CursorPosition = new GridPosition(0, 0);
        _searchResults.Clear();
        _commandHistory.Clear();
    }

    /// <summary>
    /// Creates a new empty document
    /// </summary>
    public void NewDocument()
    {
        LoadDocument(TsvDocument.CreateEmpty());
    }
}
