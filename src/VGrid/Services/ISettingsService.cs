using VGrid.Models;

namespace VGrid.Services;

/// <summary>
/// Service interface for loading and saving application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Loads the session settings from persistent storage.
    /// </summary>
    /// <returns>The session settings, or null if no settings file exists or if loading fails.</returns>
    SessionSettings? LoadSession();

    /// <summary>
    /// Saves the session settings to persistent storage.
    /// </summary>
    /// <param name="settings">The session settings to save.</param>
    void SaveSession(SessionSettings settings);

    /// <summary>
    /// Adds a folder to the recent folders list and updates the Jump List.
    /// </summary>
    /// <param name="folderPath">The folder path to add.</param>
    void AddRecentFolder(string folderPath);

    /// <summary>
    /// Gets the list of recent folders.
    /// </summary>
    /// <returns>List of recently opened folder paths.</returns>
    List<string> GetRecentFolders();
}
