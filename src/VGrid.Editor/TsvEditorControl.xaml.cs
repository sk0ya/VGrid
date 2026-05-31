using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using VGrid.Commands;
using VGrid.Models;
using VGrid.Services;
using VGrid.UI;
using VGrid.ViewModels;
using VGrid.VimEngine;

namespace VGrid.Editor;

/// <summary>
/// WPF UserControl for editing a single TSV document with Vim keybindings.
/// Set the Tab property (TabItemViewModel) to display and edit a document.
/// </summary>
public partial class TsvEditorControl : UserControl, IEditorContext
{
    private DataGridManager? _dataGridManager;
    private SelectionManager? _selectionManager;
    private VimInputHandler? _vimInputHandler;

    public static readonly DependencyProperty TabProperty =
        DependencyProperty.Register(nameof(Tab), typeof(TabItemViewModel), typeof(TsvEditorControl),
            new PropertyMetadata(null, OnTabChanged));

    public static readonly DependencyProperty IsVimModeEnabledProperty =
        DependencyProperty.Register(nameof(IsVimModeEnabled), typeof(bool), typeof(TsvEditorControl),
            new PropertyMetadata(true));

    public static readonly DependencyProperty IsRestoringSessionProperty =
        DependencyProperty.Register(nameof(IsRestoringSession), typeof(bool), typeof(TsvEditorControl),
            new PropertyMetadata(false));

    public static readonly DependencyProperty ColumnWidthServiceProperty =
        DependencyProperty.Register(nameof(ColumnWidthService), typeof(IColumnWidthService), typeof(TsvEditorControl),
            new PropertyMetadata(null));

    /// <summary>Raised when the user requests Ctrl+S save.</summary>
    public event EventHandler? SaveRequested;

    /// <summary>Raised when the user triggers a host-specific action (e.g. "GitHistory" via Ctrl+G).</summary>
    public event EventHandler<CustomKeyActionEventArgs>? CustomKeyAction;

    public TsvEditorControl()
    {
        InitializeComponent();
        InitializeManagers();

        // Wire up VimState save request → SaveRequested event
        // (subscribed per-tab in OnTabChanged)
    }

    private void InitializeManagers()
    {
        _dataGridManager = new DataGridManager(this);
        _selectionManager = new SelectionManager(this);
        _vimInputHandler = new VimInputHandler(this);
        _vimInputHandler.CustomKeyAction += (s, e) => CustomKeyAction?.Invoke(this, e);
    }

    // --- IEditorContext implementation ---

    TabItemViewModel? IEditorContext.SelectedTab => Tab;

    bool IEditorContext.IsVimModeEnabled => IsVimModeEnabled;

    bool IEditorContext.IsRestoringSession => IsRestoringSession;

    IColumnWidthService IEditorContext.ColumnWidthService =>
        ColumnWidthService ?? new ColumnWidthService();

    // --- Public DependencyProperties ---

    public TabItemViewModel? Tab
    {
        get => (TabItemViewModel?)GetValue(TabProperty);
        set => SetValue(TabProperty, value);
    }

    public bool IsVimModeEnabled
    {
        get => (bool)GetValue(IsVimModeEnabledProperty);
        set => SetValue(IsVimModeEnabledProperty, value);
    }

    public bool IsRestoringSession
    {
        get => (bool)GetValue(IsRestoringSessionProperty);
        set => SetValue(IsRestoringSessionProperty, value);
    }

    public IColumnWidthService? ColumnWidthService
    {
        get => (IColumnWidthService?)GetValue(ColumnWidthServiceProperty);
        set => SetValue(ColumnWidthServiceProperty, value);
    }

    // --- Public methods for host app integration ---

    public void ScrollToCenter() => _dataGridManager?.ScrollToCenterForTab(Tab!);
    public void FocusCurrentTab() => _dataGridManager?.FocusCurrentTab(Tab!);
    public void RecalculateAllColumnWidths() => _dataGridManager?.RecalculateAllColumnWidths();

    // --- Property change callback ---

    private static void OnTabChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (TsvEditorControl)d;
        control.DataContext = e.NewValue;

        // Subscribe to the new tab's VimState save event
        if (e.OldValue is TabItemViewModel oldTab)
            oldTab.VimState.SaveRequested -= control.VimState_SaveRequested;

