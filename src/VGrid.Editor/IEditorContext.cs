using VGrid.Services;
using VGrid.ViewModels;

namespace VGrid.Editor;

/// <summary>
/// Abstracts the host context needed by DataGridManager, SelectionManager, and VimInputHandler.
/// MainViewModel implements this; TsvEditorControl provides a single-tab implementation.
/// </summary>
public interface IEditorContext
{
    TabItemViewModel? SelectedTab { get; }
    bool IsVimModeEnabled { get; }
    bool IsRestoringSession { get; }
    IColumnWidthService ColumnWidthService { get; }
}
