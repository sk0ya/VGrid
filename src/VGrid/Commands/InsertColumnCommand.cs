using VGrid.Models;

namespace VGrid.Commands;

/// <summary>
/// Command to insert a new column with undo support
/// </summary>
public class InsertColumnCommand : ICommand
{
    private readonly TsvDocument _document;
    private readonly int _columnIndex;

    public string Description => $"Insert column at {_columnIndex}";

    public InsertColumnCommand(TsvDocument document, int columnIndex)
    {
        _document = document;
        _columnIndex = columnIndex;
    }

    public void Execute()
    {
        if (_columnIndex >= 0 && _columnIndex <= _document.ColumnCount)
        {
            // Insert a new empty column at the specified index
            _document.InsertColumn(_columnIndex);
        }
    }

    public void Undo()
    {
        if (_columnIndex >= 0 && _columnIndex < _document.ColumnCount)
        {
            // Delete the column that was inserted
            _document.DeleteColumn(_columnIndex);
        }
    }
}
