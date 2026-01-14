using VGrid.Commands;
using VGrid.Models;
using VGrid.VimEngine;
using Xunit;

namespace VGrid.Tests.VimEngine;

public class VisualModePasteTests
{
    [Fact]
    public void PasteOverCharacterSelection_SingleCell_FillsAllSelectedCells()
    {
        // Arrange
        var document = new TsvDocument();
        document.Rows.Add(new Row(0, new[] { "A", "B", "C" }));
        document.Rows.Add(new Row(1, new[] { "D", "E", "F" }));
        document.Rows.Add(new Row(2, new[] { "G", "H", "I" }));

        var vimState = new VimState();
        var commandHistory = new CommandHistory();
        vimState.CommandHistory = commandHistory;

        // Yank a single cell value "X"
        vimState.LastYank = new YankedContent
        {
            Values = new string[,] { { "X" } },
            SourceType = VisualType.Character,
            Rows = 1,
            Columns = 1
        };

        // Enter visual mode and select a 2x2 range (cells at 0,0 to 1,1)
        vimState.SwitchMode(VimMode.Visual);
        vimState.CursorPosition = new GridPosition(0, 0);

        var visualMode = new VisualMode();
        visualMode.OnEnter(vimState);

        // Move to create selection (to row 1, col 1)
        vimState.CursorPosition = new GridPosition(1, 1);

        // Create the selection manually
        vimState.CurrentSelection = new SelectionRange(
            VisualType.Character,
            new GridPosition(0, 0),
            new GridPosition(1, 1));

        // Act - Paste over selection
        bool result = visualMode.HandleKey(vimState, System.Windows.Input.Key.P, System.Windows.Input.ModifierKeys.None, document);

        // Assert
        Assert.True(result);
        Assert.Equal(VimMode.Normal, vimState.CurrentMode);

        // All 4 cells in the 2x2 selection should now be "X"
        Assert.Equal("X", document.GetCell(0, 0)!.Value);
        Assert.Equal("X", document.GetCell(0, 1)!.Value);
        Assert.Equal("X", document.GetCell(1, 0)!.Value);
        Assert.Equal("X", document.GetCell(1, 1)!.Value);

        // Other cells should be unchanged
        Assert.Equal("C", document.GetCell(0, 2)!.Value);
        Assert.Equal("F", document.GetCell(1, 2)!.Value);
    }

    [Fact]
    public void PasteOverCharacterSelection_CtrlV_FillsAllSelectedCells()
    {
        // Arrange
        var document = new TsvDocument();
        document.Rows.Add(new Row(0, new[] { "A", "B", "C" }));
        document.Rows.Add(new Row(1, new[] { "D", "E", "F" }));
        document.Rows.Add(new Row(2, new[] { "G", "H", "I" }));

        var vimState = new VimState();
        var commandHistory = new CommandHistory();
        vimState.CommandHistory = commandHistory;

        // Yank a single cell value "Y"
        vimState.LastYank = new YankedContent
        {
            Values = new string[,] { { "Y" } },
            SourceType = VisualType.Character,
            Rows = 1,
            Columns = 1
        };

        // Enter visual mode and select cells
        vimState.SwitchMode(VimMode.Visual);
        vimState.CursorPosition = new GridPosition(0, 1);

        var visualMode = new VisualMode();
        visualMode.OnEnter(vimState);

        vimState.CursorPosition = new GridPosition(2, 2);
        vimState.CurrentSelection = new SelectionRange(
            VisualType.Character,
            new GridPosition(0, 1),
            new GridPosition(2, 2));

        // Act - Paste over selection with Ctrl+V
        bool result = visualMode.HandleKey(vimState, System.Windows.Input.Key.V, System.Windows.Input.ModifierKeys.Control, document);

        // Assert
        Assert.True(result);
        Assert.Equal(VimMode.Normal, vimState.CurrentMode);

        // All cells in the selection should now be "Y"
        Assert.Equal("Y", document.GetCell(0, 1)!.Value);
        Assert.Equal("Y", document.GetCell(0, 2)!.Value);
        Assert.Equal("Y", document.GetCell(1, 1)!.Value);
        Assert.Equal("Y", document.GetCell(1, 2)!.Value);
        Assert.Equal("Y", document.GetCell(2, 1)!.Value);
        Assert.Equal("Y", document.GetCell(2, 2)!.Value);

        // First column should be unchanged
        Assert.Equal("A", document.GetCell(0, 0)!.Value);
        Assert.Equal("D", document.GetCell(1, 0)!.Value);
        Assert.Equal("G", document.GetCell(2, 0)!.Value);
    }

