using VGrid.Models;

namespace VGrid.Commands;

public class BulkFindReplaceCommand : ICommand
{
    private readonly TsvDocument _document;
    private readonly Dictionary<GridPosition, (string oldValue, string newValue)> _replacements;

    public string Description => $"Replace {_replacements.Count} matches";

    public BulkFindReplaceCommand(TsvDocument document, Dictionary<GridPosition, (string oldValue, string newValue)> replacements)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _replacements = replacements ?? throw new ArgumentNullException(nameof(replacements));
    }

    public void Execute()
    {
        foreach (var kvp in _replacements)
        {
            _document.SetCell(kvp.Key, kvp.Value.newValue);
        }
    }

    public void Undo()
    {
        foreach (var kvp in _replacements)
        {
            _document.SetCell(kvp.Key, kvp.Value.oldValue);
        }
    }
}
