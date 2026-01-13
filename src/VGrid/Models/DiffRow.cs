using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VGrid.Models;

/// <summary>
/// Row for diff display with status tracking
/// </summary>
public class DiffRow : INotifyPropertyChanged
{
    private int? _leftLineNumber;
    private int? _rightLineNumber;
    private DiffStatus _rowStatus;

    public ObservableCollection<DiffCell> Cells { get; }

    /// <summary>
    /// Line number in the left (old) file, or null if this row was added
    /// </summary>
    public int? LeftLineNumber
    {
        get => _leftLineNumber;
        set
        {
            if (_leftLineNumber != value)
            {
                _leftLineNumber = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Line number in the right (new) file, or null if this row was deleted
    /// </summary>
    public int? RightLineNumber
    {
        get => _rightLineNumber;
        set
        {
            if (_rightLineNumber != value)
            {
                _rightLineNumber = value;
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
    public DiffRow(int? leftLineNumber, int? rightLineNumber, int columnCount, DiffStatus status = DiffStatus.Unchanged)
    {
        LeftLineNumber = leftLineNumber;
        RightLineNumber = rightLineNumber;
        Cells = new ObservableCollection<DiffCell>();

        for (int i = 0; i < columnCount; i++)
        {
            Cells.Add(new DiffCell());
        }

        RowStatus = status;
    }

    /// <summary>
    /// Creates a new DiffRow with values
    /// </summary>
    public DiffRow(int? leftLineNumber, int? rightLineNumber, IEnumerable<string> values, DiffStatus status = DiffStatus.Unchanged)
    {
        LeftLineNumber = leftLineNumber;
        RightLineNumber = rightLineNumber;
        Cells = new ObservableCollection<DiffCell>(
            values.Select(v => new DiffCell { Value = v, Status = status })
        );
        RowStatus = status;
    }
}
