using System.Windows.Input;
using VGrid.VimEngine;
using VGrid.VimEngine.KeyBinding;
using Xunit;

namespace VGrid.Tests.VimEngine.KeyBinding;

public class KeyBindingConfigTests
{
    [Fact]
    public void AddBinding_NormalMode_AddsToNormalModeBindings()
    {
        // Arrange
        var config = new KeyBindingConfig();
        var binding = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.J, ModifierKeys.Control);

        // Act
        config.AddBinding(VimMode.Normal, binding, "move_down_10");

        // Assert
        Assert.Equal(1, config.GetBindingCount(VimMode.Normal));
        Assert.Equal(1, config.TotalBindingCount);
        Assert.True(config.TryGetAction(VimMode.Normal, binding, out var actionName));
        Assert.Equal("move_down_10", actionName);
    }

    [Fact]
    public void AddBinding_DifferentModes_AddsToDifferentCollections()
    {
        // Arrange
        var config = new KeyBindingConfig();
        var binding = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.J, ModifierKeys.Control);

        // Act
        config.AddBinding(VimMode.Normal, binding, "move_down_10");
        config.AddBinding(VimMode.Insert, binding, "move_down");
        config.AddBinding(VimMode.Visual, binding, "move_down_10");

        // Assert
        Assert.Equal(1, config.GetBindingCount(VimMode.Normal));
        Assert.Equal(1, config.GetBindingCount(VimMode.Insert));
        Assert.Equal(1, config.GetBindingCount(VimMode.Visual));
        Assert.Equal(0, config.GetBindingCount(VimMode.Command));
        Assert.Equal(3, config.TotalBindingCount);
    }

    [Fact]
    public void TryGetAction_BindingExists_ReturnsTrueAndActionName()
    {
        // Arrange
        var config = new KeyBindingConfig();
        var binding = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.Space, ModifierKeys.None);
        config.AddBinding(VimMode.Normal, binding, "move_right");

        // Act
        var found = config.TryGetAction(VimMode.Normal, binding, out var actionName);

        // Assert
        Assert.True(found);
        Assert.Equal("move_right", actionName);
    }

    [Fact]
    public void TryGetAction_BindingDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var config = new KeyBindingConfig();
        var binding = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.J, ModifierKeys.None);

        // Act
        var found = config.TryGetAction(VimMode.Normal, binding, out var actionName);

        // Assert
        Assert.False(found);
        Assert.Null(actionName);
    }

    [Fact]
    public void TryGetAction_WrongMode_ReturnsFalse()
    {
        // Arrange
        var config = new KeyBindingConfig();
        var binding = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.J, ModifierKeys.Control);
        config.AddBinding(VimMode.Normal, binding, "move_down_10");

        // Act
        var found = config.TryGetAction(VimMode.Insert, binding, out var actionName);

        // Assert
        Assert.False(found);
        Assert.Null(actionName);
    }

    [Fact]
    public void Clear_RemovesAllBindings()
    {
        // Arrange
        var config = new KeyBindingConfig();
        var binding = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.J, ModifierKeys.Control);
        config.AddBinding(VimMode.Normal, binding, "move_down_10");
        config.AddBinding(VimMode.Insert, binding, "move_down");

        // Act
        config.Clear();

        // Assert
        Assert.Equal(0, config.TotalBindingCount);
        Assert.Equal(0, config.GetBindingCount(VimMode.Normal));
        Assert.Equal(0, config.GetBindingCount(VimMode.Insert));
    }

    [Fact]
    public void GetBindingsForMode_ReturnsCorrectBindings()
    {
        // Arrange
        var config = new KeyBindingConfig();
        var binding1 = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.J, ModifierKeys.Control);
        var binding2 = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.K, ModifierKeys.Control);
        config.AddBinding(VimMode.Normal, binding1, "move_down_10");
        config.AddBinding(VimMode.Normal, binding2, "move_up_10");

        // Act
        var bindings = config.GetBindingsForMode(VimMode.Normal);

        // Assert
        Assert.Equal(2, bindings.Count);
        Assert.True(bindings.ContainsKey(binding1));
        Assert.True(bindings.ContainsKey(binding2));
    }

    [Fact]
    public void AddBinding_SameKeyTwice_OverwritesPreviousBinding()
    {
        // Arrange
        var config = new KeyBindingConfig();
        var binding = new VGrid.VimEngine.KeyBinding.KeyBinding(Key.J, ModifierKeys.None);

        // Act
        config.AddBinding(VimMode.Normal, binding, "move_down");
        config.AddBinding(VimMode.Normal, binding, "move_down_10");

        // Assert
        Assert.Equal(1, config.GetBindingCount(VimMode.Normal));
        Assert.True(config.TryGetAction(VimMode.Normal, binding, out var actionName));
        Assert.Equal("move_down_10", actionName);
    }
}
