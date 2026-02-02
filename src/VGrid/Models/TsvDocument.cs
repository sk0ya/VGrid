using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using VGrid.Services;

namespace VGrid.Models;

/// <summary>
/// Represents a TSV document with all its data
/// </summary>
public class TsvDocument : INotifyPropertyChanged
{
    private string? _filePath;
    private bool _isDirty;
    private int _cachedColumnCount = -1; // -1 indicates cache is invalid
    private DelimiterFormat _delimiterFormat = DelimiterFormat.Tsv;

    /// <summary>
    /// The rows in this document
    /// </summary>
    public ObservableCollection<Row> Rows { get; }

    /// <summary>
    /// The file path of this document (null if not yet saved)
    /// </summary>
    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (_filePath != value)
            {
                _filePath = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// The delimiter format used for this document's file I/O
    /// </summary>
    public DelimiterFormat DelimiterFormat
    {
        get => _delimiterFormat;
        set
        {
            if (_delimiterFormat != value)
            {
                _delimiterFormat = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Indicates whether the document has unsaved changes
    /// </summary>
    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (_isDirty != value)
            {
                _isDirty = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// The number of rows in the document
    /// </summary>
    public int RowCount => Rows.Count;

    /// <summary>
    /// The number of columns (maximum column count across all rows)
    /// Cached for performance - O(n) calculation only when cache is invalidated
    /// </summary>
    public int ColumnCount
    {
        get
        {
            if (_cachedColumnCount < 0)
            {
                _cachedColumnCount = Rows.Count == 0 ? 0 : Rows.Max(r => r.CellCount);
            }
            return _cachedColumnCount;
        }
    }

    /// <summary>
    /// Invalidates the column count cache, forcing recalculation on next access
    /// </summary>
    private void InvalidateColumnCountCache()
    {
        _cachedColumnCount = -1;
    }

    public TsvDocument()
    {
        Rows = new ObservableCollection<Row>();
        Rows.CollectionChanged += Rows_CollectionChanged;
    }

    public TsvDocument(IEnumerable<Row> rows)
    {
        Rows = new ObservableCollection<Row>(rows);
        Rows.CollectionChanged += Rows_CollectionChanged;

        // Subscribe to existing rows and cells
        foreach (var row in Rows)
        {
            SubscribeToRow(row);
        }

        NormalizeColumnCount();
    }

    /// <summary>
    /// Gets the row at the specified index
    /// </summary>
    public Row GetRow(int rowIndex)
    {
        if (rowIndex >= 0 && rowIndex < Rows.Count)
        {
            return Rows[rowIndex];
        }
        throw new ArgumentOutOfRangeException(nameof(rowIndex));
    }

    /// <summary>
    /// Gets the cell at the specified position
    /// </summary>
    public Cell? GetCell(int row, int column)
    {
        if (row >= 0 && row < Rows.Count)
        {
            return Rows[row].GetCell(column);
        }
        return null;
    }

    /// <summary>
    /// Gets the cell at the specified position
    /// </summary>
    public Cell? GetCell(GridPosition position)
    {
        return GetCell(position.Row, position.Column);
    }

    /// <summary>
    /// Sets the value of a cell at the specified position
    /// </summary>
    public void SetCell(int row, int column, string value)
    {
        var cell = GetCell(row, column);
        if (cell != null)
        {
            cell.Value = value;
            IsDirty = true;
        }
    }

    /// <summary>
    /// Sets the value of a cell at the specified position
    /// </summary>
    public void SetCell(GridPosition position, string value)
    {
        SetCell(position.Row, position.Column, value);
    }

    /// <summary>
    /// Inserts a new row at the specified index
    /// </summary>
    public void InsertRow(int index)
    {
        if (index >= 0 && index <= Rows.Count)
        {
            var newRow = new Row(index, ColumnCount);
            Rows.Insert(index, newRow);

            // Update indices of subsequent rows
            for (int i = index + 1; i < Rows.Count; i++)
            {
                Rows[i].Index = i;
            }

            IsDirty = true;
            OnPropertyChanged(nameof(RowCount));
        }
    }

    /// <summary>
    /// Deletes the row at the specified index
    /// </summary>
    public void DeleteRow(int index)
    {
        if (index >= 0 && index < Rows.Count)
        {
            Rows.RemoveAt(index);

            // Update indices of subsequent rows
            for (int i = index; i < Rows.Count; i++)
            {
                Rows[i].Index = i;
            }

            IsDirty = true;
            OnPropertyChanged(nameof(RowCount));
        }
    }

    /// <summary>
    /// Inserts a new column at the specified index
    /// </summary>
    public void InsertColumn(int index)
    {
        if (index >= 0 && index <= ColumnCount)
        {
            foreach (var row in Rows)
            {
                row.InsertCell(index);
            }

            InvalidateColumnCountCache();
            IsDirty = true;
            OnPropertyChanged(nameof(ColumnCount));
        }
    }

    /// <summary>
    /// Deletes the column at the specified index
    /// </summary>
    public void DeleteColumn(int index)
    {
        if (index >= 0 && index < ColumnCount)
        {
            foreach (var row in Rows)
            {
                row.RemoveCell(index);
            }

            InvalidateColumnCountCache();
            IsDirty = true;
            OnPropertyChanged(nameof(ColumnCount));
        }
    }

    /// <summary>
    /// Sorts the rows by the specified column
    /// </summary>
    public void SortByColumn(int columnIndex, bool ascending = true)
    {
        if (columnIndex < 0 || columnIndex >= ColumnCount)
            return;

        var sortedRows = ascending
            ? Rows.OrderBy(r => r.GetCell(columnIndex)?.Value ?? string.Empty).ToList()
            : Rows.OrderByDescending(r => r.GetCell(columnIndex)?.Value ?? string.Empty).ToList();

        Rows.Clear();
        for (int i = 0; i < sortedRows.Count; i++)
        {
            sortedRows[i].Index = i;
            Rows.Add(sortedRows[i]);
        }

        IsDirty = true;
    }

    /// <summary>
    /// Ensures all rows have the same number of columns (padding with empty cells if needed)
    /// </summary>
    public void NormalizeColumnCount()
    {
        InvalidateColumnCountCache();
        var maxColumns = ColumnCount;
        foreach (var row in Rows)
        {
            row.EnsureCellCount(maxColumns);
        }
    }

    /// <summary>
    /// Creates a new empty document with minimal initial dimensions
    /// The grid will expand as needed when user navigates or types
    /// </summary>
    public static TsvDocument CreateEmpty()
    {
        var doc = new TsvDocument();
        // Start with 40 rows, 20 columns - balanced for usability and performance
        for (int i = 0; i < 40; i++)
        {
            doc.Rows.Add(new Row(i, 20));
        }
        return doc;
    }

    /// <summary>
    /// Ensures the document has at least the specified number of rows and columns
    /// </summary>
    public void EnsureSize(int minRows, int minColumns)
    {
        InvalidateColumnCountCache();

        // Ensure minimum columns for all existing rows
        foreach (var row in Rows)
        {
            row.EnsureCellCount(minColumns);
        }

        // Add rows if needed
        while (Rows.Count < minRows)
        {
            Rows.Add(new Row(Rows.Count, minColumns));
        }
    }

    /// <summary>
    /// Finds all cells matching the specified pattern
    /// </summary>
    /// <param name="pattern">The search pattern</param>
    /// <param name="isRegex">Whether to treat the pattern as a regular expression</param>
    /// <param name="isCaseSensitive">Whether to perform case-sensitive matching (default: false for backward compatibility)</param>
    /// <returns>List of grid positions where matches were found</returns>
    public List<GridPosition> FindMatches(string pattern, bool isRegex, bool isCaseSensitive = false)
    {
        var results = new List<GridPosition>();

        if (string.IsNullOrEmpty(pattern))
            return results;

        if (isRegex)
        {
            try
            {
                var options = isCaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                var regex = new Regex(pattern, options);

                for (int r = 0; r < Rows.Count; r++)
                {
                    var row = Rows[r];
                    for (int c = 0; c < row.Cells.Count; c++)
                    {
                        if (regex.IsMatch(row.Cells[c].Value))
                        {
                            results.Add(new GridPosition(r, c));
                        }
                    }
                }
            }
            catch (ArgumentException)
            {
                // Invalid regex - return empty results
                return results;
            }
        }
        else
        {
            // Plain string search with configurable case sensitivity
            var comparison = isCaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            for (int r = 0; r < Rows.Count; r++)
            {
                var row = Rows[r];
                for (int c = 0; c < row.Cells.Count; c++)
                {
                    if (row.Cells[c].Value.Contains(pattern, comparison))
                    {
                        results.Add(new GridPosition(r, c));
                    }
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Subscribes to a row's cells collection and all existing cells
    /// </summary>
    private void SubscribeToRow(Row row)
    {
        // Subscribe to the row's cells collection
        row.Cells.CollectionChanged += Cells_CollectionChanged;

        // Subscribe to all existing cells in the row
        foreach (var cell in row.Cells)
        {
            SubscribeToCell(cell);
        }
    }

    /// <summary>
    /// Subscribes to a cell's property changed event
    /// </summary>
    private void SubscribeToCell(Cell cell)
    {
        cell.PropertyChanged += Cell_PropertyChanged;
    }

    /// <summary>
    /// Handles when rows are added or removed from the document
    /// </summary>
    private void Rows_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Invalidate column count cache when rows change
        InvalidateColumnCountCache();

        // Subscribe to newly added rows
        if (e.NewItems != null)
        {
            foreach (Row row in e.NewItems)
            {
                SubscribeToRow(row);
            }
        }
    }

    /// <summary>
    /// Handles when cells are added or removed from a row
    /// </summary>
    private void Cells_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Invalidate column count cache when cells change
        InvalidateColumnCountCache();

        // Subscribe to newly added cells
        if (e.NewItems != null)
        {
            foreach (Cell cell in e.NewItems)
            {
                SubscribeToCell(cell);
            }
        }
    }

    /// <summary>
    /// Handles when a cell's property changes
    /// </summary>
    private void Cell_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // When a cell's Value changes, mark the document as dirty
        if (e.PropertyName == nameof(Cell.Value))
        {
            IsDirty = true;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
