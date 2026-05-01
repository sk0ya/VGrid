using System.Windows.Input;

namespace VGrid.VimEngine.KeyBinding;

/// <summary>
/// Represents a key binding (key + modifiers)
/// </summary>
public readonly struct KeyBinding : IEquatable<KeyBinding>
{
    /// <summary>
    /// The primary key
    /// </summary>
    public Key Key { get; }

    /// <summary>
    /// The modifier keys (Ctrl, Shift, Alt)
    /// </summary>
    public ModifierKeys Modifiers { get; }

    public KeyBinding(Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        Key = key;
        Modifiers = modifiers;
    }

    public bool Equals(KeyBinding other)
    {
        return Key == other.Key && Modifiers == other.Modifiers;
    }

    public override bool Equals(object? obj)
    {
        return obj is KeyBinding other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Key, Modifiers);
    }

    public static bool operator ==(KeyBinding left, KeyBinding right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(KeyBinding left, KeyBinding right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        var parts = new List<string>();

        if (Modifiers.HasFlag(ModifierKeys.Control))
            parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Shift))
            parts.Add("Shift");
        if (Modifiers.HasFlag(ModifierKeys.Alt))
            parts.Add("Alt");

        parts.Add(Key.ToString());

        return string.Join("+", parts);
    }
}
