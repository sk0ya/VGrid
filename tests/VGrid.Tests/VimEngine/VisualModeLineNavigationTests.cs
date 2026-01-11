using System.Windows.Input;
using VGrid.Models;
using VGrid.VimEngine;
using Xunit;

namespace VGrid.Tests.VimEngine;

public class VisualModeLineNavigationTests
{
    [Fact]
    public void ShiftH_MovesToLineStartWithSelection()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C", "D" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CursorPosition = new GridPosition(0, 2); // Start at column 2
        state.CurrentSelection = new SelectionRange(
            VisualType.Character,
            state.CursorPosition,
            state.CursorPosition);

        // Switch to Visual mode explicitly
        state.SwitchMode(VimMode.Visual);

        var mode = new VisualMode();
        mode.OnEnter(state);

        // Act - simulate Shift+H
        mode.HandleKey(state, Key.H, ModifierKeys.Shift, document);

        // Assert - should move to column 0
        Assert.Equal(0, state.CursorPosition.Column);
        Assert.Equal(0, state.CursorPosition.Row);

        // Assert - selection should be updated
        Assert.NotNull(state.CurrentSelection);
        Assert.Equal(VimMode.Visual, state.CurrentMode);
    }

    [Fact]
    public void LowercaseH_MovesLeftWithSelection()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C", "D" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CursorPosition = new GridPosition(0, 2); // Start at column 2
        state.CurrentSelection = new SelectionRange(
            VisualType.Character,
            state.CursorPosition,
            state.CursorPosition);

        // Switch to Visual mode explicitly
        state.SwitchMode(VimMode.Visual);

        var mode = new VisualMode();
        mode.OnEnter(state);

        // Act - simulate 'h' (no Shift)
        mode.HandleKey(state, Key.H, ModifierKeys.None, document);

        // Assert - should move left by 1
        Assert.Equal(1, state.CursorPosition.Column);
        Assert.Equal(0, state.CursorPosition.Row);
    }

    [Fact]
    public void ShiftL_MovesToLastNonEmptyColumnWithSelection()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C", "", "" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CursorPosition = new GridPosition(0, 0); // Start at column 0
        state.CurrentSelection = new SelectionRange(
            VisualType.Character,
            state.CursorPosition,
            state.CursorPosition);

        // Switch to Visual mode explicitly
        state.SwitchMode(VimMode.Visual);

        var mode = new VisualMode();
        mode.OnEnter(state);

        // Act - simulate Shift+L
        mode.HandleKey(state, Key.L, ModifierKeys.Shift, document);

        // Assert - should move to column 2 (last non-empty cell)
        Assert.Equal(2, state.CursorPosition.Column);
        Assert.Equal(0, state.CursorPosition.Row);

        // Assert - selection should span from column 0 to column 2
        Assert.NotNull(state.CurrentSelection);
        Assert.Equal(0, state.CurrentSelection.StartColumn);
        Assert.Equal(2, state.CurrentSelection.EndColumn);
    }

    [Fact]
    public void LowercaseL_MovesRightWithSelection()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C", "D" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CursorPosition = new GridPosition(0, 1); // Start at column 1
        state.CurrentSelection = new SelectionRange(
            VisualType.Character,
            state.CursorPosition,
            state.CursorPosition);

        // Switch to Visual mode explicitly
        state.SwitchMode(VimMode.Visual);

        var mode = new VisualMode();
        mode.OnEnter(state);

        // Act - simulate 'l' (no Shift)
        mode.HandleKey(state, Key.L, ModifierKeys.None, document);

        // Assert - should move right by 1
        Assert.Equal(2, state.CursorPosition.Column);
        Assert.Equal(0, state.CursorPosition.Row);
    }

    [Fact]
    public void ShiftL_InVisualLineMode_MovesToLastNonEmptyColumn()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C", "", "" });
        document.Rows.Add(row1);
        var row2 = new Row(1, new[] { "D", "E", "F", "G", "" });
        document.Rows.Add(row2);

        var state = new VimState();
        state.CursorPosition = new GridPosition(0, 0); // Start at row 0, column 0
        state.CurrentSelection = new SelectionRange(
            VisualType.Line,
            state.CursorPosition,
            state.CursorPosition);

        // Switch to Visual mode explicitly
        state.SwitchMode(VimMode.Visual);

        var mode = new VisualMode();
        mode.OnEnter(state);

        // Act - simulate Shift+L
        mode.HandleKey(state, Key.L, ModifierKeys.Shift, document);

        // Assert - should move to column 2 (last non-empty cell in row 0)
        Assert.Equal(2, state.CursorPosition.Column);
        Assert.Equal(0, state.CursorPosition.Row);
    }

    [Fact]
    public void ShiftH_ThenShiftL_NavigatesBetweenLineStartAndEnd()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C", "D" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CursorPosition = new GridPosition(0, 2); // Start at column 2
        state.CurrentSelection = new SelectionRange(
            VisualType.Character,
            state.CursorPosition,
            state.CursorPosition);

        // Switch to Visual mode explicitly
        state.SwitchMode(VimMode.Visual);

        var mode = new VisualMode();
        mode.OnEnter(state);

        // Act - simulate Shift+H
        mode.HandleKey(state, Key.H, ModifierKeys.Shift, document);

        // Assert - should be at column 0
        Assert.Equal(0, state.CursorPosition.Column);

        // Act - simulate Shift+L
        mode.HandleKey(state, Key.L, ModifierKeys.Shift, document);

        // Assert - should move to column 3 (last non-empty cell)
        Assert.Equal(3, state.CursorPosition.Column);
    }

    [Fact]
    public void Zero_MovesToFirstRowFirstColumnWithSelection()
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
        state.CursorPosition = new GridPosition(2, 2); // Start at row 2, column 2
        state.CurrentSelection = new SelectionRange(
            VisualType.Character,
            state.CursorPosition,
            state.CursorPosition);

        // Switch to Visual mode explicitly
        state.SwitchMode(VimMode.Visual);

        var mode = new VisualMode();
        mode.OnEnter(state);

        // Act - simulate '0' key
        mode.HandleKey(state, Key.D0, ModifierKeys.None, document);

        // Assert - should move to row 0, column 0
        Assert.Equal(0, state.CursorPosition.Row);
        Assert.Equal(0, state.CursorPosition.Column);

        // Assert - selection should span from (0,0) to (2,2)
        Assert.NotNull(state.CurrentSelection);
        Assert.Equal(0, state.CurrentSelection.StartRow);
        Assert.Equal(0, state.CurrentSelection.StartColumn);
        Assert.Equal(2, state.CurrentSelection.EndRow);
        Assert.Equal(2, state.CurrentSelection.EndColumn);
    }

    [Fact]
    public void Zero_FromMiddlePosition_CreatesSelectionToOrigin()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);
        var row2 = new Row(1, new[] { "D", "E", "F" });
        document.Rows.Add(row2);

        var state = new VimState();
        state.CursorPosition = new GridPosition(1, 1); // Start at row 1, column 1
        state.CurrentSelection = new SelectionRange(
            VisualType.Character,
            state.CursorPosition,
            state.CursorPosition);

        // Switch to Visual mode explicitly
        state.SwitchMode(VimMode.Visual);

        var mode = new VisualMode();
        mode.OnEnter(state);

        // Act - simulate '0' key
        mode.HandleKey(state, Key.D0, ModifierKeys.None, document);

        // Assert - should move to row 0, column 0
        Assert.Equal(0, state.CursorPosition.Row);
        Assert.Equal(0, state.CursorPosition.Column);

        // Assert - selection should be updated
        Assert.NotNull(state.CurrentSelection);
        Assert.Equal(VimMode.Visual, state.CurrentMode);
    }

    [Fact]
    public void Zero_InVisualLineMode_MovesToFirstRowFirstColumn()
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
        state.CursorPosition = new GridPosition(2, 1); // Start at row 2, column 1
        state.CurrentSelection = new SelectionRange(
            VisualType.Line,
            state.CursorPosition,
            state.CursorPosition);

        // Switch to Visual mode explicitly
        state.SwitchMode(VimMode.Visual);

        var mode = new VisualMode();
        mode.OnEnter(state);

        // Act - simulate '0' key
        mode.HandleKey(state, Key.D0, ModifierKeys.None, document);

        // Assert - should move to row 0, column 0
        Assert.Equal(0, state.CursorPosition.Row);
        Assert.Equal(0, state.CursorPosition.Column);

        // Assert - should still be in Visual mode
        Assert.Equal(VimMode.Visual, state.CurrentMode);
    }
}
