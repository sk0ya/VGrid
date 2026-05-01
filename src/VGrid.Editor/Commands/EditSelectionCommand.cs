using VGrid.Models;
using VimSelectionRange = VGrid.VimEngine.SelectionRange;

namespace VGrid.Commands;

/// <summary>
/// Command to edit (set) all selected cells to the same value with undo support
/// </summary>
public class EditSelectionCommand : ICommand
{
    private readonly TsvDocument _document;
    private readonly List<(int row, int col, string oldValue)> _oldValues;
    private readonly string _newValue;

    public string Description => $"Edit selection ({_oldValues.Count} cells)";

    public EditSelectionCommand(TsvDocument document, VimSelectionRange selection, string newValue)
    {
        _document = document;
        _newValue = newValue;
        _oldValues = new List<(int, int, string)>();

        // Collect all selected cells and their current values
        for (int row = selection.StartRow; row <= selection.EndRow; row++)
        {
            if (row >= document.RowCount)
                break;

            var rowObj = document.Rows[row];

            for (int col = selection.StartColumn; col <= selection.EndColumn; col++)
            {
                if (col >= rowObj.Cells.Count)
                    break;

                var cell = rowObj.Cells[col];
                _oldValues.Add((row, col, cell.Value));
            }
        }
    }

    public void Execute()
    {
        // Set all selected cells to the new value
        foreach (var (row, col, _) in _oldValues)
        {
            if (row < _document.RowCount && col < _document.Rows[row].Cells.Count)
            {
                _document.Rows[row].Cells[col].Value = _newValue;
            }
        }
    }

    public void Undo()
    {
        // Restore all old values
        foreach (var (row, col, oldValue) in _oldValues)
        {
            if (row < _document.RowCount && col < _document.Rows[row].Cells.Count)
            {
                _document.Rows[row].Cells[col].Value = oldValue;
            }
        }
    }
}
