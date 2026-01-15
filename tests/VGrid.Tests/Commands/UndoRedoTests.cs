using System.Windows.Input;
using VGrid.Commands;
using VGrid.Models;
using VGrid.VimEngine;
using Xunit;

namespace VGrid.Tests.Commands;

public class UndoRedoTests
{
    [Fact]
    public void InsertLine_CanUndo()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var commandHistory = new CommandHistory();
        var command = new InsertRowCommand(document, 1);

        // Act - Insert row
        commandHistory.Execute(command);

        // Assert - Row was inserted
        Assert.Equal(2, document.RowCount);

        // Act - Undo
        commandHistory.Undo();

        // Assert - Row was removed
        Assert.Equal(1, document.RowCount);
        Assert.Equal("A", document.Rows[0].Cells[0].Value);
    }

    [Fact]
    public void InsertLineWithNormalMode_CanUndo()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 0);

        var mode = new NormalMode();

        // Act - Insert line with 'o'
        mode.HandleKey(state, Key.O, ModifierKeys.None, document);

        // Assert - Row was inserted
        Assert.Equal(2, document.RowCount);

        // Act - Undo with 'u'
        state.SwitchMode(VimMode.Normal);
        mode.HandleKey(state, Key.U, ModifierKeys.None, document);

        // Assert - Row was removed
        Assert.Equal(1, document.RowCount);
        Assert.Equal("A", document.Rows[0].Cells[0].Value);
    }

    [Fact]
    public void Paste_CanUndo()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var commandHistory = new CommandHistory();
        var yank = new YankedContent
        {
            SourceType = VisualType.Line,
            Values = new string[,] { { "X", "Y", "Z" } },
            Rows = 1,
            Columns = 3
        };

        var command = new PasteCommand(document, new GridPosition(0, 0), yank);

        // Act - Paste (line-wise paste inserts below)
        commandHistory.Execute(command);

        // Assert - Row was inserted
        Assert.Equal(2, document.RowCount);
        Assert.Equal("X", document.Rows[1].Cells[0].Value);

        // Act - Undo
        commandHistory.Undo();

        // Assert - Pasted row was removed
        Assert.Equal(1, document.RowCount);
        Assert.Equal("A", document.Rows[0].Cells[0].Value);
    }

    [Fact]
    public void PasteCharacterWise_CanUndo()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var commandHistory = new CommandHistory();
        var yank = new YankedContent
        {
            SourceType = VisualType.Character,
            Values = new string[,] { { "X" } },
            Rows = 1,
            Columns = 1
        };

        var command = new PasteCommand(document, new GridPosition(0, 1), yank);

        // Act - Paste (character-wise paste overwrites)
        commandHistory.Execute(command);

        // Assert - Cell was overwritten
        Assert.Equal("X", document.Rows[0].Cells[1].Value);

        // Act - Undo
        commandHistory.Undo();

        // Assert - Cell was restored
        Assert.Equal("B", document.Rows[0].Cells[1].Value);
    }

    [Fact]
    public void PasteBefore_InsertsAboveCurrentRow()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var yank = new YankedContent
        {
            SourceType = VisualType.Line,
            Values = new string[,] { { "X", "Y", "Z" } },
            Rows = 1,
            Columns = 3
        };

        var command = new PasteCommand(document, new GridPosition(0, 0), yank, pasteBefore: true);

        // Act - Paste before (line-wise paste inserts above)
        command.Execute();

        // Assert - Row was inserted above
        Assert.Equal(2, document.RowCount);
        Assert.Equal("X", document.Rows[0].Cells[0].Value);
        Assert.Equal("A", document.Rows[1].Cells[0].Value);
    }

    [Fact]
    public void PasteBeforeBlock_InsertsLeftOfCurrentColumn()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var yank = new YankedContent
        {
            SourceType = VisualType.Block,
            Values = new string[,] { { "X" } },
            Rows = 1,
            Columns = 1
        };

        var command = new PasteCommand(document, new GridPosition(0, 1), yank, pasteBefore: true);

        // Act - Paste before (block-wise paste inserts to the left)
        command.Execute();

        // Assert - Column was inserted to the left
        Assert.Equal(4, document.ColumnCount);
        Assert.Equal("A", document.Rows[0].Cells[0].Value);
        Assert.Equal("X", document.Rows[0].Cells[1].Value);
        Assert.Equal("B", document.Rows[0].Cells[2].Value);
        Assert.Equal("C", document.Rows[0].Cells[3].Value);
    }

    [Fact]
    public void PasteBefore_CanUndo()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var commandHistory = new CommandHistory();
        var yank = new YankedContent
        {
            SourceType = VisualType.Line,
            Values = new string[,] { { "X", "Y", "Z" } },
            Rows = 1,
            Columns = 3
        };

        var command = new PasteCommand(document, new GridPosition(0, 0), yank, pasteBefore: true);

        // Act - Paste before
        commandHistory.Execute(command);

        // Assert - Row was inserted above
        Assert.Equal(2, document.RowCount);
        Assert.Equal("X", document.Rows[0].Cells[0].Value);

        // Act - Undo
        commandHistory.Undo();

        // Assert - Pasted row was removed
        Assert.Equal(1, document.RowCount);
        Assert.Equal("A", document.Rows[0].Cells[0].Value);
    }

    [Fact]
    public void Sort_CanUndo()
    {
        // Arrange
        var document = new TsvDocument();
        document.Rows.Add(new Row(0, new[] { "C", "3" }));
        document.Rows.Add(new Row(1, new[] { "A", "1" }));
        document.Rows.Add(new Row(2, new[] { "B", "2" }));

        var commandHistory = new CommandHistory();
        var command = new SortCommand(document, 0, ascending: true);

        // Act - Sort by column 0
        commandHistory.Execute(command);

        // Assert - Rows were sorted
        Assert.Equal("A", document.Rows[0].Cells[0].Value);
        Assert.Equal("B", document.Rows[1].Cells[0].Value);
        Assert.Equal("C", document.Rows[2].Cells[0].Value);

        // Act - Undo
        commandHistory.Undo();

        // Assert - Original order was restored
        Assert.Equal("C", document.Rows[0].Cells[0].Value);
        Assert.Equal("A", document.Rows[1].Cells[0].Value);
        Assert.Equal("B", document.Rows[2].Cells[0].Value);
    }

    [Fact]
    public void Redo_AfterUndo_RestoresChange()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var commandHistory = new CommandHistory();
        var command = new InsertRowCommand(document, 1);

        // Act - Insert, Undo, Redo
        commandHistory.Execute(command);
        Assert.Equal(2, document.RowCount);

        commandHistory.Undo();
        Assert.Equal(1, document.RowCount);

        commandHistory.Redo();

        // Assert - Row was re-inserted
        Assert.Equal(2, document.RowCount);
    }

    [Fact]
    public void RedoWithCtrlR_RestoresChange()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var state = new VimState();
        state.CommandHistory = new CommandHistory();
        state.CursorPosition = new GridPosition(0, 0);

        var mode = new NormalMode();

        // Act - Insert line with 'o'
        mode.HandleKey(state, Key.O, ModifierKeys.None, document);
        Assert.Equal(2, document.RowCount);

        // Act - Undo with 'u'
        state.SwitchMode(VimMode.Normal);
        mode.HandleKey(state, Key.U, ModifierKeys.None, document);
        Assert.Equal(1, document.RowCount);

        // Act - Redo with Ctrl+R
        mode.HandleKey(state, Key.R, ModifierKeys.Control, document);

        // Assert - Row was re-inserted
        Assert.Equal(2, document.RowCount);
    }

    [Fact]
    public void MultipleOperations_CanUndoAndRedo()
    {
        // Arrange
        var document = new TsvDocument();
        var row1 = new Row(0, new[] { "A", "B", "C" });
        document.Rows.Add(row1);

        var commandHistory = new CommandHistory();

        // Act - Execute multiple operations
        commandHistory.Execute(new InsertRowCommand(document, 1));
        Assert.Equal(2, document.RowCount);

        commandHistory.Execute(new EditCellCommand(document, new GridPosition(0, 0), "X"));
        Assert.Equal("X", document.Rows[0].Cells[0].Value);

        commandHistory.Execute(new DeleteRowCommand(document, 1));
        Assert.Equal(1, document.RowCount);

        // Act - Undo all operations
        commandHistory.Undo(); // Undo delete
        Assert.Equal(2, document.RowCount);

        commandHistory.Undo(); // Undo edit
        Assert.Equal("A", document.Rows[0].Cells[0].Value);

        commandHistory.Undo(); // Undo insert
        Assert.Equal(1, document.RowCount);

        // Act - Redo all operations
        commandHistory.Redo(); // Redo insert
        Assert.Equal(2, document.RowCount);

        commandHistory.Redo(); // Redo edit
        Assert.Equal("X", document.Rows[0].Cells[0].Value);

        commandHistory.Redo(); // Redo delete
        Assert.Equal(1, document.RowCount);
    }
}
