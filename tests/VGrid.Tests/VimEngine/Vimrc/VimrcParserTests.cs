using System.Windows.Input;
using VGrid.VimEngine;
using VGrid.VimEngine.Vimrc;
using Xunit;

namespace VGrid.Tests.VimEngine.Vimrc;

public class VimrcParserTests
{
    [Fact]
    public void Parse_EmptyContent_ReturnsEmptyConfig()
    {
        // Arrange
        var parser = new VimrcParser();

        // Act
        var result = parser.Parse("");

        // Assert
        Assert.NotNull(result.Config);
        Assert.Equal(0, result.Config.TotalBindingCount);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Parse_CommentOnly_ReturnsEmptyConfig()
    {
        // Arrange
        var parser = new VimrcParser();
        var content = @"
"" This is a comment
"" Another comment
";

        // Act
        var result = parser.Parse(content);

        // Assert
        Assert.NotNull(result.Config);
        Assert.Equal(0, result.Config.TotalBindingCount);
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Parse_SimpleNmap_CreatesNormalModeBinding()
    {
        // Arrange
        var parser = new VimrcParser();
        var content = "nmap j move_down";

        // Act
        var result = parser.Parse(content);

        // Assert
        Assert.NotNull(result.Config);
        Assert.Equal(1, result.Config.TotalBindingCount);
        Assert.Equal(1, result.Config.GetBindingCount(VimMode.Normal));
        Assert.False(result.HasErrors);

        var binding = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.J, ModifierKeys.None);
        Assert.True(result.Config.TryGetAction(VimMode.Normal, binding, out var actionName));
        Assert.Equal("move_down", actionName);
    }

    [Fact]
    public void Parse_SimpleImap_CreatesInsertModeBinding()
    {
        // Arrange
        var parser = new VimrcParser();
        var content = "imap <C-n> move_down";

        // Act
        var result = parser.Parse(content);

        // Assert
        Assert.NotNull(result.Config);
        Assert.Equal(1, result.Config.TotalBindingCount);
        Assert.Equal(1, result.Config.GetBindingCount(VimMode.Insert));
        Assert.False(result.HasErrors);

        var binding = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.N, ModifierKeys.Control);
        Assert.True(result.Config.TryGetAction(VimMode.Insert, binding, out var actionName));
        Assert.Equal("move_down", actionName);
    }

    [Fact]
    public void Parse_SimpleVmap_CreatesVisualModeBinding()
    {
        // Arrange
        var parser = new VimrcParser();
        var content = "vmap d delete_selection";

        // Act
        var result = parser.Parse(content);

        // Assert
        Assert.NotNull(result.Config);
        Assert.Equal(1, result.Config.TotalBindingCount);
        Assert.Equal(1, result.Config.GetBindingCount(VimMode.Visual));
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Parse_Map_CreatesNormalAndVisualModeBindings()
    {
        // Arrange
        var parser = new VimrcParser();
        var content = "map j move_down";

        // Act
        var result = parser.Parse(content);

        // Assert
        Assert.NotNull(result.Config);
        Assert.Equal(2, result.Config.TotalBindingCount);
        Assert.Equal(1, result.Config.GetBindingCount(VimMode.Normal));
        Assert.Equal(1, result.Config.GetBindingCount(VimMode.Visual));
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Parse_MultipleBindings_CreatesAllBindings()
    {
        // Arrange
        var parser = new VimrcParser();
        var content = @"
nmap <C-j> move_down_10
nmap <C-k> move_up_10
nmap <Space>w save_file
";

        // Act
        var result = parser.Parse(content);

        // Assert
        Assert.NotNull(result.Config);
        Assert.Equal(3, result.Config.TotalBindingCount);
        Assert.Equal(3, result.Config.GetBindingCount(VimMode.Normal));
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Parse_MixedModesAndComments_WorksCorrectly()
    {
        // Arrange
        var parser = new VimrcParser();
        var content = @"
"" Normal mode mappings
nmap j move_down
nmap k move_up

"" Insert mode mappings
imap <C-n> move_down
imap <C-p> move_up

"" Visual mode mappings
vmap d delete_selection
";

        // Act
        var result = parser.Parse(content);

        // Assert
        Assert.NotNull(result.Config);
        Assert.Equal(5, result.Config.TotalBindingCount);
        Assert.Equal(2, result.Config.GetBindingCount(VimMode.Normal));
        Assert.Equal(2, result.Config.GetBindingCount(VimMode.Insert));
        Assert.Equal(1, result.Config.GetBindingCount(VimMode.Visual));
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void Parse_InlineComment_StripsCommentAndCreatesBinding()
    {
        // Arrange
        var parser = new VimrcParser();
        var content = @"nmap <C-j> move_down_10 "" Ctrl+j moves down 10";

        // Act
        var result = parser.Parse(content);

        // Assert
        Assert.NotNull(result.Config);
        Assert.Equal(1, result.Config.TotalBindingCount);
        Assert.False(result.HasErrors);

        var binding = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.J, ModifierKeys.Control);
        Assert.True(result.Config.TryGetAction(VimMode.Normal, binding, out var actionName));
        Assert.Equal("move_down_10", actionName);
    }

    [Fact]
    public void Parse_SpecialKeys_WorksCorrectly()
    {
        // Arrange
        var parser = new VimrcParser();
        var content = @"
nmap <Space> move_right
nmap <CR> switch_to_insert
nmap <Tab> move_to_next_word
";

        // Act
        var result = parser.Parse(content);

        // Assert
        Assert.NotNull(result.Config);
        Assert.Equal(3, result.Config.TotalBindingCount);
        Assert.False(result.HasErrors);

        var spaceBinding = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.Space, ModifierKeys.None);
        Assert.True(result.Config.TryGetAction(VimMode.Normal, spaceBinding, out var spaceAction));
        Assert.Equal("move_right", spaceAction);

        var enterBinding = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.Enter, ModifierKeys.None);
        Assert.True(result.Config.TryGetAction(VimMode.Normal, enterBinding, out var enterAction));
        Assert.Equal("switch_to_insert", enterAction);
    }

    [Fact]
    public void Parse_OverrideBinding_UsesLatestBinding()
    {
        // Arrange
        var parser = new VimrcParser();
        var content = @"
nmap j move_down
nmap j move_down_10
";

        // Act
        var result = parser.Parse(content);

        // Assert
        Assert.NotNull(result.Config);
        Assert.Equal(1, result.Config.TotalBindingCount);
        Assert.False(result.HasErrors);

        var binding = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.J, ModifierKeys.None);
        Assert.True(result.Config.TryGetAction(VimMode.Normal, binding, out var actionName));
        Assert.Equal("move_down_10", actionName);
    }

