using System.Windows.Input;
using VGrid.Commands;
using VGrid.Models;
using VGrid.VimEngine;
using Xunit;

namespace VGrid.Tests.VimEngine;

public class NormalModeChangeWordTests
{
    [Fact]
    public void Ciw_ClearsCurrentCell_AndEntersInsertMode()
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

        // Act - simulate "ciw" (press 'c', 'i', 'w')
        mode.HandleKey(state, Key.C, ModifierKeys.None, document);
        mode.HandleKey(state, Key.I, ModifierKeys.None, document);
        mode.HandleKey(state, Key.W, ModifierKeys.None, document);

        // Assert - current cell should be cleared
        Assert.Equal(string.Empty, document.Rows[1].Cells[1].Value);

        // Assert - cursor should remain at the same position
        Assert.Equal(1, state.CursorPosition.Row);
        Assert.Equal(1, state.CursorPosition.Column);

        // Assert - should be in Insert mode
        Assert.Equal(VimMode.Insert, state.CurrentMode);

        // Assert - other cells should remain unchanged
        Assert.Equal("D", document.Rows[1].Cells[0].Value);
        Assert.Equal("F", document.Rows[1].Cells[2].Value);
    }

    [Fact]
    public void Caw_ClearsCurrentCell_AndEntersInsertMode()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);
        var row2 = new Row(1, new[] { "D", "E", "F" });
        document.Rows.Add(row2);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 2); // Row 0, Cell "C"

        var mode = new NormalMode();

        // Act - simulate "caw" (press 'c', 'a', 'w')
        mode.HandleKey(state, Key.C, ModifierKeys.None, document);
        mode.HandleKey(state, Key.A, ModifierKeys.None, document);
        mode.HandleKey(state, Key.W, ModifierKeys.None, document);

        // Assert - current cell should be cleared
        Assert.Equal(string.Empty, document.Rows[0].Cells[2].Value);

        // Assert - cursor should remain at the same position
        Assert.Equal(0, state.CursorPosition.Row);
        Assert.Equal(2, state.CursorPosition.Column);

        // Assert - should be in Insert mode
        Assert.Equal(VimMode.Insert, state.CurrentMode);

        // Assert - other cells should remain unchanged
        Assert.Equal("A", document.Rows[0].Cells[0].Value);
        Assert.Equal("B", document.Rows[0].Cells[1].Value);
    }

    [Fact]
    public void Ciw_YanksCellBeforeClearing()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "Hello", "World", "!" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 1); // Cell "World"

        var mode = new NormalMode();

        // Act - simulate "ciw"
        mode.HandleKey(state, Key.C, ModifierKeys.None, document);
        mode.HandleKey(state, Key.I, ModifierKeys.None, document);
        mode.HandleKey(state, Key.W, ModifierKeys.None, document);

        // Assert - yanked content should be the cell value
        Assert.NotNull(state.LastYank);
        Assert.Equal(VisualType.Character, state.LastYank.SourceType);
        Assert.Equal(1, state.LastYank.Rows);
        Assert.Equal(1, state.LastYank.Columns);
        Assert.Equal("World", state.LastYank.Values[0, 0]);
    }

    [Fact]
    public void Ciw_CanBeUndone()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 1); // Cell "B"

        var mode = new NormalMode();

        // Act - simulate "ciw" followed by undo
        mode.HandleKey(state, Key.C, ModifierKeys.None, document);
        mode.HandleKey(state, Key.I, ModifierKeys.None, document);
        mode.HandleKey(state, Key.W, ModifierKeys.None, document);

        // Verify clearing worked
        Assert.Equal(string.Empty, document.Rows[0].Cells[1].Value);

        // Exit insert mode first
        state.SwitchMode(VimMode.Normal);

        // Undo the change
        mode.HandleKey(state, Key.U, ModifierKeys.None, document);

        // Assert - cell should be restored
        Assert.Equal("B", document.Rows[0].Cells[1].Value);
    }

    [Fact]
    public void Ciw_SetsPendingInsertTypeCorrectly()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 1);

        var mode = new NormalMode();

        // Act - simulate "ciw"
        mode.HandleKey(state, Key.C, ModifierKeys.None, document);
        mode.HandleKey(state, Key.I, ModifierKeys.None, document);
        mode.HandleKey(state, Key.W, ModifierKeys.None, document);

        // Assert - PendingInsertType should be ChangeWord
        Assert.Equal(ChangeType.ChangeWord, state.PendingInsertType);
    }

    [Fact]
    public void Caw_SetsPendingInsertTypeCorrectly()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 1);

        var mode = new NormalMode();

        // Act - simulate "caw"
        mode.HandleKey(state, Key.C, ModifierKeys.None, document);
        mode.HandleKey(state, Key.A, ModifierKeys.None, document);
        mode.HandleKey(state, Key.W, ModifierKeys.None, document);

        // Assert - PendingInsertType should be ChangeWord
        Assert.Equal(ChangeType.ChangeWord, state.PendingInsertType);
    }

    [Fact]
    public void Ciw_SetsCaretPositionToStart()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 1);

        var mode = new NormalMode();

        // Act - simulate "ciw"
        mode.HandleKey(state, Key.C, ModifierKeys.None, document);
        mode.HandleKey(state, Key.I, ModifierKeys.None, document);
        mode.HandleKey(state, Key.W, ModifierKeys.None, document);

        // Assert - CellEditCaretPosition should be Start
        Assert.Equal(CellEditCaretPosition.Start, state.CellEditCaretPosition);
    }
}
