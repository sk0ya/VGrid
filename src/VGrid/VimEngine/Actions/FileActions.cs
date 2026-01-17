using VGrid.VimEngine.KeyBinding;

namespace VGrid.VimEngine.Actions;

/// <summary>
/// File operation Vim actions
/// </summary>
public static class FileActions
{
    public class SaveFileAction : IVimAction
    {
        public string Name => "save_file";

        public bool Execute(VimActionContext context)
        {
            context.State.OnSaveRequested();
            return true;
        }
    }

    public class QuitAction : IVimAction
    {
        public string Name => "quit";

        public bool Execute(VimActionContext context)
        {
            context.State.OnQuitRequested(forceQuit: false);
            return true;
        }
    }

    public class ForceQuitAction : IVimAction
    {
        public string Name => "force_quit";

        public bool Execute(VimActionContext context)
        {
            context.State.OnQuitRequested(forceQuit: true);
            return true;
        }
    }

    public class SaveAndQuitAction : IVimAction
    {
        public string Name => "save_and_quit";

        public bool Execute(VimActionContext context)
        {
            context.State.OnSaveRequested();
            context.State.OnQuitRequested(forceQuit: false);
            return true;
        }
    }
}
