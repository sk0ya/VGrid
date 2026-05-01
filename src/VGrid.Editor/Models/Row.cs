using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VGrid.Models;

/// <summary>
/// Represents a single row in the TSV grid
/// </summary>
public class Row : INotifyPropertyChanged
{
    private int _index;

    /// <summary>
    /// The cells in this row
    /// </summary>
    public ObservableCollection<Cell> Cells { get; }

    /// <summary>
    /// The index of this row in the document
    /// </summary>
    public int Index
    {
        get => _index;
        set
        {
            if (_index != value)
            {
                _index = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// The number of cells in this row
    /// </summary>
    public int CellCount => Cells.Count;

    public Row(int index, int columnCount = 0)
    {
        _index = index;
        Cells = new ObservableCollection<Cell>();

        // Initialize with empty cells
        for (int i = 0; i < columnCount; i++)
        {
            Cells.Add(new Cell());
        }
    }

    public Row(int index, IEnumerable<string> values)
    {
        _index = index;
        Cells = new ObservableCollection<Cell>(
            values.Select(v => new Cell { Value = v })
        );
    }

    /// <summary>
    /// Gets the cell at the specified column index
    /// </summary>
    public Cell? GetCell(int columnIndex)
    {
        if (columnIndex >= 0 && columnIndex < Cells.Count)
        {
            return Cells[columnIndex];
        }
        return null;
    }

    /// <summary>
    /// Inserts a new empty cell at the specified index
    /// </summary>
    public void InsertCell(int index)
    {
        if (index >= 0 && index <= Cells.Count)
        {
            Cells.Insert(index, new Cell());
        }
    }

    /// <summary>
    /// Removes the cell at the specified index
    /// </summary>
    public void RemoveCell(int index)
    {
        if (index >= 0 && index < Cells.Count)
        {
            Cells.RemoveAt(index);
        }
    }

    /// <summary>
    /// Ensures this row has at least the specified number of cells
    /// </summary>
    public void EnsureCellCount(int count)
    {
        while (Cells.Count < count)
        {
            Cells.Add(new Cell());
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
