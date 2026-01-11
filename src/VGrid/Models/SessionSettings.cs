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
}
