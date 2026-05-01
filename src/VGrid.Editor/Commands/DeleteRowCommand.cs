using VGrid.Models;

namespace VGrid.Commands;

/// <summary>
/// Command to delete entire rows with undo support
/// </summary>
public class DeleteRowCommand : ICommand
{
    private readonly TsvDocument _document;
    private readonly int _rowIndex;
    private Row? _deletedRow;

    public string Description => $"Delete row {_rowIndex}";

    public DeleteRowCommand(TsvDocument document, int rowIndex)
    {
        _document = document;
        _rowIndex = rowIndex;
    }

    public void Execute()
    {
        if (_rowIndex >= 0 && _rowIndex < _document.RowCount)
        {
            // Store the row before deleting
            _deletedRow = _document.Rows[_rowIndex];

            // Create a copy of the row data
            var copiedRow = new Row(_deletedRow.Index, _deletedRow.Cells.Count);
            for (int i = 0; i < _deletedRow.Cells.Count; i++)
            {
                copiedRow.Cells[i].Value = _deletedRow.Cells[i].Value;
            }
            _deletedRow = copiedRow;

            // Delete the row
            _document.DeleteRow(_rowIndex);
        }
    }

    public void Undo()
    {
        if (_deletedRow != null && _rowIndex >= 0 && _rowIndex <= _document.RowCount)
        {
            // Insert the row back
            _document.InsertRow(_rowIndex);

            // Restore the values
            var row = _document.Rows[_rowIndex];
            for (int i = 0; i < _deletedRow.Cells.Count && i < row.Cells.Count; i++)
            {
                row.Cells[i].Value = _deletedRow.Cells[i].Value;
            }
        }
    }
}
