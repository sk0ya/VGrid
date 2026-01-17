using VGrid.VimEngine.KeyBinding;

namespace VGrid.Services;

/// <summary>
/// Service for loading and managing vimrc configuration
/// </summary>
public interface IVimrcService
{
    /// <summary>
    /// Gets the current keybinding configuration
    /// </summary>
    KeyBindingConfig Config { get; }

    /// <summary>
    /// Gets whether a vimrc file was successfully loaded
    /// </summary>
    bool IsLoaded { get; }

    /// <summary>
    /// Gets the path to the vimrc file
    /// </summary>
    string VimrcPath { get; }

    /// <summary>
    /// Gets any errors that occurred during loading
    /// </summary>
    IReadOnlyList<string> LoadErrors { get; }

    /// <summary>
    /// Loads the vimrc configuration from the default location
    /// </summary>
    void Load();

    /// <summary>
    /// Loads the vimrc configuration from a specific path
    /// </summary>
    /// <param name="path">The path to the vimrc file</param>
    void Load(string path);

    /// <summary>
    /// Reloads the vimrc configuration
    /// </summary>
    void Reload();

    /// <summary>
    /// Creates a default vimrc file if one doesn't exist
    /// </summary>
    /// <returns>True if a file was created</returns>
    bool CreateDefaultIfNotExists();
}
