using VGrid.Commands;
using VGrid.Models;
using VGrid.VimEngine;
using Xunit;

namespace VGrid.Tests.Commands;

public class PasteOverSelectionCommandTests
{
    [Fact]
    public void Execute_LineSelection_FillsAllCellsInRows()
    {
        // Arrange
        var document = new TsvDocument();
        document.Rows.Add(new Row(0, new[] { "A", "B", "C" }));
        document.Rows.Add(new Row(1, new[] { "D", "E", "F" }));
        document.Rows.Add(new Row(2, new[] { "G", "H", "I" }));

        var yank = new YankedContent
        {
            Values = new string[,] { { "X" } },
            SourceType = VisualType.Character,
            Rows = 1,
            Columns = 1
        };

        var selection = new SelectionRange(
            VisualType.Line,
            new GridPosition(0, 0),
            new GridPosition(1, 0));

        var command = new PasteOverSelectionCommand(document, selection, yank);

        // Act
        command.Execute();

        // Assert
        // First two rows should be filled with "X"
        Assert.Equal("X", document.GetCell(0, 0)!.Value);
        Assert.Equal("X", document.GetCell(0, 1)!.Value);
        Assert.Equal("X", document.GetCell(0, 2)!.Value);
        Assert.Equal("X", document.GetCell(1, 0)!.Value);
        Assert.Equal("X", document.GetCell(1, 1)!.Value);
        Assert.Equal("X", document.GetCell(1, 2)!.Value);

        // Third row should be unchanged
        Assert.Equal("G", document.GetCell(2, 0)!.Value);
    }
}
