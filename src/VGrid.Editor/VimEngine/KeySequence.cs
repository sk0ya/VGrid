using System.Windows.Input;

namespace VGrid.VimEngine;

/// <summary>
/// Buffers a sequence of keys for multi-key commands (e.g., "dd", "3j")
/// </summary>
public class KeySequence
{
    private readonly List<Key> _keys = new();
    private DateTime _lastKeyTime = DateTime.Now;
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The keys in this sequence
    /// </summary>
    public IReadOnlyList<Key> Keys => _keys.AsReadOnly();

    /// <summary>
    /// The time of the last key press
    /// </summary>
    public DateTime LastKeyTime => _lastKeyTime;

    /// <summary>
    /// Adds a key to the sequence
    /// </summary>
    public void Add(Key key)
    {
        _keys.Add(key);
        _lastKeyTime = DateTime.Now;
    }

    /// <summary>
    /// Clears the sequence
    /// </summary>
    public void Clear()
    {
        _keys.Clear();
        _lastKeyTime = DateTime.Now;
    }

    /// <summary>
    /// Checks if the sequence has expired (timeout)
    /// </summary>
    public bool IsExpired(TimeSpan? timeout = null)
    {
        var timeoutSpan = timeout ?? DefaultTimeout;
        return (DateTime.Now - _lastKeyTime) > timeoutSpan;
    }

    /// <summary>
    /// Gets the string representation of the key sequence
    /// </summary>
    public override string ToString()
    {
        return string.Join("", _keys.Select(KeyToString));
    }

    private static string KeyToString(Key key)
    {
        // Convert WPF Key to string representation
        return key switch
        {
            Key.D0 => "0",
            Key.D1 => "1",
            Key.D2 => "2",
            Key.D3 => "3",
            Key.D4 => "4",
            Key.D5 => "5",
            Key.D6 => "6",
            Key.D7 => "7",
            Key.D8 => "8",
            Key.D9 => "9",
            _ => key.ToString().ToLower()
        };
    }
}
