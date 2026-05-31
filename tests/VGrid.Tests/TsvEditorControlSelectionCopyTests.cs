using VGrid.Commands;
using VGrid.Editor;
using VGrid.Models;
using VGrid.ViewModels;
using VGrid.VimEngine;

namespace VGrid.Tests;

public class TsvEditorControlSelectionCopyTests
{
    [Fact]
    public void CreateYankedContentFromCurrentSelection_ExpandsLineSelectionToFullRowWidth()
    {
        var tab = CreateTab(new[]
        {
            new[] { "A1", "B1", "C1" },
            new[] { "A2", "B2", "C2" }
        });
        tab.VimState.CurrentSelection = new SelectionRange(
            VisualType.Line,
            new GridPosition(0, 1),
            new GridPosition(1, 1));

        var yank = TsvEditorControl.CreateYankedContentFromCurrentSelection(tab);

        Assert.NotNull(yank);
        Assert.Equal(VisualType.Line, yank.SourceType);
        Assert.Equal(2, yank.Rows);
        Assert.Equal(3, yank.Columns);
        Assert.Equal("A1", yank.Values[0, 0]);
        Assert.Equal("B1", yank.Values[0, 1]);
        Assert.Equal("C1", yank.Values[0, 2]);
        Assert.Equal("A2", yank.Values[1, 0]);
        Assert.Equal("B2", yank.Values[1, 1]);
        Assert.Equal("C2", yank.Values[1, 2]);
    }

    private static TabItemViewModel CreateTab(IEnumerable<IEnumerable<string>> rows)
    {
        var document = new TsvDocument(rows.Select((values, index) => new Row(index, values)));
        var commandHistory = new CommandHistory();
        var vimState = new VimState { CommandHistory = commandHistory };
        var gridViewModel = new TsvGridViewModel(commandHistory)
        {
            Document = document
        };

        return new TabItemViewModel("test.tsv", document, vimState, gridViewModel);
    }
}
