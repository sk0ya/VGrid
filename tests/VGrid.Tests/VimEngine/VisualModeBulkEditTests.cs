using System.Windows.Input;
using VGrid.Models;
using VGrid.VimEngine;
using Xunit;

namespace VGrid.Tests.VimEngine;

public class VisualModeBulkEditTests
{
    [Fact]
    public void VisualMode_I_ShouldSavePendingBulkEditRangeAndOriginalValue()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A1", "B1", "C1" });
        document.Rows.Add(row1);
        var row2 = new Row(1, new[] { "A2", "B2", "C2" });
        document.Rows.Add(row2);

        var state = new VimState();
        state.CursorPosition = new GridPosition(0, 0);

        // Enter Visual mode
        state.SwitchMode(VimMode.Visual);

        // Move to create a selection
        state.HandleKey(Key.L, ModifierKeys.None, document);
        state.HandleKey(Key.J, ModifierKeys.None, document);

        // Act - Press 'i' to initiate bulk edit
        state.HandleKey(Key.I, ModifierKeys.None, document);

        // Assert
        Assert.Equal(VimMode.Insert, state.CurrentMode);
        Assert.NotNull(state.PendingBulkEditRange);
        Assert.Equal(CellEditCaretPosition.Start, state.CellEditCaretPosition);
        Assert.Equal(new GridPosition(0, 0), state.CursorPosition);
        Assert.Equal("A1", state.OriginalCellValueForBulkEdit); // Original value saved
    }

    [Fact]
    public void VisualMode_A_ShouldSavePendingBulkEditRangeWithEndCaret()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A1", "B1", "C1" });
        document.Rows.Add(row1);
        var row2 = new Row(1, new[] { "A2", "B2", "C2" });
        document.Rows.Add(row2);

        var state = new VimState();
        state.CursorPosition = new GridPosition(0, 0);

        // Enter Visual mode
        state.SwitchMode(VimMode.Visual);

        // Move to create a selection
        state.HandleKey(Key.L, ModifierKeys.None, document);
        state.HandleKey(Key.J, ModifierKeys.None, document);

        // Act - Press 'a' to initiate bulk edit with cursor at end
        state.HandleKey(Key.A, ModifierKeys.None, document);

        // Assert
        Assert.Equal(VimMode.Insert, state.CurrentMode);
        Assert.NotNull(state.PendingBulkEditRange);
        Assert.Equal(CellEditCaretPosition.End, state.CellEditCaretPosition);
        Assert.Equal(new GridPosition(0, 0), state.CursorPosition);
        Assert.Equal("A1", state.OriginalCellValueForBulkEdit); // Original value saved
    }
}
