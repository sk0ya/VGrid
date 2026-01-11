using VGrid.Models;

namespace VGrid.Commands;

/// <summary>
/// Command for editing multiple cells with different values for each cell
/// </summary>
public class BulkEditCellsWithValuesCommand : ICommand
{
    private readonly TsvDocument _document;
    private readonly Dictionary<GridPosition, string> _newValues;
    private readonly Dictionary<GridPosition, string> _oldValues;

    public string Description => $"Bulk edit {_newValues.Count} cells with individual values";

    public BulkEditCellsWithValuesCommand(TsvDocument document, Dictionary<GridPosition, string> newValues)
    {
        _document = document;
        _newValues = newValues;
        _oldValues = new Dictionary<GridPosition, string>();

        // Store old values for undo
        foreach (var kvp in newValues)
        {
            var cell = document.GetCell(kvp.Key);
            _oldValues[kvp.Key] = cell?.Value ?? string.Empty;
        }
    }

    public void Execute()
    {
        foreach (var kvp in _newValues)
        {
            _document.SetCell(kvp.Key, kvp.Value);
        }
    }

    public void Undo()
    {
        foreach (var kvp in _oldValues)
        {
            _document.SetCell(kvp.Key, kvp.Value);
        }
    }
}
