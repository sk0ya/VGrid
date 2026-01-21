using System.IO;
using System.Text.Json;
using System.Windows.Shell;
using VGrid.Models;

namespace VGrid.Services;

/// <summary>
/// Service for loading and saving application settings in JSON format.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;

    public SettingsService()
    {
        // Store settings in %LOCALAPPDATA%\VGrid\session.json
        var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var vgridFolder = Path.Combine(appDataFolder, "VGrid");
        _settingsFilePath = Path.Combine(vgridFolder, "session.json");
    }

    /// <summary>
    /// Loads the session settings from the JSON file.
    /// </summary>
    /// <returns>The session settings, or null if the file doesn't exist or if deserialization fails.</returns>
    public SessionSettings? LoadSession()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return null;
            }

            var json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<SessionSettings>(json);
        }
        catch (Exception)
        {
            // If JSON is corrupted or any other error occurs, return null
            // This allows the application to fall back to creating a new file
            return null;
        }
    }

    /// <summary>
    /// Saves the session settings to the JSON file.
    /// </summary>
    /// <param name="settings">The session settings to save.</param>
    public void SaveSession(SessionSettings settings)
    {
        try
        {
            // Ensure the directory exists
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Serialize and save with indentation for readability
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(_settingsFilePath, json);
        }
        catch (Exception)
        {
            // Silently fail if we can't save settings
            // The application should continue to work even if settings can't be saved
        }
    }

    /// <summary>
    /// Adds a folder to the recent folders list and updates the Jump List.
    /// </summary>
    /// <param name="folderPath">The folder path to add.</param>
    public void AddRecentFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            return;

        var session = LoadSession() ?? new SessionSettings();

        // Remove if already exists (to move it to the top)
        session.RecentFolders.RemoveAll(f =>
            string.Equals(f, folderPath, StringComparison.OrdinalIgnoreCase));

        // Insert at the beginning
        session.RecentFolders.Insert(0, folderPath);

        // Keep only the last 10 folders
        if (session.RecentFolders.Count > 10)
        {
            session.RecentFolders = session.RecentFolders.Take(10).ToList();
        }

        SaveSession(session);
        UpdateJumpList(session.RecentFolders);
    }

    /// <summary>
    /// Gets the list of recent folders.
    /// </summary>
    /// <returns>List of recently opened folder paths.</returns>
    public List<string> GetRecentFolders()
    {
        var session = LoadSession();
        return session?.RecentFolders ?? new List<string>();
    }

    /// <summary>
    /// Updates the Windows Jump List with recent folders.
    /// </summary>
    /// <param name="recentFolders">List of recent folder paths.</param>
    public void UpdateJumpList(List<string> recentFolders)
    {
        try
        {
            var jumpList = new JumpList();

            foreach (var folder in recentFolders.Where(Directory.Exists).Take(10))
            {
                var folderName = Path.GetFileName(folder);
                if (string.IsNullOrEmpty(folderName))
                    folderName = folder;

                var jumpTask = new JumpTask
                {
                    Title = folderName,
                    Description = folder,
                    ApplicationPath = Environment.ProcessPath ?? string.Empty,
                    Arguments = $"--folder \"{folder}\"",
                    CustomCategory = "Recent Folders"
                };

                jumpList.JumpItems.Add(jumpTask);
            }

            jumpList.Apply();
        }
        catch (Exception)
        {
            // Silently fail if Jump List update fails
        }
    }
}
