using VGrid.VimEngine;

namespace VGrid.Tests.VimEngine;

public class ClipboardHelperMarkdownTests
{
    [Fact]
    public void ConvertToMarkdownTable_UsesFirstRowAsHeader()
    {
        var yank = new YankedContent
        {
            Values = new[,]
            {
                { "Name", "Age" },
                { "Alice", "30" },
                { "Bob", "25" }
            },
            SourceType = VisualType.Character,
            Rows = 3,
            Columns = 2
        };

        var result = ClipboardHelper.ConvertToMarkdownTable(yank);

        Assert.Equal(
            "| Name | Age |\r\n" +
            "| --- | --- |\r\n" +
            "| Alice | 30 |\r\n" +
            "| Bob | 25 |\r\n",
            result);
    }

    [Fact]
    public void ConvertToMarkdownTable_EscapesPipesBackslashesAndNewlines()
    {
        var yank = new YankedContent
        {
            Values = new[,]
            {
                { "A|B", "Path" },
                { "Line1\nLine2", @"C:\Temp" }
            },
            SourceType = VisualType.Character,
            Rows = 2,
            Columns = 2
        };

        var result = ClipboardHelper.ConvertToMarkdownTable(yank);

        Assert.Equal(
            "| A\\|B | Path |\r\n" +
            "| --- | --- |\r\n" +
            "| Line1<br>Line2 | C:\\\\Temp |\r\n",
            result);
    }
}
