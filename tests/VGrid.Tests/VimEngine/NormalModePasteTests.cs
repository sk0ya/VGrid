using System.Windows.Input;
using VGrid.Commands;
using VGrid.Models;
using VGrid.VimEngine;
using Xunit;

namespace VGrid.Tests.VimEngine;

public class NormalModePasteTests
{
    [Fact]
    public void PasteAfterCursor_WithLowerP_InsertsBelow()
    {
        // Arrange
        var document = new TsvDocument();
        document.Rows.Add(new Row(0, new[] { "A", "B", "C" }));
        document.Rows.Add(new Row(1, new[] { "D", "E", "F" }));

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 0);

        // Set up yanked content (line)
        state.LastYank = new YankedContent
        {
            SourceType = VisualType.Line,
            Values = new string[,] { { "X", "Y", "Z" } },
            Rows = 1,
            Columns = 3
        };

        var mode = new NormalMode();

        // Act - Press 'p' (lowercase, paste after)
        mode.HandleKey(state, Key.P, ModifierKeys.None, document);

        // Assert - Row was inserted below current row
        Assert.Equal(3, document.RowCount);
        Assert.Equal("A", document.Rows[0].Cells[0].Value);
        Assert.Equal("X", document.Rows[1].Cells[0].Value);
        Assert.Equal("D", document.Rows[2].Cells[0].Value);
    }

    [Fact]
    public void PasteBeforeCursor_WithShiftP_InsertsAbove()
    {
        // Arrange
        var document = new TsvDocument();
        document.Rows.Add(new Row(0, new[] { "A", "B", "C" }));
        document.Rows.Add(new Row(1, new[] { "D", "E", "F" }));

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(1, 0);

        // Set up yanked content (line)
        state.LastYank = new YankedContent
        {
            SourceType = VisualType.Line,
            Values = new string[,] { { "X", "Y", "Z" } },
            Rows = 1,
            Columns = 3
        };

        var mode = new NormalMode();

        // Act - Press 'P' (Shift+P, paste before)
        mode.HandleKey(state, Key.P, ModifierKeys.Shift, document);

        // Assert - Row was inserted above current row
        Assert.Equal(3, document.RowCount);
        Assert.Equal("A", document.Rows[0].Cells[0].Value);
        Assert.Equal("X", document.Rows[1].Cells[0].Value);
        Assert.Equal("D", document.Rows[2].Cells[0].Value);
    }

    [Fact]
    public void PasteBeforeCursor_WithShiftP_BlockMode_InsertsLeft()
    {
        // Arrange
        var document = new TsvDocument();
        document.Rows.Add(new Row(0, new[] { "A", "B", "C" }));
        document.Rows.Add(new Row(1, new[] { "D", "E", "F" }));

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 1);

        // Set up yanked content (block/column)
        state.LastYank = new YankedContent
        {
            SourceType = VisualType.Block,
            Values = new string[,] { { "X" }, { "Y" } },
            Rows = 2,
            Columns = 1
        };

        var mode = new NormalMode();

        // Act - Press 'P' (Shift+P, paste before)
        mode.HandleKey(state, Key.P, ModifierKeys.Shift, document);

        // Assert - Column was inserted to the left
        Assert.Equal(4, document.ColumnCount);
        Assert.Equal("A", document.Rows[0].Cells[0].Value);
        Assert.Equal("X", document.Rows[0].Cells[1].Value);
        Assert.Equal("B", document.Rows[0].Cells[2].Value);
        Assert.Equal("C", document.Rows[0].Cells[3].Value);
        Assert.Equal("D", document.Rows[1].Cells[0].Value);
        Assert.Equal("Y", document.Rows[1].Cells[1].Value);
        Assert.Equal("E", document.Rows[1].Cells[2].Value);
        Assert.Equal("F", document.Rows[1].Cells[3].Value);
    }

    [Fact]
    public void PasteAfterCursor_WithLowerP_BlockMode_InsertsRight()
    {
        // Arrange
        var document = new TsvDocument();
        document.Rows.Add(new Row(0, new[] { "A", "B", "C" }));
        document.Rows.Add(new Row(1, new[] { "D", "E", "F" }));

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 1);

        // Set up yanked content (block/column)
        state.LastYank = new YankedContent
        {
            SourceType = VisualType.Block,
            Values = new string[,] { { "X" }, { "Y" } },
            Rows = 2,
            Columns = 1
        };

        var mode = new NormalMode();

        // Act - Press 'p' (lowercase, paste after)
        mode.HandleKey(state, Key.P, ModifierKeys.None, document);

        // Assert - Column was inserted to the right
        Assert.Equal(4, document.ColumnCount);
        Assert.Equal("A", document.Rows[0].Cells[0].Value);
        Assert.Equal("B", document.Rows[0].Cells[1].Value);
        Assert.Equal("X", document.Rows[0].Cells[2].Value);
        Assert.Equal("C", document.Rows[0].Cells[3].Value);
        Assert.Equal("D", document.Rows[1].Cells[0].Value);
        Assert.Equal("E", document.Rows[1].Cells[1].Value);
        Assert.Equal("Y", document.Rows[1].Cells[2].Value);
        Assert.Equal("F", document.Rows[1].Cells[3].Value);
    }

    [Fact]
    public void PasteBeforeCursor_WithShiftP_CanUndo()
    {
        // Arrange
        var document = new TsvDocument();
        document.Rows.Add(new Row(0, new[] { "A", "B", "C" }));

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 0);

        // Set up yanked content (line)
        state.LastYank = new YankedContent
        {
            SourceType = VisualType.Line,
            Values = new string[,] { { "X", "Y", "Z" } },
            Rows = 1,
            Columns = 3
        };

        var mode = new NormalMode();

        // Act - Press 'P' (Shift+P, paste before)
        mode.HandleKey(state, Key.P, ModifierKeys.Shift, document);
        Assert.Equal(2, document.RowCount);
        Assert.Equal("X", document.Rows[0].Cells[0].Value);

        // Act - Undo with 'u'
        mode.HandleKey(state, Key.U, ModifierKeys.None, document);

        // Assert - Pasted row was removed
        Assert.Equal(1, document.RowCount);
        Assert.Equal("A", document.Rows[0].Cells[0].Value);
    }
}