    [Fact]
    public void PasteOverCharacterSelection_MultiCellPattern_RepeatsPattern()
    {
        // Arrange
        var document = new TsvDocument();
        document.Rows.Add(new Row(0, new[] { "A", "B", "C", "D" }));
        document.Rows.Add(new Row(1, new[] { "E", "F", "G", "H" }));
        document.Rows.Add(new Row(2, new[] { "I", "J", "K", "L" }));

        var vimState = new VimState();
        var commandHistory = new CommandHistory();
        vimState.CommandHistory = commandHistory;

        // Yank a 2x2 pattern
        vimState.LastYank = new YankedContent
        {
            Values = new string[,] { { "1", "2" }, { "3", "4" } },
            SourceType = VisualType.Character,
            Rows = 2,
            Columns = 2
        };

        // Enter visual mode and select a 3x3 range
        vimState.SwitchMode(VimMode.Visual);
        vimState.CursorPosition = new GridPosition(0, 0);

        var visualMode = new VisualMode();
        visualMode.OnEnter(vimState);

        vimState.CursorPosition = new GridPosition(2, 2);
        vimState.CurrentSelection = new SelectionRange(
            VisualType.Character,
            new GridPosition(0, 0),
            new GridPosition(2, 2));

        // Act - Paste over selection
        bool result = visualMode.HandleKey(vimState, System.Windows.Input.Key.P, System.Windows.Input.ModifierKeys.None, document);

        // Assert
        Assert.True(result);

        // The 2x2 pattern should repeat to fill the 3x3 selection
        Assert.Equal("1", document.GetCell(0, 0)!.Value);
        Assert.Equal("2", document.GetCell(0, 1)!.Value);
        Assert.Equal("1", document.GetCell(0, 2)!.Value); // Pattern repeats

        Assert.Equal("3", document.GetCell(1, 0)!.Value);
        Assert.Equal("4", document.GetCell(1, 1)!.Value);
        Assert.Equal("3", document.GetCell(1, 2)!.Value); // Pattern repeats

        Assert.Equal("1", document.GetCell(2, 0)!.Value); // Pattern repeats
        Assert.Equal("2", document.GetCell(2, 1)!.Value); // Pattern repeats
        Assert.Equal("1", document.GetCell(2, 2)!.Value); // Pattern repeats

        // Fourth column should be unchanged
        Assert.Equal("D", document.GetCell(0, 3)!.Value);
    }

    [Fact]
    public void PasteOverSelection_CanUndo()
    {
        // Arrange
        var document = new TsvDocument();
        document.Rows.Add(new Row(0, new[] { "A", "B" }));
        document.Rows.Add(new Row(1, new[] { "C", "D" }));

        var vimState = new VimState();
        var commandHistory = new CommandHistory();
        vimState.CommandHistory = commandHistory;

        vimState.LastYank = new YankedContent
        {
            Values = new string[,] { { "X" } },
            SourceType = VisualType.Character,
            Rows = 1,
            Columns = 1
        };

        vimState.SwitchMode(VimMode.Visual);
        vimState.CursorPosition = new GridPosition(0, 0);

        var visualMode = new VisualMode();
        visualMode.OnEnter(vimState);

        vimState.CursorPosition = new GridPosition(1, 1);
        vimState.CurrentSelection = new SelectionRange(
            VisualType.Character,
            new GridPosition(0, 0),
            new GridPosition(1, 1));

        // Act - Paste and then undo
        visualMode.HandleKey(vimState, System.Windows.Input.Key.P, System.Windows.Input.ModifierKeys.None, document);

        Assert.Equal("X", document.GetCell(0, 0)!.Value);
        Assert.Equal("X", document.GetCell(1, 1)!.Value);

        commandHistory.Undo();

        // Assert - Original values restored
        Assert.Equal("A", document.GetCell(0, 0)!.Value);
        Assert.Equal("B", document.GetCell(0, 1)!.Value);
        Assert.Equal("C", document.GetCell(1, 0)!.Value);
        Assert.Equal("D", document.GetCell(1, 1)!.Value);
    }

