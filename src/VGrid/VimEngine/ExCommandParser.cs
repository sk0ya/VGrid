namespace VGrid.VimEngine;

/// <summary>
/// Types of ex-commands
/// </summary>
public enum ExCommandType
{
    /// <summary>
    /// Unknown or invalid command
    /// </summary>
    Unknown,

    /// <summary>
    /// Write (save) command (:w)
    /// </summary>
    Write,

    /// <summary>
    /// Quit (close) command (:q)
    /// </summary>
    Quit,

    /// <summary>
    /// Write and quit command (:wq)
    /// </summary>
    WriteQuit
}

/// <summary>
/// Represents the result of parsing an ex-command
/// </summary>
public class ExCommandResult
{
    /// <summary>
    /// The type of command
    /// </summary>
    public ExCommandType Type { get; init; }

    /// <summary>
    /// Whether the command has a force modifier (!)
    /// </summary>
    public bool Force { get; init; }

    /// <summary>
    /// Error message if the command is invalid
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Whether the command is valid
    /// </summary>
    public bool IsValid => string.IsNullOrEmpty(ErrorMessage);
}

/// <summary>
/// Parses Vim ex-commands (: commands)
/// </summary>
public static class ExCommandParser
{
    /// <summary>
    /// Parses an ex-command string
    /// </summary>
    /// <param name="command">Command without the leading ':'</param>
    /// <returns>Parsed command result</returns>
    public static ExCommandResult Parse(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return new ExCommandResult { Type = ExCommandType.Unknown };
        }

        string trimmed = command.Trim();
        bool force = trimmed.EndsWith('!');
        string commandName = force ? trimmed[..^1] : trimmed;

        return commandName switch
        {
            "w" or "write" => new ExCommandResult { Type = ExCommandType.Write, Force = force },
            "q" or "quit" => new ExCommandResult { Type = ExCommandType.Quit, Force = force },
            "wq" or "x" => new ExCommandResult { Type = ExCommandType.WriteQuit, Force = force },
            _ => new ExCommandResult
            {
                Type = ExCommandType.Unknown,
                ErrorMessage = $"Not an editor command: {trimmed}"
            }
        };
    }
}
