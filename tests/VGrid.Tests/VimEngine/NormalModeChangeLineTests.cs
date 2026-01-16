using System.Windows.Input;
using VGrid.Commands;
using VGrid.Models;
using VGrid.VimEngine;
using Xunit;

namespace VGrid.Tests.VimEngine;

public class NormalModeChangeLineTests
{
    [Fact]
    public void Cc_ClearsCurrentLine_AndEntersInsertMode()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);
        var row2 = new Row(1, new[] { "D", "E", "F" });
        document.Rows.Add(row2);
        var row3 = new Row(2, new[] { "G", "H", "I" });
        document.Rows.Add(row3);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(1, 1); // Row 1, Cell "E"

        var mode = new NormalMode();

        // Act - simulate "cc" (press 'c' twice)
        mode.HandleKey(state, Key.C, ModifierKeys.None, document);
        mode.HandleKey(state, Key.C, ModifierKeys.None, document);

        // Assert - row should still exist (not deleted)
        Assert.Equal(3, document.RowCount);

        // Assert - all cells in row 1 should be cleared
        Assert.Equal(string.Empty, document.Rows[1].Cells[0].Value);
        Assert.Equal(string.Empty, document.Rows[1].Cells[1].Value);
        Assert.Equal(string.Empty, document.Rows[1].Cells[2].Value);

        // Assert - cursor should be on first column of the same row
        Assert.Equal(1, state.CursorPosition.Row);
        Assert.Equal(0, state.CursorPosition.Column);

        // Assert - should be in Insert mode
        Assert.Equal(VimMode.Insert, state.CurrentMode);

        // Assert - other rows should remain unchanged
        Assert.Equal("A", document.Rows[0].Cells[0].Value);
        Assert.Equal("B", document.Rows[0].Cells[1].Value);
        Assert.Equal("C", document.Rows[0].Cells[2].Value);
        Assert.Equal("G", document.Rows[2].Cells[0].Value);
        Assert.Equal("H", document.Rows[2].Cells[1].Value);
        Assert.Equal("I", document.Rows[2].Cells[2].Value);
    }

    [Fact]
    public void Cc_YanksLineBeforeClearing()
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

        // Act - simulate "cc"
        mode.HandleKey(state, Key.C, ModifierKeys.None, document);
        mode.HandleKey(state, Key.C, ModifierKeys.None, document);

        // Assert - yanked content should be the entire row
        Assert.NotNull(state.LastYank);
        Assert.Equal(VisualType.Line, state.LastYank.SourceType);
        Assert.Equal(1, state.LastYank.Rows);
        Assert.Equal(3, state.LastYank.Columns);
        Assert.Equal("D", state.LastYank.Values[0, 0]);
        Assert.Equal("E", state.LastYank.Values[0, 1]);
        Assert.Equal("F", state.LastYank.Values[0, 2]);
    }

    [Fact]
    public void Cc_CanBeUndone()
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

        // Act - simulate "cc" followed by undo
        mode.HandleKey(state, Key.C, ModifierKeys.None, document);
        mode.HandleKey(state, Key.C, ModifierKeys.None, document);

        // Verify clearing worked
        Assert.Equal(string.Empty, document.Rows[1].Cells[0].Value);
        Assert.Equal(string.Empty, document.Rows[1].Cells[1].Value);
        Assert.Equal(string.Empty, document.Rows[1].Cells[2].Value);

        // Exit insert mode first
        state.SwitchMode(VimMode.Normal);

        // Undo the change
        mode.HandleKey(state, Key.U, ModifierKeys.None, document);

        // Assert - row should be restored
        Assert.Equal("D", document.Rows[1].Cells[0].Value);
        Assert.Equal("E", document.Rows[1].Cells[1].Value);
        Assert.Equal("F", document.Rows[1].Cells[2].Value);
    }

    [Fact]
    public void Cc_OnFirstRow_Works()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 0); // First row

        var mode = new NormalMode();

        // Act - simulate "cc" on first row
        mode.HandleKey(state, Key.C, ModifierKeys.None, document);
        mode.HandleKey(state, Key.C, ModifierKeys.None, document);

        // Assert - row should still exist (not deleted)
        Assert.Equal(1, document.RowCount);

        // Assert - all cells should be cleared
        Assert.Equal(string.Empty, document.Rows[0].Cells[0].Value);
        Assert.Equal(string.Empty, document.Rows[0].Cells[1].Value);
        Assert.Equal(string.Empty, document.Rows[0].Cells[2].Value);

        // Assert - cursor should be on first column
        Assert.Equal(0, state.CursorPosition.Row);
        Assert.Equal(0, state.CursorPosition.Column);

        // Assert - should be in Insert mode
        Assert.Equal(VimMode.Insert, state.CurrentMode);
    }

    [Fact]
    public void Cc_SetsPendingInsertTypeCorrectly()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 1);

        var mode = new NormalMode();

        // Act - simulate "cc"
        mode.HandleKey(state, Key.C, ModifierKeys.None, document);
        mode.HandleKey(state, Key.C, ModifierKeys.None, document);

        // Assert - PendingInsertType should be ChangeLine
        Assert.Equal(ChangeType.ChangeLine, state.PendingInsertType);
    }
}
