using System.Text.RegularExpressions;
using System.Windows.Input;
using VGrid.VimEngine.KeyBinding;

namespace VGrid.VimEngine.Vimrc;

/// <summary>
/// Parses Vim key notation (e.g., <C-j>, <Space>, <CR>) into KeyBinding
/// </summary>
public static class KeyNotationParser
{
    /// <summary>
    /// Special key name mappings
    /// </summary>
    private static readonly Dictionary<string, Key> SpecialKeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Space", Key.Space },
        { "CR", Key.Enter },
        { "Enter", Key.Enter },
        { "Return", Key.Enter },
        { "Esc", Key.Escape },
        { "Escape", Key.Escape },
        { "Tab", Key.Tab },
        { "BS", Key.Back },
        { "Backspace", Key.Back },
        { "Del", Key.Delete },
        { "Delete", Key.Delete },
        { "Up", Key.Up },
        { "Down", Key.Down },
        { "Left", Key.Left },
        { "Right", Key.Right },
        { "Home", Key.Home },
        { "End", Key.End },
        { "PageUp", Key.PageUp },
        { "PageDown", Key.PageDown },
        { "Insert", Key.Insert },
        { "F1", Key.F1 },
        { "F2", Key.F2 },
        { "F3", Key.F3 },
        { "F4", Key.F4 },
        { "F5", Key.F5 },
        { "F6", Key.F6 },
        { "F7", Key.F7 },
        { "F8", Key.F8 },
        { "F9", Key.F9 },
        { "F10", Key.F10 },
        { "F11", Key.F11 },
        { "F12", Key.F12 },
    };

    /// <summary>
    /// Character to Key mappings for special characters
    /// </summary>
    private static readonly Dictionary<char, (Key key, bool needsShift)> CharToKeyMap = new()
    {
        { '0', (Key.D0, false) },
        { '1', (Key.D1, false) },
        { '2', (Key.D2, false) },
        { '3', (Key.D3, false) },
        { '4', (Key.D4, false) },
        { '5', (Key.D5, false) },
        { '6', (Key.D6, false) },
        { '7', (Key.D7, false) },
        { '8', (Key.D8, false) },
        { '9', (Key.D9, false) },
        { '$', (Key.D4, true) },
        { '%', (Key.D5, true) },
        { '^', (Key.D6, true) },
        { '&', (Key.D7, true) },
        { '*', (Key.D8, true) },
        { '(', (Key.D9, true) },
        { ')', (Key.D0, true) },
        { '-', (Key.OemMinus, false) },
        { '_', (Key.OemMinus, true) },
        { '=', (Key.OemPlus, false) },
        { '+', (Key.OemPlus, true) },
        { '[', (Key.OemOpenBrackets, false) },
        { '{', (Key.OemOpenBrackets, true) },
        { ']', (Key.OemCloseBrackets, false) },
        { '}', (Key.OemCloseBrackets, true) },
        { '\\', (Key.OemBackslash, false) },
        { '|', (Key.OemBackslash, true) },
        { ';', (Key.OemSemicolon, false) },
        { ':', (Key.Oem1, true) },
        { '\'', (Key.OemQuotes, false) },
        { '"', (Key.OemQuotes, true) },
        { ',', (Key.OemComma, false) },
        { '<', (Key.OemComma, true) },
        { '.', (Key.OemPeriod, false) },
        { '>', (Key.OemPeriod, true) },
        { '/', (Key.OemQuestion, false) },
        { '?', (Key.OemQuestion, true) },
        { '`', (Key.OemTilde, false) },
        { '~', (Key.OemTilde, true) },
    };

    /// <summary>
    /// Regex pattern for special key notation: <C-x>, <S-x>, <A-x>, <Space>, etc.
    /// </summary>
    private static readonly Regex SpecialKeyPattern = new(@"^<([^>]+)>$", RegexOptions.Compiled);

    /// <summary>
    /// Parses a key notation string into a KeyBinding
    /// </summary>
    /// <param name="notation">The key notation (e.g., "<C-j>", "<Space>", "j", "J")</param>
    /// <returns>The parsed KeyBinding, or null if parsing failed</returns>
    public static KeyBinding.KeyBinding? Parse(string notation)
    {
        if (string.IsNullOrWhiteSpace(notation))
            return null;

        notation = notation.Trim();

        // Check for special key notation: <...>
        var match = SpecialKeyPattern.Match(notation);
        if (match.Success)
        {
            return ParseSpecialNotation(match.Groups[1].Value);
        }

        // Single character
        if (notation.Length == 1)
        {
            return ParseSingleCharacter(notation[0]);
        }

        return null;
    }

    /// <summary>
    /// Parses a single character into a KeyBinding
    /// </summary>
    private static KeyBinding.KeyBinding? ParseSingleCharacter(char c)
    {
        // Check special character mappings first
        if (CharToKeyMap.TryGetValue(c, out var mapping))
        {
            var modifiers = mapping.needsShift ? ModifierKeys.Shift : ModifierKeys.None;
            return new KeyBinding.KeyBinding(mapping.key, modifiers);
        }

        // Alphabetic characters
        if (char.IsLetter(c))
        {
            var keyName = char.ToUpper(c).ToString();
            if (Enum.TryParse<Key>(keyName, out var key))
            {
                var modifiers = char.IsUpper(c) ? ModifierKeys.Shift : ModifierKeys.None;
                return new KeyBinding.KeyBinding(key, modifiers);
            }
        }

        return null;
    }

    /// <summary>
    /// Parses special notation inside angle brackets (without the brackets)
    /// </summary>
    private static KeyBinding.KeyBinding? ParseSpecialNotation(string inner)
    {
        if (string.IsNullOrEmpty(inner))
            return null;

        var modifiers = ModifierKeys.None;
        var keyPart = inner;

        // Parse modifier prefixes: C-, S-, A-, M- (Meta = Alt)
        while (keyPart.Length >= 2 && keyPart[1] == '-')
        {
            var modChar = char.ToUpper(keyPart[0]);
            switch (modChar)
            {
                case 'C':
                    modifiers |= ModifierKeys.Control;
                    break;
                case 'S':
                    modifiers |= ModifierKeys.Shift;
                    break;
                case 'A':
                case 'M':
                    modifiers |= ModifierKeys.Alt;
                    break;
                default:
                    // Unknown modifier, stop parsing modifiers
                    goto ParseKey;
            }
            keyPart = keyPart.Substring(2);
        }

        ParseKey:
        if (string.IsNullOrEmpty(keyPart))
            return null;

        // Check special key names
        if (SpecialKeyMap.TryGetValue(keyPart, out var specialKey))
        {
            return new KeyBinding.KeyBinding(specialKey, modifiers);
        }

        // Single character after modifiers
        if (keyPart.Length == 1)
        {
            var c = keyPart[0];

            // Check special character mappings
            if (CharToKeyMap.TryGetValue(c, out var mapping))
            {
                if (mapping.needsShift)
                {
                    modifiers |= ModifierKeys.Shift;
                }
                return new KeyBinding.KeyBinding(mapping.key, modifiers);
            }

            // Alphabetic character
            if (char.IsLetter(c))
            {
                var keyName = char.ToUpper(c).ToString();
                if (Enum.TryParse<Key>(keyName, out var key))
                {
                    // In <C-x> notation, uppercase letters don't automatically add Shift
                    // User must explicitly specify <C-S-x> for Shift
                    if (char.IsUpper(c) && !modifiers.HasFlag(ModifierKeys.Control) &&
                        !modifiers.HasFlag(ModifierKeys.Alt))
                    {
                        modifiers |= ModifierKeys.Shift;
                    }
                    return new KeyBinding.KeyBinding(key, modifiers);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Converts a KeyBinding back to Vim notation
    /// </summary>
    public static string ToNotation(KeyBinding.KeyBinding binding)
    {
        var parts = new List<string>();

        if (binding.Modifiers.HasFlag(ModifierKeys.Control))
            parts.Add("C");
        if (binding.Modifiers.HasFlag(ModifierKeys.Shift))
            parts.Add("S");
        if (binding.Modifiers.HasFlag(ModifierKeys.Alt))
            parts.Add("A");

        var keyString = KeyToString(binding.Key);

        if (parts.Count > 0 || IsSpecialKey(binding.Key))
        {
            parts.Add(keyString);
            return $"<{string.Join("-", parts)}>";
        }

        return keyString;
    }

    private static bool IsSpecialKey(Key key)
    {
        return key switch
        {
            Key.Space or Key.Enter or Key.Escape or Key.Tab or Key.Back or
            Key.Delete or Key.Up or Key.Down or Key.Left or Key.Right or
            Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.Insert or
            >= Key.F1 and <= Key.F12 => true,
            _ => false
        };
    }

    private static string KeyToString(Key key)
    {
        return key switch
        {
            Key.Space => "Space",
            Key.Enter => "CR",
            Key.Escape => "Esc",
            Key.Tab => "Tab",
            Key.Back => "BS",
            Key.Delete => "Del",
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
            >= Key.A and <= Key.Z => ((char)('a' + (key - Key.A))).ToString(),
            _ => key.ToString()
        };
    }
}
