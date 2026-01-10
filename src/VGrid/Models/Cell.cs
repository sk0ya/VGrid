using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VGrid.Models;

/// <summary>
/// Represents a single cell in the TSV grid
/// </summary>
public class Cell : INotifyPropertyChanged
{
    private string _value = string.Empty;
    private bool _isSelected;
    private bool _isEditing;
    private bool _isSearchMatch;

    /// <summary>
    /// The text content of the cell
    /// </summary>
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

    /// <summary>
    /// Indicates whether this cell is currently selected (e.g., in Visual mode)
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Indicates whether this cell is currently being edited
    /// </summary>
    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (_isEditing != value)
            {
                _isEditing = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Indicates whether this cell is the current search match
    /// </summary>
    public bool IsSearchMatch
    {
        get => _isSearchMatch;
        set
        {
            if (_isSearchMatch != value)
            {
                _isSearchMatch = value;
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
