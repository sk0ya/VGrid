using System.Windows.Input;
using VGrid.VimEngine.Vimrc;
using Xunit;

namespace VGrid.Tests.VimEngine.Vimrc;

public class KeyNotationParserTests
{
    [Theory]
    [InlineData("j", Key.J, ModifierKeys.None)]
    [InlineData("J", Key.J, ModifierKeys.Shift)]
    [InlineData("a", Key.A, ModifierKeys.None)]
    [InlineData("A", Key.A, ModifierKeys.Shift)]
    [InlineData("z", Key.Z, ModifierKeys.None)]
    [InlineData("Z", Key.Z, ModifierKeys.Shift)]
    public void Parse_SingleLetter_ReturnsCorrectBinding(string notation, Key expectedKey, ModifierKeys expectedModifiers)
    {
        // Act
        var result = KeyNotationParser.Parse(notation);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedKey, result.Value.Key);
        Assert.Equal(expectedModifiers, result.Value.Modifiers);
    }

    [Theory]
    [InlineData("0", Key.D0)]
    [InlineData("1", Key.D1)]
    [InlineData("5", Key.D5)]
    [InlineData("9", Key.D9)]
    public void Parse_SingleDigit_ReturnsCorrectBinding(string notation, Key expectedKey)
    {
        // Act
        var result = KeyNotationParser.Parse(notation);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedKey, result.Value.Key);
        Assert.Equal(ModifierKeys.None, result.Value.Modifiers);
    }

    [Theory]
    [InlineData("<C-j>", Key.J, ModifierKeys.Control)]
    [InlineData("<C-J>", Key.J, ModifierKeys.Control)]
    [InlineData("<C-a>", Key.A, ModifierKeys.Control)]
    [InlineData("<C-z>", Key.Z, ModifierKeys.Control)]
    public void Parse_ControlModifier_ReturnsCorrectBinding(string notation, Key expectedKey, ModifierKeys expectedModifiers)
    {
        // Act
        var result = KeyNotationParser.Parse(notation);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedKey, result.Value.Key);
        Assert.Equal(expectedModifiers, result.Value.Modifiers);
    }

    [Theory]
    [InlineData("<S-j>", Key.J, ModifierKeys.Shift)]
    [InlineData("<A-j>", Key.J, ModifierKeys.Alt)]
    [InlineData("<M-j>", Key.J, ModifierKeys.Alt)]
    public void Parse_OtherModifiers_ReturnsCorrectBinding(string notation, Key expectedKey, ModifierKeys expectedModifiers)
    {
        // Act
        var result = KeyNotationParser.Parse(notation);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedKey, result.Value.Key);
        Assert.Equal(expectedModifiers, result.Value.Modifiers);
    }

    [Theory]
    [InlineData("<C-S-j>", Key.J, ModifierKeys.Control | ModifierKeys.Shift)]
    [InlineData("<C-A-j>", Key.J, ModifierKeys.Control | ModifierKeys.Alt)]
    [InlineData("<S-A-j>", Key.J, ModifierKeys.Shift | ModifierKeys.Alt)]
    public void Parse_MultipleModifiers_ReturnsCorrectBinding(string notation, Key expectedKey, ModifierKeys expectedModifiers)
    {
        // Act
        var result = KeyNotationParser.Parse(notation);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedKey, result.Value.Key);
        Assert.Equal(expectedModifiers, result.Value.Modifiers);
    }

    [Theory]
    [InlineData("<Space>", Key.Space)]
    [InlineData("<CR>", Key.Enter)]
    [InlineData("<Enter>", Key.Enter)]
    [InlineData("<Esc>", Key.Escape)]
    [InlineData("<Tab>", Key.Tab)]
    [InlineData("<BS>", Key.Back)]
    [InlineData("<Del>", Key.Delete)]
    public void Parse_SpecialKeys_ReturnsCorrectBinding(string notation, Key expectedKey)
    {
        // Act
        var result = KeyNotationParser.Parse(notation);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedKey, result.Value.Key);
        Assert.Equal(ModifierKeys.None, result.Value.Modifiers);
    }

    [Theory]
    [InlineData("<C-Space>", Key.Space, ModifierKeys.Control)]
    [InlineData("<S-Tab>", Key.Tab, ModifierKeys.Shift)]
    [InlineData("<A-Enter>", Key.Enter, ModifierKeys.Alt)]
    public void Parse_SpecialKeysWithModifiers_ReturnsCorrectBinding(string notation, Key expectedKey, ModifierKeys expectedModifiers)
    {
        // Act
        var result = KeyNotationParser.Parse(notation);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedKey, result.Value.Key);
        Assert.Equal(expectedModifiers, result.Value.Modifiers);
    }

    [Theory]
    [InlineData("<F1>", Key.F1)]
    [InlineData("<F5>", Key.F5)]
    [InlineData("<F12>", Key.F12)]
    public void Parse_FunctionKeys_ReturnsCorrectBinding(string notation, Key expectedKey)
    {
        // Act
        var result = KeyNotationParser.Parse(notation);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedKey, result.Value.Key);
        Assert.Equal(ModifierKeys.None, result.Value.Modifiers);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_EmptyOrNull_ReturnsNull(string? notation)
    {
        // Act
        var result = KeyNotationParser.Parse(notation!);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ToNotation_SimpleKey_ReturnsSimpleNotation()
    {
        // Arrange
        var binding = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.J, ModifierKeys.None);

        // Act
        var result = KeyNotationParser.ToNotation(binding);

        // Assert
        Assert.Equal("j", result);
    }

    [Fact]
    public void ToNotation_ControlKey_ReturnsAngleBracketNotation()
    {
        // Arrange
        var binding = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.J, ModifierKeys.Control);

        // Act
        var result = KeyNotationParser.ToNotation(binding);

        // Assert
        Assert.Equal("<C-j>", result);
    }

    [Fact]
    public void ToNotation_SpecialKey_ReturnsAngleBracketNotation()
    {
        // Arrange
        var binding = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.Space, ModifierKeys.None);

        // Act
        var result = KeyNotationParser.ToNotation(binding);

        // Assert
        Assert.Equal("<Space>", result);
    }
}
