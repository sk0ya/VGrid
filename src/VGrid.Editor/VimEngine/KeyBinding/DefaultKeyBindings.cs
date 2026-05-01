using System.Windows.Input;

namespace VGrid.VimEngine.KeyBinding;

/// <summary>
/// Provides default key-to-action mappings for Vim modes
/// </summary>
public static class DefaultKeyBindings
{
    /// <summary>
    /// Default Normal mode key bindings
    /// </summary>
    private static readonly Dictionary<KeyBinding, string> NormalModeDefaults = new()
    {
        // Basic movement (hjkl)
        { new KeyBinding(Key.H, ModifierKeys.None), "move_left" },
        { new KeyBinding(Key.J, ModifierKeys.None), "move_down" },
        { new KeyBinding(Key.K, ModifierKeys.None), "move_up" },
        { new KeyBinding(Key.L, ModifierKeys.None), "move_right" },

        // Movement with Shift (10 lines)
        { new KeyBinding(Key.H, ModifierKeys.Shift), "move_to_line_start" },
        { new KeyBinding(Key.J, ModifierKeys.Shift), "move_down_10" },
        { new KeyBinding(Key.K, ModifierKeys.Shift), "move_up_10" },
        { new KeyBinding(Key.L, ModifierKeys.Shift), "move_to_last_column" },

        // Line movement
        { new KeyBinding(Key.D0, ModifierKeys.None), "move_to_first_cell" },
        { new KeyBinding(Key.D4, ModifierKeys.Shift), "move_to_last_column" }, // $

        // Word movement
        { new KeyBinding(Key.W, ModifierKeys.None), "move_to_next_word" },
        { new KeyBinding(Key.B, ModifierKeys.None), "move_to_prev_word" },

        // Paragraph movement
        { new KeyBinding(Key.OemCloseBrackets, ModifierKeys.Shift), "move_to_next_empty_row" }, // }
        { new KeyBinding(Key.OemOpenBrackets, ModifierKeys.Shift), "move_to_prev_empty_row" }, // {

        // File movement (G is handled specially for gg)
        { new KeyBinding(Key.G, ModifierKeys.Shift), "move_to_last_line" },

        // Delete operations
        { new KeyBinding(Key.X, ModifierKeys.None), "delete_cell" },
        { new KeyBinding(Key.Delete, ModifierKeys.None), "delete_cell" },

        // Paste operations
        { new KeyBinding(Key.P, ModifierKeys.None), "paste_after" },
        { new KeyBinding(Key.P, ModifierKeys.Shift), "paste_before" },

        // Undo/Redo
        { new KeyBinding(Key.U, ModifierKeys.None), "undo" },
        { new KeyBinding(Key.R, ModifierKeys.Control), "redo" },

        // Copy (Ctrl+C)
        { new KeyBinding(Key.C, ModifierKeys.Control), "yank_cell" },

        // Paste (Ctrl+V)
        { new KeyBinding(Key.V, ModifierKeys.Control), "paste_after" },

        // Mode switching
        { new KeyBinding(Key.I, ModifierKeys.None), "switch_to_insert" },
        { new KeyBinding(Key.I, ModifierKeys.Shift), "switch_to_insert_line_start" },
        { new KeyBinding(Key.A, ModifierKeys.None), "switch_to_append" },
        { new KeyBinding(Key.A, ModifierKeys.Shift), "switch_to_append_line_end" },
        { new KeyBinding(Key.O, ModifierKeys.None), "switch_to_insert_below" },
        { new KeyBinding(Key.O, ModifierKeys.Shift), "switch_to_insert_above" },
        { new KeyBinding(Key.V, ModifierKeys.None), "switch_to_visual" },
        { new KeyBinding(Key.V, ModifierKeys.Shift), "switch_to_visual_line" },
        { new KeyBinding(Key.V, ModifierKeys.Control | ModifierKeys.Shift), "switch_to_visual_block" },
        { new KeyBinding(Key.Oem1, ModifierKeys.None), "switch_to_command" }, // :
        { new KeyBinding(Key.OemQuestion, ModifierKeys.None), "start_search" }, // /

        // Search navigation
        { new KeyBinding(Key.N, ModifierKeys.None), "navigate_to_next_match" },
        { new KeyBinding(Key.N, ModifierKeys.Shift), "navigate_to_prev_match" },

        // Tab navigation
        { new KeyBinding(Key.OemComma, ModifierKeys.Shift), "switch_to_prev_tab" }, // <
        { new KeyBinding(Key.OemPeriod, ModifierKeys.Shift), "switch_to_next_tab" }, // >

        // Align
        { new KeyBinding(Key.OemPlus, ModifierKeys.None), "align_selection" }, // =

        // Escape
        { new KeyBinding(Key.Escape, ModifierKeys.None), "switch_to_normal" },
    };

    /// <summary>
    /// Default Insert mode key bindings
    /// </summary>
    private static readonly Dictionary<KeyBinding, string> InsertModeDefaults = new()
    {
        { new KeyBinding(Key.Escape, ModifierKeys.None), "switch_to_normal" },
        { new KeyBinding(Key.Left, ModifierKeys.None), "move_left" },
        { new KeyBinding(Key.Right, ModifierKeys.None), "move_right" },
        { new KeyBinding(Key.Up, ModifierKeys.None), "move_up" },
        { new KeyBinding(Key.Down, ModifierKeys.None), "move_down" },
    };

