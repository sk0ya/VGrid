using VGrid.VimEngine.KeyBinding;

namespace VGrid.VimEngine.Actions;

/// <summary>
/// Scroll-related Vim actions
/// </summary>
public static class ScrollActions
{
    public class ScrollToCenterAction : IVimAction
    {
        public string Name => "scroll_to_center";

        public bool Execute(VimActionContext context)
        {
            context.State.OnScrollToCenterRequested();
            return true;
        }
    }
}
