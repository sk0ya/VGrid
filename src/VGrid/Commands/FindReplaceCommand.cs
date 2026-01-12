using VGrid.Models;

namespace VGrid.Commands;

public class FindReplaceCommand : ICommand
{
    private readonly TsvDocument _document;
    private readonly GridPosition _position;
    private readonly string _oldValue;
    private readonly string _newValue;

    public string Description => $"Replace at ({_position.Row}, {_position.Column})";

    public FindReplaceCommand(TsvDocument document, GridPosition position, string oldValue, string newValue)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _position = position;
        _oldValue = oldValue ?? string.Empty;
        _newValue = newValue ?? string.Empty;
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
