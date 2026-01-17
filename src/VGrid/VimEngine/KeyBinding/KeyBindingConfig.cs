namespace VGrid.VimEngine.KeyBinding;

/// <summary>
/// Configuration for custom keybindings loaded from .vimrc
/// </summary>
public class KeyBindingConfig
{
    /// <summary>
    /// Normal mode keybindings
    /// </summary>
    private readonly Dictionary<KeyBinding, string> _normalModeBindings = new();

    /// <summary>
    /// Insert mode keybindings
    /// </summary>
    private readonly Dictionary<KeyBinding, string> _insertModeBindings = new();

    /// <summary>
    /// Visual mode keybindings
    /// </summary>
    private readonly Dictionary<KeyBinding, string> _visualModeBindings = new();

    /// <summary>
    /// Command mode keybindings
    /// </summary>
    private readonly Dictionary<KeyBinding, string> _commandModeBindings = new();

    /// <summary>
    /// Gets the keybindings for a specific mode
    /// </summary>
    public IReadOnlyDictionary<KeyBinding, string> GetBindingsForMode(VimMode mode)
    {
        return mode switch
        {
            VimMode.Normal => _normalModeBindings,
            VimMode.Insert => _insertModeBindings,
            VimMode.Visual => _visualModeBindings,
            VimMode.Command => _commandModeBindings,
            _ => new Dictionary<KeyBinding, string>()
        };
    }

    /// <summary>
    /// Adds a keybinding for a specific mode
    /// </summary>
    /// <param name="mode">The Vim mode</param>
    /// <param name="binding">The key binding</param>
    /// <param name="actionName">The action name to execute</param>
    public void AddBinding(VimMode mode, KeyBinding binding, string actionName)
    {
        var bindings = GetMutableBindingsForMode(mode);
        bindings[binding] = actionName;
    }

    /// <summary>
    /// Tries to get an action name for a keybinding in a specific mode
    /// </summary>
    /// <param name="mode">The Vim mode</param>
    /// <param name="binding">The key binding</param>
    /// <param name="actionName">The action name if found</param>
    /// <returns>True if a binding was found</returns>
    public bool TryGetAction(VimMode mode, KeyBinding binding, out string? actionName)
    {
        var bindings = GetBindingsForMode(mode);
        if (bindings.TryGetValue(binding, out var action))
        {
            actionName = action;
            return true;
        }

        actionName = null;
        return false;
    }

    /// <summary>
    /// Clears all bindings
    /// </summary>
    public void Clear()
    {
        _normalModeBindings.Clear();
        _insertModeBindings.Clear();
        _visualModeBindings.Clear();
        _commandModeBindings.Clear();
    }

    /// <summary>
    /// Gets the number of bindings for a specific mode
    /// </summary>
    public int GetBindingCount(VimMode mode)
    {
        return GetBindingsForMode(mode).Count;
    }

    /// <summary>
    /// Gets the total number of bindings across all modes
    /// </summary>
    public int TotalBindingCount =>
        _normalModeBindings.Count +
        _insertModeBindings.Count +
        _visualModeBindings.Count +
        _commandModeBindings.Count;

    private Dictionary<KeyBinding, string> GetMutableBindingsForMode(VimMode mode)
    {
        return mode switch
        {
            VimMode.Normal => _normalModeBindings,
            VimMode.Insert => _insertModeBindings,
            VimMode.Visual => _visualModeBindings,
            VimMode.Command => _commandModeBindings,
            _ => throw new ArgumentException($"Unknown mode: {mode}", nameof(mode))
        };
    }
}
