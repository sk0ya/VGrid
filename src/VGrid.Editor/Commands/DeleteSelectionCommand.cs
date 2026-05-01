using VGrid.Models;
using VimSelectionRange = VGrid.VimEngine.SelectionRange;

namespace VGrid.Commands;

/// <summary>
/// Command to delete (clear) selected cells with undo support
/// </summary>
public class DeleteSelectionCommand : ICommand
{
    private readonly TsvDocument _document;
    private readonly List<(int row, int col, string oldValue)> _oldValues;

    public string Description => $"Delete selection ({_oldValues.Count} cells)";

    public DeleteSelectionCommand(TsvDocument document, VimSelectionRange selection)
    {
        _document = document;
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
        // Clear all selected cells
        foreach (var (row, col, _) in _oldValues)
        {
            if (row < _document.RowCount && col < _document.Rows[row].Cells.Count)
            {
                _document.Rows[row].Cells[col].Value = string.Empty;
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
