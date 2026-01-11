using System.Windows.Input;
using VGrid.Commands;
using VGrid.Models;
using VGrid.VimEngine;
using Xunit;

namespace VGrid.Tests.VimEngine;

public class NormalModeWordTests
{
    [Fact]
    public void YankWord_YiwCommand_YanksCurrentCell()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var row2 = new Row(1, new[] { "D", "E", "F" });
        document.Rows.Add(row2);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 1); // Cell "B"

        var mode = new NormalMode();

        // Act - simulate "yiw" key sequence
        mode.HandleKey(state, Key.Y, ModifierKeys.None, document);  // Press 'y'
        mode.HandleKey(state, Key.I, ModifierKeys.None, document);  // Press 'i'
        mode.HandleKey(state, Key.W, ModifierKeys.None, document);  // Press 'w'

        // Assert
        Assert.NotNull(state.LastYank);
        Assert.Equal("B", state.LastYank.Values[0, 0]);
        Assert.Equal(1, state.LastYank.Rows);
        Assert.Equal(1, state.LastYank.Columns);
    }

    [Fact]
    public void DeleteWord_DiwCommand_YanksAndClearsCurrentCell()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var row2 = new Row(1, new[] { "D", "E", "F" });
        document.Rows.Add(row2);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 1); // Cell "B"

        var mode = new NormalMode();

        // Act - simulate "diw" key sequence
        mode.HandleKey(state, Key.D, ModifierKeys.None, document);  // Press 'd'
        mode.HandleKey(state, Key.I, ModifierKeys.None, document);  // Press 'i'
        mode.HandleKey(state, Key.W, ModifierKeys.None, document);  // Press 'w'

        // Assert - should yank "B"
        Assert.NotNull(state.LastYank);
        Assert.Equal("B", state.LastYank.Values[0, 0]);

        // Assert - cell should be cleared
        var cell = document.GetCell(new GridPosition(0, 1));
        Assert.Equal(string.Empty, cell?.Value);
    }

    [Fact]
    public void YankWord_YawCommand_YanksCurrentCell()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var row2 = new Row(1, new[] { "D", "E", "F" });
        document.Rows.Add(row2);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(1, 2); // Cell "F"

        var mode = new NormalMode();

        // Act - simulate "yaw" key sequence
        mode.HandleKey(state, Key.Y, ModifierKeys.None, document);  // Press 'y'
        mode.HandleKey(state, Key.A, ModifierKeys.None, document);  // Press 'a'
        mode.HandleKey(state, Key.W, ModifierKeys.None, document);  // Press 'w'

        // Assert
        Assert.NotNull(state.LastYank);
        Assert.Equal("F", state.LastYank.Values[0, 0]);
    }

    [Fact]
    public void CtrlC_CopiesCurrentCell()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var row2 = new Row(1, new[] { "D", "E", "F" });
        document.Rows.Add(row2);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 1); // Cell "B"

        var mode = new NormalMode();

        // Act - simulate "Ctrl+C"
        mode.HandleKey(state, Key.C, ModifierKeys.Control, document);

        // Assert
        Assert.NotNull(state.LastYank);
        Assert.Equal("B", state.LastYank.Values[0, 0]);
        Assert.Equal(1, state.LastYank.Rows);
        Assert.Equal(1, state.LastYank.Columns);
    }

    [Fact]
    public void CtrlC_ThenPaste_CopiesAndPastesCellValue()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var row2 = new Row(1, new[] { "D", "E", "F" });
        document.Rows.Add(row2);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 1); // Cell "B"

        var mode = new NormalMode();

        // Act - copy cell "B" with Ctrl+C
        mode.HandleKey(state, Key.C, ModifierKeys.Control, document);

        // Move to cell "E"
        state.CursorPosition = new GridPosition(1, 1);

        // Paste with 'p'
        mode.HandleKey(state, Key.P, ModifierKeys.None, document);

        // Assert - cell "E" should now contain "B"
        var cell = document.GetCell(new GridPosition(1, 1));
        Assert.Equal("B", cell?.Value);
    }

    [Fact]
    public void CtrlV_PastesCellValue()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var row2 = new Row(1, new[] { "D", "E", "F" });
        document.Rows.Add(row2);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 1); // Cell "B"

        var mode = new NormalMode();

        // Act - copy cell "B" with Ctrl+C
        mode.HandleKey(state, Key.C, ModifierKeys.Control, document);

        // Move to cell "E"
        state.CursorPosition = new GridPosition(1, 1);

        // Paste with Ctrl+V
        mode.HandleKey(state, Key.V, ModifierKeys.Control, document);

        // Assert - cell "E" should now contain "B"
        var cell = document.GetCell(new GridPosition(1, 1));
        Assert.Equal("B", cell?.Value);
    }

    [Fact]
    public void CtrlShiftV_EntersBlockVisualMode()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CursorPosition = new GridPosition(0, 0);

        var mode = new NormalMode();

        // Act - press Ctrl+Shift+V
        mode.HandleKey(state, Key.V, ModifierKeys.Control | ModifierKeys.Shift, document);

        // Assert - should be in Visual mode with Block type
        Assert.Equal(VimMode.Visual, state.CurrentMode);
        Assert.NotNull(state.CurrentSelection);
        Assert.Equal(VisualType.Block, state.CurrentSelection.Type);
    }

    [Fact]
    public void X_DeletesCurrentCell()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var row2 = new Row(1, new[] { "D", "E", "F" });
        document.Rows.Add(row2);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 1); // Cell "B"

        var mode = new NormalMode();

        // Act - press 'x' to delete current cell
        mode.HandleKey(state, Key.X, ModifierKeys.None, document);

        // Assert - cell should be cleared
        var cell = document.GetCell(new GridPosition(0, 1));
        Assert.Equal(string.Empty, cell?.Value);

        // Assert - value should be yanked
        Assert.NotNull(state.LastYank);
        Assert.Equal("B", state.LastYank.Values[0, 0]);
    }

    [Fact]
    public void X_ThenPaste_DeletesAndPastesCellValue()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var row2 = new Row(1, new[] { "D", "E", "F" });
        document.Rows.Add(row2);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 1); // Cell "B"

        var mode = new NormalMode();

        // Act - delete cell "B" with 'x'
        mode.HandleKey(state, Key.X, ModifierKeys.None, document);

        // Verify cell "B" is cleared
        var cellB = document.GetCell(new GridPosition(0, 1));
        Assert.Equal(string.Empty, cellB?.Value);

        // Move to cell "E"
        state.CursorPosition = new GridPosition(1, 1);

        // Paste with 'p'
        mode.HandleKey(state, Key.P, ModifierKeys.None, document);

        // Assert - cell "E" should now contain "B"
        var cellE = document.GetCell(new GridPosition(1, 1));
        Assert.Equal("B", cellE?.Value);
    }
}
