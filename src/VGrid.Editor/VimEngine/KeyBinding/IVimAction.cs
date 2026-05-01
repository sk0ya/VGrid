namespace VGrid.VimEngine.KeyBinding;

/// <summary>
/// Interface for Vim actions that can be executed via keybindings
/// </summary>
public interface IVimAction
{
    /// <summary>
    /// The unique name of this action (e.g., "move_down", "delete_line")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the action
    /// </summary>
    /// <param name="context">The execution context containing state and document</param>
    /// <returns>True if the action was handled</returns>
    bool Execute(VimActionContext context);
}
