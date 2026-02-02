using VGrid.Services;

namespace VGrid.Tests.Services;

public class TsvDelimiterStrategyTests
{
    private readonly TsvDelimiterStrategy _strategy = new();

    [Fact]
    public void ParseLine_SplitsByTab()
    {
        var result = _strategy.ParseLine("a\tb\tc");
        Assert.Equal(new[] { "a", "b", "c" }, result);
    }

    [Fact]
    public void ParseLine_SingleField()
    {
        var result = _strategy.ParseLine("hello");
        Assert.Equal(new[] { "hello" }, result);
    }

    [Fact]
    public void FormatLine_JoinsWithTab()
    {
        var result = _strategy.FormatLine(new[] { "a", "b", "c" });
        Assert.Equal("a\tb\tc", result);
    }

    [Fact]
    public void ParseContent_MultipleLines()
    {
        var result = _strategy.ParseContent("a\tb\nc\td\n");
        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "a", "b" }, result[0]);
        Assert.Equal(new[] { "c", "d" }, result[1]);
    }

    [Fact]
    public void ParseContent_EmptyString()
    {
        var result = _strategy.ParseContent("");
        Assert.Empty(result);
    }
}

public class CsvDelimiterStrategyTests
{
    private readonly CsvDelimiterStrategy _strategy = new();

    [Fact]
    public void ParseLine_SimpleFields()
    {
        var result = _strategy.ParseLine("a,b,c");
        Assert.Equal(new[] { "a", "b", "c" }, result);
    }

    [Fact]
    public void ParseLine_QuotedFieldWithComma()
    {
        var result = _strategy.ParseLine("a,\"b,c\",d");
        Assert.Equal(new[] { "a", "b,c", "d" }, result);
    }

    [Fact]
    public void ParseLine_QuotedFieldWithDoubleQuote()
    {
        var result = _strategy.ParseLine("a,\"b\"\"c\",d");
        Assert.Equal(new[] { "a", "b\"c", "d" }, result);
    }

    [Fact]
    public void ParseLine_EmptyFields()
    {
        var result = _strategy.ParseLine(",a,,b,");
        Assert.Equal(new[] { "", "a", "", "b", "" }, result);
    }

    [Fact]
    public void ParseLine_QuotedEmptyField()
    {
        var result = _strategy.ParseLine("a,\"\",b");
        Assert.Equal(new[] { "a", "", "b" }, result);
    }

    [Fact]
    public void FormatLine_SimpleFields()
    {
        var result = _strategy.FormatLine(new[] { "a", "b", "c" });
        Assert.Equal("a,b,c", result);
    }

    [Fact]
    public void FormatLine_FieldWithComma_IsQuoted()
    {
        var result = _strategy.FormatLine(new[] { "a", "b,c", "d" });
        Assert.Equal("a,\"b,c\",d", result);
    }

    [Fact]
    public void FormatLine_FieldWithQuote_IsEscaped()
    {
        var result = _strategy.FormatLine(new[] { "a", "b\"c", "d" });
        Assert.Equal("a,\"b\"\"c\",d", result);
    }

    [Fact]
    public void FormatLine_FieldWithNewline_IsQuoted()
    {
        var result = _strategy.FormatLine(new[] { "a", "b\nc", "d" });
        Assert.Equal("a,\"b\nc\",d", result);
    }

    [Fact]
    public void FormatLine_EmptyField_NotQuoted()
    {
        var result = _strategy.FormatLine(new[] { "a", "", "b" });
        Assert.Equal("a,,b", result);
    }

    [Fact]
    public void ParseContent_SimpleRows()
    {
        var result = _strategy.ParseContent("a,b\nc,d\n");
        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "a", "b" }, result[0]);
        Assert.Equal(new[] { "c", "d" }, result[1]);
    }

    [Fact]
    public void ParseContent_QuotedFieldWithNewline()
    {
        var result = _strategy.ParseContent("a,\"b\nc\",d\ne,f,g\n");
        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "a", "b\nc", "d" }, result[0]);
        Assert.Equal(new[] { "e", "f", "g" }, result[1]);
    }

    [Fact]
    public void ParseContent_CrLfLineEndings()
    {
        var result = _strategy.ParseContent("a,b\r\nc,d\r\n");
        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "a", "b" }, result[0]);
        Assert.Equal(new[] { "c", "d" }, result[1]);
    }

    [Fact]
    public void ParseContent_EmptyString()
    {
        var result = _strategy.ParseContent("");
        Assert.Empty(result);
    }

    [Fact]
    public void ParseContent_QuotedFieldWithCrLf()
    {
        var result = _strategy.ParseContent("a,\"b\r\nc\",d\r\ne,f,g");
        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "a", "b\r\nc", "d" }, result[0]);
        Assert.Equal(new[] { "e", "f", "g" }, result[1]);
    }

    [Fact]
    public void Roundtrip_PreservesData()
    {
        var original = new[] { "hello", "world,test", "a\"b", "line\nbreak" };
        var formatted = _strategy.FormatLine(original);
        var parsed = _strategy.ParseLine(formatted);
        Assert.Equal(original, parsed);
    }
}

public class DelimiterStrategyFactoryTests
{
    [Theory]
    [InlineData("file.csv", DelimiterFormat.Csv)]
    [InlineData("file.CSV", DelimiterFormat.Csv)]
    [InlineData("file.tsv", DelimiterFormat.Tsv)]
    [InlineData("file.txt", DelimiterFormat.Tsv)]
    [InlineData("file.tab", DelimiterFormat.Tsv)]
    [InlineData("file.unknown", DelimiterFormat.Tsv)]
    public void DetectFromExtension_ReturnsCorrectFormat(string filePath, DelimiterFormat expected)
    {
        Assert.Equal(expected, DelimiterStrategyFactory.DetectFromExtension(filePath));
    }

    [Fact]
    public void Create_Csv_ReturnsCsvStrategy()
    {
        var strategy = DelimiterStrategyFactory.Create(DelimiterFormat.Csv);
        Assert.IsType<CsvDelimiterStrategy>(strategy);
        Assert.Equal(',', strategy.Delimiter);
    }

    [Fact]
    public void Create_Tsv_ReturnsTsvStrategy()
    {
        var strategy = DelimiterStrategyFactory.Create(DelimiterFormat.Tsv);
        Assert.IsType<TsvDelimiterStrategy>(strategy);
        Assert.Equal('\t', strategy.Delimiter);
    }

}