        if (e.NewValue is TabItemViewModel newTab)
            newTab.VimState.SaveRequested += control.VimState_SaveRequested;
    }

    private void VimState_SaveRequested(object? sender, EventArgs e)
    {
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    // --- DataGrid event handlers (delegate to managers) ---

    private void TsvGrid_Loaded(object sender, RoutedEventArgs e) =>
        _dataGridManager?.TsvGrid_Loaded(sender, e);

    private void TsvGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e) =>
        _dataGridManager?.TsvGrid_CellEditEnding(sender, e);

    private void TsvGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e) =>
        _dataGridManager?.TsvGrid_BeginningEdit(sender, e);

    private void TsvGrid_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) =>
        _dataGridManager?.TsvGrid_OnDataContextChanged(sender, e);

    // --- Row/Column header handlers (delegate to SelectionManager) ---

    private void RowHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _selectionManager?.RowHeader_PreviewMouseLeftButtonDown(sender, e);

    private void RowHeader_PreviewMouseMove(object sender, MouseEventArgs e) =>
        _selectionManager?.RowHeader_PreviewMouseMove(sender, e);

    private void RowHeader_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        _selectionManager?.RowHeader_PreviewMouseLeftButtonUp(sender, e);

    private void RowHeader_MouseRightButtonUp(object sender, MouseButtonEventArgs e) =>
        _selectionManager?.RowHeader_MouseRightButtonUp(sender, e);

    private void ColumnHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _selectionManager?.ColumnHeader_PreviewMouseLeftButtonDown(sender, e);

    private void ColumnHeader_PreviewMouseMove(object sender, MouseEventArgs e) =>
        _selectionManager?.ColumnHeader_PreviewMouseMove(sender, e);

    private void ColumnHeader_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        _selectionManager?.ColumnHeader_PreviewMouseLeftButtonUp(sender, e);

    private void ColumnHeader_MouseRightButtonUp(object sender, MouseButtonEventArgs e) =>
        _selectionManager?.ColumnHeader_MouseRightButtonUp(sender, e);

    // --- Keyboard handler ---

    private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e) =>
        _vimInputHandler?.Window_PreviewKeyDown(sender, e);

    // --- Context menu handlers ---

    private void ContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        CopyAsMarkdownTableMenuItem.Visibility = HasMultipleCellSelection()
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ContextMenu_Cut_Click(object sender, RoutedEventArgs e)
    {
        ContextMenu_Copy_Click(sender, e);
        ContextMenu_Delete_Click(sender, e);
    }

    private void ContextMenu_Copy_Click(object sender, RoutedEventArgs e)
    {
        var tab = Tab;
        if (tab == null) return;
        var state = tab.VimState;
        state.LastYank = CreateYankedContentFromCurrentSelection(tab);
        if (state.LastYank == null) return;

        ClipboardHelper.CopyToClipboard(state.LastYank);
        state.OnYankPerformed();
    }

    private void ContextMenu_CopyAsMarkdownTable_Click(object sender, RoutedEventArgs e)
    {
        var tab = Tab;
        if (tab == null || !HasMultipleCellSelection()) return;

        var state = tab.VimState;
        var yank = CreateYankedContentFromCurrentSelection(tab);
        if (yank == null) return;

        state.LastYank = yank;
        ClipboardHelper.CopyMarkdownTableToClipboard(yank);
        state.OnYankPerformed();
    }

    private void ContextMenu_Paste_Click(object sender, RoutedEventArgs e)
    {
        var tab = Tab;
        if (tab == null) return;
        var state = tab.VimState;
        var document = tab.GridViewModel.Document;
        var yank = state.LastYank ?? ClipboardHelper.ReadFromClipboard();
        if (yank == null) return;
        var command = new PasteCommand(document, state.CursorPosition, yank, pasteBefore: false);
        state.CommandHistory?.Execute(command);
        if (command.AffectedColumns.Any())
            state.OnColumnWidthUpdateRequested(command.AffectedColumns);
    }

    private void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
    {
        var tab = Tab;
        if (tab == null) return;
        var state = tab.VimState;
        var document = tab.GridViewModel.Document;
        if (state.CurrentSelection != null)
        {
            var command = new DeleteSelectionCommand(document, state.CurrentSelection);
            state.CommandHistory?.Execute(command);
            state.SwitchMode(VimMode.Normal);
        }
        else
        {
            if (state.CursorPosition.Row >= document.RowCount) return;
            state.CommandHistory?.Execute(new EditCellCommand(document, state.CursorPosition, string.Empty));
        }
    }

    private bool HasMultipleCellSelection()
    {
        var selection = Tab?.VimState.CurrentSelection;
        if (selection == null || Tab == null)
            return false;

        int columnCount = selection.Type == VisualType.Line
            ? Tab.GridViewModel.Document.ColumnCount
            : selection.ColumnCount;

        return selection.RowCount * columnCount > 1;
    }

    internal static YankedContent? CreateYankedContentFromCurrentSelection(TabItemViewModel tab)
    {
        var state = tab.VimState;
        var document = tab.GridViewModel.Document;

        if (state.CurrentSelection != null)
        {
            var selection = state.CurrentSelection;
            int startColumn = selection.Type == VisualType.Line ? 0 : selection.StartColumn;
            int columnCount = selection.Type == VisualType.Line ? document.ColumnCount : selection.ColumnCount;
            var values = new string[selection.RowCount, columnCount];
            for (int r = 0; r < selection.RowCount; r++)
                for (int c = 0; c < columnCount; c++)
                {
                    int docRow = selection.StartRow + r;
                    int docCol = startColumn + c;
                    values[r, c] = docRow < document.RowCount && docCol < document.Rows[docRow].Cells.Count
                        ? document.Rows[docRow].Cells[docCol].Value
                        : string.Empty;
                }

            return new YankedContent
            {
                Values = values,
                SourceType = selection.Type,
                Rows = selection.RowCount,
                Columns = columnCount
            };
        }

        var cell = document.GetCell(state.CursorPosition);
        if (cell == null) return null;

        var singleCell = new string[1, 1];
        singleCell[0, 0] = cell.Value;
        return new YankedContent
        {
            Values = singleCell,
            SourceType = VisualType.Character,
            Rows = 1,
            Columns = 1
        };
    }
}
