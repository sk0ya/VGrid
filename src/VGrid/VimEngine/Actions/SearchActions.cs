using VGrid.VimEngine.KeyBinding;

namespace VGrid.VimEngine.Actions;

/// <summary>
/// Search-related Vim actions
/// </summary>
public static class SearchActions
{
    public class NavigateToNextMatchAction : IVimAction
    {
        public string Name => "navigate_to_next_match";

        public bool Execute(VimActionContext context)
        {
            if (!context.State.IsSearchActive || context.State.SearchResults.Count == 0)
                return true;

            context.State.NavigateToNextMatch(forward: true);
            return true;
        }
    }

    public class NavigateToPrevMatchAction : IVimAction
    {
        public string Name => "navigate_to_prev_match";

        public bool Execute(VimActionContext context)
        {
            if (!context.State.IsSearchActive || context.State.SearchResults.Count == 0)
                return true;

            context.State.NavigateToNextMatch(forward: false);
            return true;
        }
    }
}
