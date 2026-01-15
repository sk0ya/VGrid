using System.Windows.Input;
using VGrid.Commands;
using VGrid.Models;
using VGrid.VimEngine;
using Xunit;

namespace VGrid.Tests.VimEngine;

public class NormalModeInsertLineTests
{
    [Fact]
    public void O_InsertsLineAbove_AndEntersInsertMode()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);
        var row2 = new Row(1, new[] { "D", "E", "F" });
        document.Rows.Add(row2);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(1, 1); // Row 1, Cell "E"

        var mode = new NormalMode();

        // Act - simulate "O" (Shift+O)
        mode.HandleKey(state, Key.O, ModifierKeys.Shift, document);

        // Assert - should insert row above (at index 1)
        Assert.Equal(3, document.RowCount);

        // Assert - cursor should be on the new row (row 1, col 1) - maintains column
        Assert.Equal(1, state.CursorPosition.Row);
        Assert.Equal(1, state.CursorPosition.Column);

        // Assert - should be in Insert mode
        Assert.Equal(VimMode.Insert, state.CurrentMode);

        // Assert - original row 1 should now be at row 2
        Assert.Equal("D", document.Rows[2].Cells[0].Value);
        Assert.Equal("E", document.Rows[2].Cells[1].Value);
        Assert.Equal("F", document.Rows[2].Cells[2].Value);
    }

    [Fact]
    public void LowercaseO_InsertsLineBelow_AndEntersInsertMode()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);
        var row2 = new Row(1, new[] { "D", "E", "F" });
        document.Rows.Add(row2);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 1); // Row 0, Cell "B"

        var mode = new NormalMode();

        // Act - simulate "o" (no Shift)
        mode.HandleKey(state, Key.O, ModifierKeys.None, document);

        // Assert - should insert row below (at index 1)
        Assert.Equal(3, document.RowCount);

        // Assert - cursor should be on the new row (row 1, col 1) - maintains column
        Assert.Equal(1, state.CursorPosition.Row);
        Assert.Equal(1, state.CursorPosition.Column);

        // Assert - should be in Insert mode
        Assert.Equal(VimMode.Insert, state.CurrentMode);

        // Assert - original row 1 should now be at row 2
        Assert.Equal("D", document.Rows[2].Cells[0].Value);
        Assert.Equal("E", document.Rows[2].Cells[1].Value);
        Assert.Equal("F", document.Rows[2].Cells[2].Value);
    }

    [Fact]
    public void O_OnFirstRow_InsertsRowAtBeginning()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 0); // First row

        var mode = new NormalMode();

        // Act - simulate "O" on first row
        mode.HandleKey(state, Key.O, ModifierKeys.Shift, document);

        // Assert - should insert row at index 0
        Assert.Equal(2, document.RowCount);

        // Assert - cursor should be on the new row (row 0, col 0)
        Assert.Equal(0, state.CursorPosition.Row);
        Assert.Equal(0, state.CursorPosition.Column);

        // Assert - original row should now be at row 1
        Assert.Equal("A", document.Rows[1].Cells[0].Value);
    }

    [Fact]
    public void LowercaseO_OnLastRow_InsertsRowAtEnd()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);
        var row2 = new Row(1, new[] { "D", "E", "F" });
        document.Rows.Add(row2);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(1, 2); // Last row

        var mode = new NormalMode();

        // Act - simulate "o" on last row
        mode.HandleKey(state, Key.O, ModifierKeys.None, document);

        // Assert - should insert row after last row (at index 2)
        Assert.Equal(3, document.RowCount);

        // Assert - cursor should be on the new row (row 2, col 2) - maintains column
        Assert.Equal(2, state.CursorPosition.Row);
        Assert.Equal(2, state.CursorPosition.Column);

        // Assert - should be in Insert mode
        Assert.Equal(VimMode.Insert, state.CurrentMode);
    }
}
