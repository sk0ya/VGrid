using VGrid.Models;

namespace VGrid.Commands;

/// <summary>
/// Command for editing multiple cells with the same value
/// </summary>
public class BulkEditCellsCommand : ICommand
{
    private readonly TsvDocument _document;
    private readonly List<GridPosition> _positions;
    private readonly string _newValue;
    private readonly Dictionary<GridPosition, string> _oldValues;

    public string Description => $"Bulk edit {_positions.Count} cells";

    public BulkEditCellsCommand(TsvDocument document, List<GridPosition> positions, string newValue)
    {
        _document = document;
        _positions = positions;
        _newValue = newValue;
        _oldValues = new Dictionary<GridPosition, string>();

        // Store old values for undo
        foreach (var position in positions)
        {
            var cell = document.GetCell(position);
            _oldValues[position] = cell?.Value ?? string.Empty;
        }
    }

    public void Execute()
    {
        foreach (var position in _positions)
        {
            _document.SetCell(position, _newValue);
        }
    }

    public void Undo()
    {
        foreach (var position in _positions)
        {
            if (_oldValues.TryGetValue(position, out var oldValue))
            {
                _document.SetCell(position, oldValue);
            }
        }
    }
}
