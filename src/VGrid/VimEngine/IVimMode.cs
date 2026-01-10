using System.Windows.Input;
using VGrid.Models;

namespace VGrid.VimEngine;

/// <summary>
/// Interface for Vim mode implementations
/// </summary>
public interface IVimMode
{
    /// <summary>
    /// Handles a key press in this mode
    /// </summary>
    /// <param name="state">The current Vim state</param>
    /// <param name="key">The key that was pressed</param>
    /// <param name="modifiers">The modifier keys</param>
    /// <param name="document">The document being edited</param>
    /// <returns>True if the key was handled, false otherwise</returns>
    bool HandleKey(VimState state, Key key, ModifierKeys modifiers, TsvDocument document);

    /// <summary>
    /// Called when entering this mode
    /// </summary>
    void OnEnter(VimState state);

    /// <summary>
    /// Called when exiting this mode
    /// </summary>
    void OnExit(VimState state);

    /// <summary>
    /// Gets the name of this mode for display
    /// </summary>
    string GetModeName();
}
