using VGrid.Models;

namespace VGrid.Commands;

/// <summary>
/// Command to delete entire columns with undo support
/// </summary>
public class DeleteColumnCommand : ICommand
{
    private readonly TsvDocument _document;
    private readonly int _columnIndex;
    private List<string>? _deletedColumnValues;

    public string Description => $"Delete column {_columnIndex}";

    public DeleteColumnCommand(TsvDocument document, int columnIndex)
    {
        _document = document;
        _columnIndex = columnIndex;
    }

    public void Execute()
    {
        if (_columnIndex >= 0 && _columnIndex < _document.ColumnCount)
        {
            // Store all values from the column before deleting
            _deletedColumnValues = new List<string>();
            foreach (var row in _document.Rows)
            {
                if (_columnIndex < row.Cells.Count)
                {
                    _deletedColumnValues.Add(row.Cells[_columnIndex].Value);
                }
                else
                {
                    _deletedColumnValues.Add(string.Empty);
                }
            }

            // Delete the column
            _document.DeleteColumn(_columnIndex);
        }
    }

    public void Undo()
    {
        if (_deletedColumnValues != null && _columnIndex >= 0 && _columnIndex <= _document.ColumnCount)
        {
            // Insert the column back
            _document.InsertColumn(_columnIndex);

            // Restore the values
            for (int i = 0; i < _deletedColumnValues.Count && i < _document.RowCount; i++)
            {
                var row = _document.Rows[i];
                if (_columnIndex < row.Cells.Count)
                {
                    row.Cells[_columnIndex].Value = _deletedColumnValues[i];
                }
            }
        }
    }
}