    [Fact]
    public void PasteOverLineSelection_FillsAllCellsInRows()
    {
        // Arrange
        var document = new TsvDocument();
        document.Rows.Add(new Row(0, new[] { "A", "B", "C" }));
        document.Rows.Add(new Row(1, new[] { "D", "E", "F" }));
        document.Rows.Add(new Row(2, new[] { "G", "H", "I" }));

        var vimState = new VimState();
        var commandHistory = new CommandHistory();
        vimState.CommandHistory = commandHistory;

        vimState.LastYank = new YankedContent
        {
            Values = new string[,] { { "X" } },
            SourceType = VisualType.Character,
            Rows = 1,
            Columns = 1
        };

        // Create selection first, before OnEnter
        vimState.CurrentSelection = new SelectionRange(
            VisualType.Line,
            new GridPosition(0, 0),
            new GridPosition(1, 0));

        vimState.SwitchMode(VimMode.Visual);
        vimState.CursorPosition = new GridPosition(0, 0);

        var visualMode = new VisualMode();
        visualMode.OnEnter(vimState);

        vimState.CursorPosition = new GridPosition(1, 0);

        // Act - Paste over line selection
        bool result = visualMode.HandleKey(vimState, System.Windows.Input.Key.P, System.Windows.Input.ModifierKeys.None, document);

        // Assert
        Assert.True(result);

        // First two rows should be filled with "X"
        Assert.Equal("X", document.GetCell(0, 0)!.Value);
        Assert.Equal("X", document.GetCell(0, 1)!.Value);
        Assert.Equal("X", document.GetCell(0, 2)!.Value);
        Assert.Equal("X", document.GetCell(1, 0)!.Value);
        Assert.Equal("X", document.GetCell(1, 1)!.Value);
        Assert.Equal("X", document.GetCell(1, 2)!.Value);

        // Third row should be unchanged
        Assert.Equal("G", document.GetCell(2, 0)!.Value);
        Assert.Equal("H", document.GetCell(2, 1)!.Value);
        Assert.Equal("I", document.GetCell(2, 2)!.Value);
    }

    [Fact]
    public void PasteOverBlockSelection_FillsAllCellsInColumns()
    {
        // Arrange
        var document = new TsvDocument();
        document.Rows.Add(new Row(0, new[] { "A", "B", "C" }));
        document.Rows.Add(new Row(1, new[] { "D", "E", "F" }));
        document.Rows.Add(new Row(2, new[] { "G", "H", "I" }));

        var vimState = new VimState();
        var commandHistory = new CommandHistory();
        vimState.CommandHistory = commandHistory;

        vimState.LastYank = new YankedContent
        {
            Values = new string[,] { { "Z" } },
            SourceType = VisualType.Character,
            Rows = 1,
            Columns = 1
        };

        // Create selection first, before OnEnter
        vimState.CurrentSelection = new SelectionRange(
            VisualType.Block,
            new GridPosition(0, 1),
            new GridPosition(0, 2));

        vimState.SwitchMode(VimMode.Visual);
        vimState.CursorPosition = new GridPosition(0, 1);

        var visualMode = new VisualMode();
        visualMode.OnEnter(vimState);

        vimState.CursorPosition = new GridPosition(0, 2);

        // Act - Paste over block selection
        bool result = visualMode.HandleKey(vimState, System.Windows.Input.Key.P, System.Windows.Input.ModifierKeys.None, document);

        // Assert
        Assert.True(result);

        // Columns 1 and 2 should be filled with "Z"
        Assert.Equal("Z", document.GetCell(0, 1)!.Value);
        Assert.Equal("Z", document.GetCell(0, 2)!.Value);
        Assert.Equal("Z", document.GetCell(1, 1)!.Value);
        Assert.Equal("Z", document.GetCell(1, 2)!.Value);
        Assert.Equal("Z", document.GetCell(2, 1)!.Value);
        Assert.Equal("Z", document.GetCell(2, 2)!.Value);

        // First column should be unchanged
        Assert.Equal("A", document.GetCell(0, 0)!.Value);
        Assert.Equal("D", document.GetCell(1, 0)!.Value);
        Assert.Equal("G", document.GetCell(2, 0)!.Value);
    }
}
