using System.IO;
using System.Text.Json;
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
}
