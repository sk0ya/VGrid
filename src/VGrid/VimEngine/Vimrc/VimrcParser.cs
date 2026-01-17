using System.Text.RegularExpressions;
using VGrid.VimEngine.KeyBinding;

namespace VGrid.VimEngine.Vimrc;

/// <summary>
/// Parses .vimrc files for keybinding configuration
/// </summary>
public class VimrcParser
{
    /// <summary>
    /// Regex pattern for map commands: nmap/imap/vmap/cmap <key> <action>
    /// </summary>
    private static readonly Regex MapCommandPattern = new(
        @"^\s*([niv]?map)\s+(\S+)\s+(\S+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Represents a parse error
    /// </summary>
    public record ParseError(int LineNumber, string Line, string Message);

    /// <summary>
    /// Result of parsing a vimrc file
    /// </summary>
    public class ParseResult
    {
        public KeyBindingConfig Config { get; } = new();
        public List<ParseError> Errors { get; } = new();
        public bool HasErrors => Errors.Count > 0;
    }

    /// <summary>
    /// Parses vimrc content
    /// </summary>
    /// <param name="content">The vimrc file content</param>
    /// <returns>Parse result with configuration and any errors</returns>
    public ParseResult Parse(string content)
    {
        var result = new ParseResult();

        if (string.IsNullOrWhiteSpace(content))
            return result;

        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Remove inline comments
            line = RemoveInlineComment(line);

            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Skip comment-only lines
            if (line.TrimStart().StartsWith("\""))
                continue;

            // Try to parse map command
            var match = MapCommandPattern.Match(line);
            if (match.Success)
            {
                ParseMapCommand(result, match, lineNumber, line);
            }
            else
            {
                // Unknown command - just skip it (vimrc can have many commands we don't support)
            }
        }

        return result;
    }

    /// <summary>
    /// Parses a map command and adds it to the configuration
    /// </summary>
    private void ParseMapCommand(ParseResult result, Match match, int lineNumber, string originalLine)
    {
        var mapType = match.Groups[1].Value.ToLowerInvariant();
        var keyNotation = match.Groups[2].Value;
        var actionName = match.Groups[3].Value;

        // Determine the mode(s) for this mapping
        var modes = GetModesForMapType(mapType);
        if (modes.Count == 0)
        {
            result.Errors.Add(new ParseError(lineNumber, originalLine,
                $"Unknown map type: {mapType}"));
            return;
        }

        // Parse the key notation
        var binding = ParseKeySequence(keyNotation);
        if (binding == null)
        {
            result.Errors.Add(new ParseError(lineNumber, originalLine,
                $"Invalid key notation: {keyNotation}"));
            return;
        }

        // Add binding for each mode
        foreach (var mode in modes)
        {
            // Resolve action name: if it's a key notation (like "k"), look up the default action
            var resolvedAction = DefaultKeyBindings.ResolveToAction(actionName, mode);
            result.Config.AddBinding(mode, binding.Value, resolvedAction);
        }
    }

    /// <summary>
    /// Gets the Vim modes for a map type command
    /// </summary>
    private List<VimMode> GetModesForMapType(string mapType)
    {
        return mapType switch
        {
            "nmap" => new List<VimMode> { VimMode.Normal },
            "imap" => new List<VimMode> { VimMode.Insert },
            "vmap" => new List<VimMode> { VimMode.Visual },
            // "map" without prefix applies to Normal, Visual, and Operator-pending
            // For simplicity, we apply to Normal and Visual
            "map" => new List<VimMode> { VimMode.Normal, VimMode.Visual },
            _ => new List<VimMode>()
        };
    }

    /// <summary>
    /// Parses a key sequence (handles multi-key sequences like <Space>w)
    /// </summary>
    private KeyBinding.KeyBinding? ParseKeySequence(string notation)
    {
        // For now, we support single key/key combo bindings
        // Multi-key sequences like <Space>w will be handled as <Space> binding
        // that triggers a pending key state

        // Check if it's a special notation
        if (notation.StartsWith("<") && notation.Contains(">"))
        {
            // Could be a single special key like <C-j> or a sequence like <Space>w
            var endBracket = notation.IndexOf('>');
            var specialPart = notation.Substring(0, endBracket + 1);
            var remaining = notation.Substring(endBracket + 1);

            if (string.IsNullOrEmpty(remaining))
            {
                // Single special key
                return KeyNotationParser.Parse(specialPart);
            }
            else
            {
                // Multi-key sequence - for now, just return the first key
                // The action handler will need to handle the sequence
                return KeyNotationParser.Parse(specialPart);
            }
        }

        // Single character or unknown notation
        if (notation.Length == 1)
        {
            return KeyNotationParser.Parse(notation);
        }

        // Multi-character sequence without special notation
        // For now, just return the first character
        return KeyNotationParser.Parse(notation[0].ToString());
    }

    /// <summary>
    /// Removes inline comments from a line (everything after unescaped ")
    /// </summary>
    private string RemoveInlineComment(string line)
    {
        // In Vimscript, " starts a comment
        // Find the first unquoted "
        var inString = false;
        var stringChar = '\0';

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inString)
            {
                if (c == stringChar)
                    inString = false;
            }
            else
            {
                if (c == '\'' || c == '"')
                {
                    // Check if this is a comment or a string
                    // In vimrc, " typically starts a comment
                    // We'll treat " at the start of words as comments
                    if (c == '"' && (i == 0 || char.IsWhiteSpace(line[i - 1])))
                    {
                        return line.Substring(0, i).TrimEnd();
                    }
                    inString = true;
                    stringChar = c;
                }
            }
        }

        return line;
    }
}
