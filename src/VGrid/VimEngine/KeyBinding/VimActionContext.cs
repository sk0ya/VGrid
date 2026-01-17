using VGrid.Models;

namespace VGrid.VimEngine.KeyBinding;

/// <summary>
/// Context for executing Vim actions
/// </summary>
public class VimActionContext
{
    /// <summary>
    /// The current Vim state
    /// </summary>
    public VimState State { get; }

    /// <summary>
    /// The document being edited
    /// </summary>
    public TsvDocument Document { get; }

    /// <summary>
    /// The count prefix for the command (default 1)
    /// </summary>
    public int Count { get; }

    public VimActionContext(VimState state, TsvDocument document, int count = 1)
    {
        State = state;
        Document = document;
        Count = count;
    }
}
