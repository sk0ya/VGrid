using VGrid.Models;

namespace VGrid.Commands;

/// <summary>
/// Command for editing a cell's value
/// </summary>
public class EditCellCommand : ICommand
{
    private readonly TsvDocument _document;
    private readonly GridPosition _position;
    private readonly string _newValue;
    private readonly string _oldValue;

    public string Description => $"Edit cell at ({_position.Row}, {_position.Column})";

    public EditCellCommand(TsvDocument document, GridPosition position, string newValue)
    {
        _document = document;
        _position = position;
        _newValue = newValue;

        // Store the old value for undo
        var cell = document.GetCell(position);
        _oldValue = cell?.Value ?? string.Empty;
    }

    /// <summary>
    /// Constructor with explicit old value (for when the change has already been applied by data binding)
    /// </summary>
    public EditCellCommand(TsvDocument document, GridPosition position, string newValue, string oldValue)
    {
        _document = document;
        _position = position;
        _newValue = newValue;
        _oldValue = oldValue;
    }

    public void Execute()
    {
        _document.SetCell(_position, _newValue);
    }

    public void Undo()
    {
        _document.SetCell(_position, _oldValue);
    }
}
