using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VGrid.Models;

/// <summary>
/// Cell with diff status for visual comparison
/// </summary>
public class DiffCell : INotifyPropertyChanged
{
    private string _value = string.Empty;
    private DiffStatus _status = DiffStatus.Unchanged;

    public string Value
    {
        get => _value;
        set
        {
            if (_value != value)
            {
                _value = value;
                OnPropertyChanged();
            }
        }
    }

    public DiffStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Status of a cell in diff view
/// </summary>
public enum DiffStatus
{
    /// <summary>
    /// Cell is unchanged (white background)
    /// </summary>
    Unchanged,

    /// <summary>
    /// Cell content has been modified (yellow background)
    /// </summary>
    Modified,

    /// <summary>
    /// Cell/Row has been added (green background)
    /// </summary>
    Added,

    /// <summary>
    /// Cell/Row has been deleted (red background)
    /// </summary>
    Deleted
}
