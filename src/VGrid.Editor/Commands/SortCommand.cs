using VGrid.Models;

namespace VGrid.Commands;

/// <summary>
/// Command for sorting rows by a column with undo support
/// </summary>
public class SortCommand : ICommand
{
    private readonly TsvDocument _document;
    private readonly int _columnIndex;
    private readonly bool _ascending;
    private readonly List<Row> _originalRowOrder;

    public string Description => $"Sort by column {_columnIndex} ({(_ascending ? "ascending" : "descending")})";

    public SortCommand(TsvDocument document, int columnIndex, bool ascending = true)
    {
        _document = document;
        _columnIndex = columnIndex;
        _ascending = ascending;

        // Store the original row order for undo
        _originalRowOrder = new List<Row>(document.Rows);
    }

    public void Execute()
    {
        _document.SortByColumn(_columnIndex, _ascending);
    }

    public void Undo()
    {
        // Restore the original row order
        _document.Rows.Clear();
        for (int i = 0; i < _originalRowOrder.Count; i++)
        {
            _originalRowOrder[i].Index = i;
            _document.Rows.Add(_originalRowOrder[i]);
        }

        _document.IsDirty = true;
    }
}
