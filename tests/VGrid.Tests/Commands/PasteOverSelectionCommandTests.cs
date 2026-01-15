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

    [Fact]
    public void Execute_VerticalSelectionWithHorizontalContent_Expands3x3()
    {
        // Arrange: 3 rows x 3 columns document
        var document = new TsvDocument();
        document.Rows.Add(new Row(0, new[] { "A", "B", "C" }));
        document.Rows.Add(new Row(1, new[] { "D", "E", "F" }));
        document.Rows.Add(new Row(2, new[] { "G", "H", "I" }));

        // Clipboard has 1 row x 3 columns
        var yank = new YankedContent
        {
            Values = new string[,] { { "X", "Y", "Z" } },
            SourceType = VisualType.Character,
            Rows = 1,
            Columns = 3
        };

        // Selection is 3 rows x 1 column (vertical)
        var selection = new SelectionRange(
            VisualType.Character,
            new GridPosition(0, 0),
            new GridPosition(2, 0));

        var command = new PasteOverSelectionCommand(document, selection, yank);

        // Act
        command.Execute();

        // Assert: Should paste 3x3 area (each row gets X, Y, Z)
        // Row 0
        Assert.Equal("X", document.GetCell(0, 0)!.Value);
        Assert.Equal("Y", document.GetCell(0, 1)!.Value);
        Assert.Equal("Z", document.GetCell(0, 2)!.Value);

        // Row 1
        Assert.Equal("X", document.GetCell(1, 0)!.Value);
        Assert.Equal("Y", document.GetCell(1, 1)!.Value);
        Assert.Equal("Z", document.GetCell(1, 2)!.Value);

        // Row 2
        Assert.Equal("X", document.GetCell(2, 0)!.Value);
        Assert.Equal("Y", document.GetCell(2, 1)!.Value);
        Assert.Equal("Z", document.GetCell(2, 2)!.Value);
    }

    [Fact]
    public void Execute_HorizontalSelectionWithVerticalContent_Expands3x3()
    {
        // Arrange: 3 rows x 3 columns document
        var document = new TsvDocument();
        document.Rows.Add(new Row(0, new[] { "A", "B", "C" }));
        document.Rows.Add(new Row(1, new[] { "D", "E", "F" }));
        document.Rows.Add(new Row(2, new[] { "G", "H", "I" }));

        // Clipboard has 3 rows x 1 column (vertical)
        var yank = new YankedContent
        {
            Values = new string[,] { { "X" }, { "Y" }, { "Z" } },
            SourceType = VisualType.Character,
            Rows = 3,
            Columns = 1
        };

        // Selection is 1 row x 3 columns (horizontal)
        var selection = new SelectionRange(
            VisualType.Character,
            new GridPosition(0, 0),
            new GridPosition(0, 2));

        var command = new PasteOverSelectionCommand(document, selection, yank);

        // Act
        command.Execute();

        // Assert: Should paste 3x3 area (each column gets X, Y, Z)
        // Column 0
        Assert.Equal("X", document.GetCell(0, 0)!.Value);
        Assert.Equal("Y", document.GetCell(1, 0)!.Value);
        Assert.Equal("Z", document.GetCell(2, 0)!.Value);

        // Column 1
        Assert.Equal("X", document.GetCell(0, 1)!.Value);
        Assert.Equal("Y", document.GetCell(1, 1)!.Value);
        Assert.Equal("Z", document.GetCell(2, 1)!.Value);

        // Column 2
        Assert.Equal("X", document.GetCell(0, 2)!.Value);
        Assert.Equal("Y", document.GetCell(1, 2)!.Value);
        Assert.Equal("Z", document.GetCell(2, 2)!.Value);
    }

    [Fact]
    public void Execute_2x2SelectionWith1x1Content_Repeats2x2()
    {
        // Arrange: 3 rows x 3 columns document
        var document = new TsvDocument();
        document.Rows.Add(new Row(0, new[] { "A", "B", "C" }));
        document.Rows.Add(new Row(1, new[] { "D", "E", "F" }));
        document.Rows.Add(new Row(2, new[] { "G", "H", "I" }));

        // Clipboard has 1 row x 1 column
        var yank = new YankedContent
        {
            Values = new string[,] { { "X" } },
            SourceType = VisualType.Character,
            Rows = 1,
            Columns = 1
        };

        // Selection is 2 rows x 2 columns
        var selection = new SelectionRange(
            VisualType.Character,
            new GridPosition(0, 0),
            new GridPosition(1, 1));

        var command = new PasteOverSelectionCommand(document, selection, yank);

        // Act
        command.Execute();

        // Assert: Should paste 2x2 area (repeat X in all cells)
        Assert.Equal("X", document.GetCell(0, 0)!.Value);
        Assert.Equal("X", document.GetCell(0, 1)!.Value);
        Assert.Equal("X", document.GetCell(1, 0)!.Value);
        Assert.Equal("X", document.GetCell(1, 1)!.Value);

        // Other cells unchanged
        Assert.Equal("C", document.GetCell(0, 2)!.Value);
        Assert.Equal("F", document.GetCell(1, 2)!.Value);
        Assert.Equal("G", document.GetCell(2, 0)!.Value);
    }

    [Fact]
    public void Execute_1RowVerticalSelectionWithVerticalContent_Works()
    {
        // Arrange: Test case where selection is "1 row with 3 columns" but positioned vertically
        // This tests the symmetry of the paste operation
        var document = new TsvDocument();
        document.Rows.Add(new Row(0, new[] { "A", "B", "C", "D" }));
        document.Rows.Add(new Row(1, new[] { "E", "F", "G", "H" }));
        document.Rows.Add(new Row(2, new[] { "I", "J", "K", "L" }));

        // Clipboard has 3 rows x 1 column (vertical: X, Y, Z)
        var yank = new YankedContent
        {
            Values = new string[,] { { "X" }, { "Y" }, { "Z" } },
            SourceType = VisualType.Character,
            Rows = 3,
            Columns = 1
        };

        // Selection is 1 row x 3 columns (horizontal at row 1)
        var selection = new SelectionRange(
            VisualType.Character,
            new GridPosition(1, 0),
            new GridPosition(1, 2));

        var command = new PasteOverSelectionCommand(document, selection, yank);

        // Act
        command.Execute();

        // Assert: Should paste 3x3 area starting at (1,0)
        // Row 1
        Assert.Equal("X", document.GetCell(1, 0)!.Value);
        Assert.Equal("X", document.GetCell(1, 1)!.Value);
        Assert.Equal("X", document.GetCell(1, 2)!.Value);

        // Row 2
        Assert.Equal("Y", document.GetCell(2, 0)!.Value);
        Assert.Equal("Y", document.GetCell(2, 1)!.Value);
        Assert.Equal("Y", document.GetCell(2, 2)!.Value);

        // Row 0 unchanged
        Assert.Equal("A", document.GetCell(0, 0)!.Value);
    }

    [Fact]
    public void Execute_VerticalSelectionWithHorizontalContentAtOffset_Works()
    {
        // Arrange: Test vertical selection at column offset
        var document = new TsvDocument();
        document.Rows.Add(new Row(0, new[] { "A", "B", "C", "D" }));
        document.Rows.Add(new Row(1, new[] { "E", "F", "G", "H" }));
        document.Rows.Add(new Row(2, new[] { "I", "J", "K", "L" }));

        // Clipboard has 1 row x 3 columns (horizontal: X, Y, Z)
        var yank = new YankedContent
        {
            Values = new string[,] { { "X", "Y", "Z" } },
            SourceType = VisualType.Character,
            Rows = 1,
            Columns = 3
        };

        // Selection is 3 rows x 1 column (vertical at column 1)
        var selection = new SelectionRange(
            VisualType.Character,
            new GridPosition(0, 1),
            new GridPosition(2, 1));

        var command = new PasteOverSelectionCommand(document, selection, yank);

        // Act
        command.Execute();

        // Assert: Should paste 3x3 area starting at (0,1)
        // Row 0
        Assert.Equal("A", document.GetCell(0, 0)!.Value); // unchanged
        Assert.Equal("X", document.GetCell(0, 1)!.Value);
        Assert.Equal("Y", document.GetCell(0, 2)!.Value);
        Assert.Equal("Z", document.GetCell(0, 3)!.Value);

        // Row 1
        Assert.Equal("E", document.GetCell(1, 0)!.Value); // unchanged
        Assert.Equal("X", document.GetCell(1, 1)!.Value);
        Assert.Equal("Y", document.GetCell(1, 2)!.Value);
        Assert.Equal("Z", document.GetCell(1, 3)!.Value);

        // Row 2
        Assert.Equal("I", document.GetCell(2, 0)!.Value); // unchanged
        Assert.Equal("X", document.GetCell(2, 1)!.Value);
        Assert.Equal("Y", document.GetCell(2, 2)!.Value);
        Assert.Equal("Z", document.GetCell(2, 3)!.Value);
    }
}
