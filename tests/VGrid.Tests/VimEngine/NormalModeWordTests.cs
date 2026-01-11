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
}
