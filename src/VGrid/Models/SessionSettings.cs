namespace VGrid.Models;

/// <summary>
/// Represents the application session state that is persisted between application runs.
/// </summary>
public class SessionSettings
{
    /// <summary>
    /// List of file paths that were open in the previous session.
    /// </summary>
    public List<string> OpenFiles { get; set; } = new();

    /// <summary>
    /// Index of the tab that was selected in the previous session.
    /// </summary>
    public int SelectedTabIndex { get; set; }

    /// <summary>
    /// Path of the folder that was selected in the folder explorer.
    /// </summary>
    public string? SelectedFolderPath { get; set; }

    /// <summary>
    /// List of recently opened folder paths for the Windows Jump List.
    /// Maximum 10 folders, most recent first.
    /// </summary>
    public List<string> RecentFolders { get; set; } = new();

    /// <summary>
    /// Whether Vim keybindings are enabled. Default is true.
    /// </summary>
    public bool IsVimModeEnabled { get; set; } = true;

    /// <summary>
    /// Color theme name. Values: "Light" or "Dark". Default is "Light".
    /// </summary>
    public string ColorTheme { get; set; } = "Light";
}
