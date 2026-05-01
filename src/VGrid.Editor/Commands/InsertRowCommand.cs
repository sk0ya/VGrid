using VGrid.Models;

namespace VGrid.Commands;

/// <summary>
/// Command to insert a new row with undo support
/// </summary>
public class InsertRowCommand : ICommand
{
    private readonly TsvDocument _document;
    private readonly int _rowIndex;

    public string Description => $"Insert row at {_rowIndex}";

    public InsertRowCommand(TsvDocument document, int rowIndex)
    {
        _document = document;
        _rowIndex = rowIndex;
    }

    public void Execute()
    {
        if (_rowIndex >= 0 && _rowIndex <= _document.RowCount)
        {
            // Insert a new empty row at the specified index
            _document.InsertRow(_rowIndex);
        }
    }

    public void Undo()
    {
        if (_rowIndex >= 0 && _rowIndex < _document.RowCount)
        {
            // Delete the row that was inserted
            _document.DeleteRow(_rowIndex);
        }
    }
}