    /// <summary>
    /// Default Visual mode key bindings
    /// </summary>
    private static readonly Dictionary<KeyBinding, string> VisualModeDefaults = new()
    {
        { new KeyBinding(Key.Escape, ModifierKeys.None), "switch_to_normal" },
        { new KeyBinding(Key.H, ModifierKeys.None), "move_left" },
        { new KeyBinding(Key.J, ModifierKeys.None), "move_down" },
        { new KeyBinding(Key.K, ModifierKeys.None), "move_up" },
        { new KeyBinding(Key.L, ModifierKeys.None), "move_right" },
        { new KeyBinding(Key.H, ModifierKeys.Shift), "move_to_line_start" },
        { new KeyBinding(Key.J, ModifierKeys.Shift), "move_down_10" },
        { new KeyBinding(Key.K, ModifierKeys.Shift), "move_up_10" },
        { new KeyBinding(Key.L, ModifierKeys.Shift), "move_to_last_column" },
        { new KeyBinding(Key.D0, ModifierKeys.None), "move_to_first_cell" },
        { new KeyBinding(Key.D4, ModifierKeys.Shift), "move_to_last_column" }, // $
        { new KeyBinding(Key.W, ModifierKeys.None), "move_to_next_word" },
        { new KeyBinding(Key.B, ModifierKeys.None), "move_to_prev_word" },
        { new KeyBinding(Key.G, ModifierKeys.Shift), "move_to_last_line" },
        { new KeyBinding(Key.OemCloseBrackets, ModifierKeys.Shift), "move_to_next_empty_row" }, // }
        { new KeyBinding(Key.OemOpenBrackets, ModifierKeys.Shift), "move_to_prev_empty_row" }, // {
        { new KeyBinding(Key.D, ModifierKeys.None), "delete_selection" },
        { new KeyBinding(Key.Delete, ModifierKeys.None), "delete_selection" },
        { new KeyBinding(Key.Y, ModifierKeys.None), "yank_selection" },
        { new KeyBinding(Key.C, ModifierKeys.Control), "yank_selection" },
        { new KeyBinding(Key.P, ModifierKeys.None), "paste_after" },
        { new KeyBinding(Key.V, ModifierKeys.Control), "paste_after" },
    };

    /// <summary>
    /// Tries to resolve an action name from a key binding for a specific mode.
    /// Returns the action name if the key is a known default binding.
    /// </summary>
    /// <param name="mode">The Vim mode</param>
    /// <param name="binding">The key binding</param>
    /// <returns>The action name if found, null otherwise</returns>
    public static string? GetActionForKey(VimMode mode, KeyBinding binding)
    {
        var defaults = GetDefaultsForMode(mode);
        return defaults.TryGetValue(binding, out var action) ? action : null;
    }

    /// <summary>
    /// Gets all default bindings for a specific mode
    /// </summary>
    public static IReadOnlyDictionary<KeyBinding, string> GetDefaultsForMode(VimMode mode)
    {
        return mode switch
        {
            VimMode.Normal => NormalModeDefaults,
            VimMode.Insert => InsertModeDefaults,
            VimMode.Visual => VisualModeDefaults,
            _ => new Dictionary<KeyBinding, string>()
        };
    }

    /// <summary>
    /// Tries to resolve a key notation to an action name.
    /// If the input is a key notation (like "k", "<C-j>"), looks up the default action for that key.
    /// If not found as a key, returns the input as-is (assuming it's an action name).
    /// </summary>
    /// <param name="input">Key notation or action name</param>
    /// <param name="mode">The Vim mode to look up defaults for</param>
    /// <returns>The resolved action name</returns>
    public static string ResolveToAction(string input, VimMode mode)
    {
        // First, try to parse as a key notation
        var binding = VGrid.VimEngine.Vimrc.KeyNotationParser.Parse(input);

        if (binding.HasValue)
        {
            // It's a valid key notation, look up the default action
            var action = GetActionForKey(mode, binding.Value);
            if (action != null)
            {
                return action;
            }
        }

        // Not a known key binding, return as-is (assuming it's an action name)
        return input;
    }

    /// <summary>
    /// Gets the key binding string for a given action name in Normal mode
    /// </summary>
    /// <param name="actionName">The action name to look up</param>
    /// <returns>The key binding string if found, empty string otherwise</returns>
    public static string GetKeyBindingForAction(string actionName)
    {
        foreach (var kvp in NormalModeDefaults)
        {
            if (kvp.Value == actionName)
            {
                return KeyBindingToString(kvp.Key);
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// Converts a KeyBinding to a display string
    /// </summary>
    private static string KeyBindingToString(KeyBinding binding)
    {
        var parts = new System.Collections.Generic.List<string>();

        if (binding.Modifiers.HasFlag(ModifierKeys.Control))
            parts.Add("Ctrl");
        if (binding.Modifiers.HasFlag(ModifierKeys.Shift))
            parts.Add("Shift");
        if (binding.Modifiers.HasFlag(ModifierKeys.Alt))
            parts.Add("Alt");

        string keyStr = binding.Key switch
        {
            Key.H => "h",
            Key.J => "j",
            Key.K => "k",
            Key.L => "l",
            Key.W => "w",
            Key.B => "b",
            Key.D => "d",
            Key.Y => "y",
            Key.P => "p",
            Key.U => "u",
            Key.R => "r",
            Key.I => "i",
            Key.A => "a",
            Key.O => "o",
            Key.V => "v",
            Key.N => "n",
            Key.G => "G",
            Key.X => "x",
            Key.C => "c",
            Key.S => "s",
            Key.D0 => "0",
            Key.D4 => "$",
            Key.Escape => "Esc",
            Key.Delete => "Del",
            Key.OemComma => "<",
            Key.OemPeriod => ">",
            Key.OemPlus => "=",
            Key.OemQuestion => "/",
            Key.Oem1 => ":",
            Key.OemOpenBrackets => "{",
            Key.OemCloseBrackets => "}",
            _ => binding.Key.ToString()
        };

        parts.Add(keyStr);
        return string.Join("+", parts);
    }
}
