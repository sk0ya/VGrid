using VGrid.VimEngine.KeyBinding;

namespace VGrid.VimEngine.Actions;

/// <summary>
/// Registry of all available Vim actions
/// </summary>
public class ActionRegistry
{
    private readonly Dictionary<string, IVimAction> _actions = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the singleton instance of the action registry
    /// </summary>
    public static ActionRegistry Instance { get; } = new();

    private ActionRegistry()
    {
        RegisterBuiltInActions();
    }

    /// <summary>
    /// Registers all built-in actions
    /// </summary>
    private void RegisterBuiltInActions()
    {
        // Movement actions
        RegisterMovementActions();

        // Edit actions
        RegisterEditActions();

        // Mode switch actions
        RegisterModeActions();

        // File operation actions
        RegisterFileActions();

        // Tab actions
        RegisterTabActions();

        // Scroll actions
        RegisterScrollActions();

        // Search actions
        RegisterSearchActions();
    }

    private void RegisterMovementActions()
    {
        Register(new MovementActions.MoveLeftAction());
        Register(new MovementActions.MoveRightAction());
        Register(new MovementActions.MoveUpAction());
        Register(new MovementActions.MoveDownAction());
        Register(new MovementActions.MoveUp10Action());
        Register(new MovementActions.MoveDown10Action());
        Register(new MovementActions.MoveToLineStartAction());
        Register(new MovementActions.MoveToFirstCellAction());
        Register(new MovementActions.MoveToLastColumnAction());
        Register(new MovementActions.MoveToFirstLineAction());
        Register(new MovementActions.MoveToLastLineAction());
        Register(new MovementActions.MoveToNextWordAction());
        Register(new MovementActions.MoveToPrevWordAction());
        Register(new MovementActions.MoveToNextEmptyRowAction());
        Register(new MovementActions.MoveToPrevEmptyRowAction());
    }

    private void RegisterEditActions()
    {
        Register(new EditActions.DeleteLineAction());
        Register(new EditActions.DeleteCellAction());
        Register(new EditActions.DeleteWordAction());
        Register(new EditActions.DeleteSelectionAction());
        Register(new EditActions.YankLineAction());
        Register(new EditActions.YankCellAction());
        Register(new EditActions.YankWordAction());
        Register(new EditActions.YankSelectionAction());
        Register(new EditActions.PasteAfterAction());
        Register(new EditActions.PasteBeforeAction());
        Register(new EditActions.UndoAction());
        Register(new EditActions.RedoAction());
        Register(new EditActions.AlignSelectionAction());
        Register(new EditActions.ChangeLineAction());
        Register(new EditActions.ChangeWordAction());
    }

    private void RegisterModeActions()
    {
        Register(new ModeActions.SwitchToInsertAction());
        Register(new ModeActions.SwitchToInsertLineStartAction());
        Register(new ModeActions.SwitchToAppendAction());
        Register(new ModeActions.SwitchToAppendLineEndAction());
        Register(new ModeActions.SwitchToInsertBelowAction());
        Register(new ModeActions.SwitchToInsertAboveAction());
        Register(new ModeActions.SwitchToVisualAction());
        Register(new ModeActions.SwitchToVisualLineAction());
        Register(new ModeActions.SwitchToVisualBlockAction());
        Register(new ModeActions.SwitchToCommandAction());
        Register(new ModeActions.StartSearchAction());
        Register(new ModeActions.SwitchToNormalAction());
    }

    private void RegisterFileActions()
    {
        Register(new FileActions.SaveFileAction());
        Register(new FileActions.QuitAction());
        Register(new FileActions.ForceQuitAction());
        Register(new FileActions.SaveAndQuitAction());
    }

    private void RegisterTabActions()
    {
        Register(new TabActions.SwitchToPrevTabAction());
        Register(new TabActions.SwitchToNextTabAction());
    }

    private void RegisterScrollActions()
    {
        Register(new ScrollActions.ScrollToCenterAction());
    }

    private void RegisterSearchActions()
    {
        Register(new SearchActions.NavigateToNextMatchAction());
        Register(new SearchActions.NavigateToPrevMatchAction());
    }

    /// <summary>
    /// Registers an action
    /// </summary>
    public void Register(IVimAction action)
    {
        _actions[action.Name] = action;
    }

    /// <summary>
    /// Tries to get an action by name
    /// </summary>
    public bool TryGetAction(string name, out IVimAction? action)
    {
        return _actions.TryGetValue(name, out action);
    }

    /// <summary>
    /// Gets all registered action names
    /// </summary>
    public IEnumerable<string> GetActionNames()
    {
        return _actions.Keys.OrderBy(k => k);
    }

    /// <summary>
    /// Gets the count of registered actions
    /// </summary>
    public int Count => _actions.Count;
}
