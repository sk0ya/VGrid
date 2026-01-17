using VGrid.VimEngine.KeyBinding;

namespace VGrid.VimEngine.Actions;

/// <summary>
/// Tab navigation Vim actions
/// </summary>
public static class TabActions
{
    public class SwitchToPrevTabAction : IVimAction
    {
        public string Name => "switch_to_prev_tab";

        public bool Execute(VimActionContext context)
        {
            context.State.OnPreviousTabRequested();
            return true;
        }
    }

    public class SwitchToNextTabAction : IVimAction
    {
        public string Name => "switch_to_next_tab";

        public bool Execute(VimActionContext context)
        {
            context.State.OnNextTabRequested();
            return true;
        }
    }
}