    [Fact]
    public void Parse_KeyToKeyMapping_ResolvesToDefaultAction()
    {
        // Arrange
        var parser = new VimrcParser();
        // Map 'h' to behave like 'k' (which is move_up by default)
        var content = "nmap h k";

        // Act
        var result = parser.Parse(content);

        // Assert
        Assert.NotNull(result.Config);
        Assert.Equal(1, result.Config.TotalBindingCount);
        Assert.False(result.HasErrors);

        var binding = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.H, ModifierKeys.None);
        Assert.True(result.Config.TryGetAction(VimMode.Normal, binding, out var actionName));
        // 'k' should resolve to 'move_up'
        Assert.Equal("move_up", actionName);
    }

    [Fact]
    public void Parse_KeyToKeyMappingWithShift_ResolvesToCorrectAction()
    {
        // Arrange
        var parser = new VimrcParser();
        // Map 'j' to behave like 'K' (Shift+K, which is move_up_10 by default)
        var content = "nmap j K";

        // Act
        var result = parser.Parse(content);

        // Assert
        Assert.NotNull(result.Config);
        Assert.Equal(1, result.Config.TotalBindingCount);
        Assert.False(result.HasErrors);

        var binding = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.J, ModifierKeys.None);
        Assert.True(result.Config.TryGetAction(VimMode.Normal, binding, out var actionName));
        // 'K' (Shift+K) should resolve to 'move_up_10'
        Assert.Equal("move_up_10", actionName);
    }

    [Fact]
    public void Parse_KeyToKeyMappingWithCtrl_ResolvesToCorrectAction()
    {
        // Arrange
        var parser = new VimrcParser();
        // Map 'r' to behave like '<C-r>' (Ctrl+R, which is redo by default)
        var content = "nmap r <C-r>";

        // Act
        var result = parser.Parse(content);

        // Assert
        Assert.NotNull(result.Config);
        Assert.Equal(1, result.Config.TotalBindingCount);
        Assert.False(result.HasErrors);

        var binding = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.R, ModifierKeys.None);
        Assert.True(result.Config.TryGetAction(VimMode.Normal, binding, out var actionName));
        // '<C-r>' should resolve to 'redo'
        Assert.Equal("redo", actionName);
    }

    [Fact]
    public void Parse_UnknownKeyMapping_KeepsOriginalValue()
    {
        // Arrange
        var parser = new VimrcParser();
        // Map 'z' to 'q' which is not a default binding, so it stays as 'q'
        var content = "nmap z q";

        // Act
        var result = parser.Parse(content);

        // Assert
        Assert.NotNull(result.Config);
        Assert.Equal(1, result.Config.TotalBindingCount);
        Assert.False(result.HasErrors);

        var binding = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.Z, ModifierKeys.None);
        Assert.True(result.Config.TryGetAction(VimMode.Normal, binding, out var actionName));
        // 'q' is not a known default binding, so it stays as 'q'
        Assert.Equal("q", actionName);
    }
}
