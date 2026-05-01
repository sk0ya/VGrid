namespace VGrid.VimEngine;

/// <summary>
/// Represents the different modes in Vim
/// </summary>
public enum VimMode
{
    /// <summary>
    /// Normal mode - for navigation and commands
    /// </summary>
    Normal,

    /// <summary>
    /// Insert mode - for text editing
    /// </summary>
    Insert,

    /// <summary>
    /// Visual mode - for selection
    /// </summary>
    Visual,

    /// <summary>
    /// Command mode - for : commands
    /// </summary>
    Command
}
