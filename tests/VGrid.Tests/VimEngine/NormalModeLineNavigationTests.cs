using System.Windows.Input;
using VGrid.Commands;
using VGrid.Models;
using VGrid.VimEngine;
using Xunit;

namespace VGrid.Tests.VimEngine;

public class NormalModeLineNavigationTests
{
    [Fact]
    public void ShiftH_MovesToLineStart()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C", "D" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 2); // Column 2

        var mode = new NormalMode();

        // Act - simulate Shift+H
        mode.HandleKey(state, Key.H, ModifierKeys.Shift, document);

        // Assert - should move to column 0
        Assert.Equal(0, state.CursorPosition.Column);
        Assert.Equal(0, state.CursorPosition.Row);
    }

    [Fact]
    public void LowercaseH_MovesLeft()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C", "D" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 2); // Column 2

        var mode = new NormalMode();

        // Act - simulate 'h' (no Shift)
        mode.HandleKey(state, Key.H, ModifierKeys.None, document);

        // Assert - should move left by 1
        Assert.Equal(1, state.CursorPosition.Column);
        Assert.Equal(0, state.CursorPosition.Row);
    }

    [Fact]
    public void ShiftL_MovesToLastNonEmptyColumn()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C", "", "" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 0); // Column 0

        var mode = new NormalMode();

        // Act - simulate Shift+L
        mode.HandleKey(state, Key.L, ModifierKeys.Shift, document);

        // Assert - should move to column 2 (last non-empty cell)
        Assert.Equal(2, state.CursorPosition.Column);
        Assert.Equal(0, state.CursorPosition.Row);
    }

    [Fact]
    public void LowercaseL_MovesRight()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C", "D" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 1); // Column 1

        var mode = new NormalMode();

        // Act - simulate 'l' (no Shift)
        mode.HandleKey(state, Key.L, ModifierKeys.None, document);

        // Assert - should move right by 1
        Assert.Equal(2, state.CursorPosition.Column);
        Assert.Equal(0, state.CursorPosition.Row);
    }

    [Fact]
    public void ShiftL_WithAllEmptyCells_StaysAtCurrentPosition()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "", "", "", "" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 1); // Column 1

        var mode = new NormalMode();

        // Act - simulate Shift+L
        mode.HandleKey(state, Key.L, ModifierKeys.Shift, document);

        // Assert - should stay at column 1 (no non-empty cells found)
        Assert.Equal(1, state.CursorPosition.Column);
        Assert.Equal(0, state.CursorPosition.Row);
    }

    [Fact]
    public void ShiftL_FindsLastNonEmptyInMiddleOfRow()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "", "C", "", "E", "", "" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 0); // Column 0

        var mode = new NormalMode();

        // Act - simulate Shift+L
        mode.HandleKey(state, Key.L, ModifierKeys.Shift, document);

        // Assert - should move to column 4 (last non-empty cell "E")
        Assert.Equal(4, state.CursorPosition.Column);
        Assert.Equal(0, state.CursorPosition.Row);
    }
}
