using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VGrid.Models;

/// <summary>
/// Row for diff display with status tracking
/// </summary>
public class DiffRow : INotifyPropertyChanged
{
    private int _index;
    private DiffStatus _rowStatus;

    public ObservableCollection<DiffCell> Cells { get; }

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

    public DiffStatus RowStatus
    {
        get => _rowStatus;
        set
        {
            if (_rowStatus != value)
            {
                _rowStatus = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Creates a new DiffRow with empty cells
    /// </summary>
    public DiffRow(int index, int columnCount)
    {
        Index = index;
        Cells = new ObservableCollection<DiffCell>();

        for (int i = 0; i < columnCount; i++)
        {
            Cells.Add(new DiffCell());
        }

        RowStatus = DiffStatus.Unchanged;
    }

    /// <summary>
    /// Creates a new DiffRow with values
    /// </summary>
    public DiffRow(int index, IEnumerable<string> values, DiffStatus status = DiffStatus.Unchanged)
    {
        Index = index;
        Cells = new ObservableCollection<DiffCell>(
            values.Select(v => new DiffCell { Value = v, Status = status })
        );
        RowStatus = status;
    }
}
