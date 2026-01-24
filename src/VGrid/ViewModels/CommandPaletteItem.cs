using VGrid.VimEngine.KeyBinding;

namespace VGrid.ViewModels;

/// <summary>
/// Type of command palette item
/// </summary>
public enum CommandPaletteItemType
{
    Command,
    File
}

/// <summary>
/// Mode for the command palette
/// </summary>
public enum CommandPaletteMode
{
    All,
    Commands,
    Files
}

/// <summary>
/// Represents an item in the command palette
/// </summary>
public class CommandPaletteItem
{
    /// <summary>
    /// The type of this item
    /// </summary>
    public CommandPaletteItemType ItemType { get; }

    /// <summary>
    /// The action name (e.g., "move_down") - for Command type
    /// </summary>
    public string ActionName { get; }

    /// <summary>
    /// The display name (e.g., "Move Down" or "filename.tsv")
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// The key binding string (e.g., "j") - for Command type
    /// </summary>
    public string KeyBinding { get; }

    /// <summary>
    /// Reference to the action - for Command type
    /// </summary>
    public IVimAction? Action { get; }

    /// <summary>
    /// Full file path - for File type
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// Relative file path for display - for File type
    /// </summary>
    public string? RelativePath { get; }

    /// <summary>
    /// Creates a command item
    /// </summary>
    public CommandPaletteItem(string actionName, string displayName, string keyBinding, IVimAction action)
    {
        ItemType = CommandPaletteItemType.Command;
        ActionName = actionName;
        DisplayName = displayName;
        KeyBinding = keyBinding;
        Action = action;
        FilePath = null;
        RelativePath = null;
    }

    /// <summary>
    /// Creates a file item
    /// </summary>
    public CommandPaletteItem(string filePath, string relativePath)
    {
        ItemType = CommandPaletteItemType.File;
        ActionName = string.Empty;
        DisplayName = System.IO.Path.GetFileName(filePath);
        KeyBinding = string.Empty;
        Action = null;
        FilePath = filePath;
        RelativePath = relativePath;
    }

    /// <summary>
    /// Creates a display name from an action name (e.g., "move_down" -> "Move Down")
    /// </summary>
    public static string CreateDisplayName(string actionName)
    {
        if (string.IsNullOrEmpty(actionName))
            return string.Empty;

        // Split by underscore and capitalize each word
        var parts = actionName.Split('_');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
            {
                parts[i] = char.ToUpper(parts[i][0]) + parts[i].Substring(1).ToLower();
            }
        }
        return string.Join(" ", parts);
    }
}
