using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using VGrid.Models;
using VGrid.ViewModels;
using VGrid.VimEngine;

namespace VGrid.UI;

/// <summary>
/// Manages row and column header selection operations
/// </summary>
public class SelectionManager
{
    private readonly MainViewModel _viewModel;

    // Drag selection state
    private bool _isDraggingRow;
    private bool _isDraggingColumn;
    private int _dragStartRowIndex = -1;
    private int _dragStartColumnIndex = -1;
    private DataGridRowHeader? _capturedRowHeader;
    private DataGridColumnHeader? _capturedColumnHeader;

    public SelectionManager(MainViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public void RowHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var header = sender as DataGridRowHeader;
        if (header == null || _viewModel == null)
            return;

        var grid = FindVisualParent<DataGrid>(header);
        if (grid == null)
            return;

        var row = FindVisualParent<DataGridRow>(header);
        if (row == null)
            return;

        int rowIndex = row.GetIndex();
        var tab = _viewModel.SelectedTab;

        if (tab == null || rowIndex < 0 || rowIndex >= tab.Document.RowCount)
            return;

        bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        // Start drag selection
        _isDraggingRow = true;
        _dragStartRowIndex = rowIndex;
        _capturedRowHeader = header;
        Mouse.Capture(header);

        HandleRowSelection(tab, rowIndex, isCtrlPressed, isShiftPressed);
        e.Handled = true;
    }

    public void ColumnHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var header = sender as DataGridColumnHeader;
        if (header == null || _viewModel == null)
            return;

        var grid = FindVisualParent<DataGrid>(header);
        if (grid == null)
            return;

        int columnIndex = header.Column.DisplayIndex;
        var tab = _viewModel.SelectedTab;

        if (tab == null || columnIndex < 0 || columnIndex >= tab.Document.ColumnCount)
            return;

        bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        // Start drag selection
        _isDraggingColumn = true;
        _dragStartColumnIndex = columnIndex;
        _capturedColumnHeader = header;
        Mouse.Capture(header);

        HandleColumnSelection(tab, columnIndex, isCtrlPressed, isShiftPressed);
        e.Handled = true;
    }

    private void HandleRowSelection(TabItemViewModel tab, int rowIndex, bool isCtrlPressed, bool isShiftPressed)
    {
        if (tab.VimState.CurrentMode != VimMode.Visual ||
            tab.VimState.CurrentSelection?.Type != VisualType.Line)
        {
            tab.VimState.ClearColumnSelections();
            tab.VimState.SetSingleRowSelection(rowIndex);
            tab.VimState.CursorPosition = new GridPosition(rowIndex, 0);

            tab.VimState.CurrentSelection = new SelectionRange(
                VisualType.Line,
                new GridPosition(rowIndex, 0),
                new GridPosition(rowIndex, tab.Document.ColumnCount - 1));

            tab.VimState.SwitchMode(VimMode.Visual);
        }
        else if (isShiftPressed)
        {
            tab.VimState.SetRowRangeSelection(rowIndex);
        }
        else if (isCtrlPressed)
        {
            tab.VimState.ToggleRowSelection(rowIndex);

            if (tab.VimState.SelectedRows.Count == 0)
            {
                tab.VimState.SwitchMode(VimMode.Normal);
            }
        }
        else
        {
            tab.VimState.SetSingleRowSelection(rowIndex);
            tab.VimState.CursorPosition = new GridPosition(rowIndex, 0);
        }

        UpdateHeaderSelectionHighlighting(tab);
    }

    private void HandleColumnSelection(TabItemViewModel tab, int columnIndex, bool isCtrlPressed, bool isShiftPressed)
    {
        if (tab.VimState.CurrentMode != VimMode.Visual ||
            tab.VimState.CurrentSelection?.Type != VisualType.Block)
        {
            tab.VimState.ClearRowSelections();
            tab.VimState.SetSingleColumnSelection(columnIndex);
            tab.VimState.CursorPosition = new GridPosition(0, columnIndex);

            tab.VimState.CurrentSelection = new SelectionRange(
                VisualType.Block,
                new GridPosition(0, columnIndex),
                new GridPosition(tab.Document.RowCount - 1, columnIndex));

            tab.VimState.SwitchMode(VimMode.Visual);
        }
        else if (isShiftPressed)
        {
            tab.VimState.SetColumnRangeSelection(columnIndex);
        }
        else if (isCtrlPressed)
        {
            tab.VimState.ToggleColumnSelection(columnIndex);

            if (tab.VimState.SelectedColumns.Count == 0)
            {
                tab.VimState.SwitchMode(VimMode.Normal);
            }
        }
        else
        {
            tab.VimState.SetSingleColumnSelection(columnIndex);
            tab.VimState.CursorPosition = new GridPosition(0, columnIndex);
        }

        UpdateHeaderSelectionHighlighting(tab);
    }

    private void UpdateHeaderSelectionHighlighting(TabItemViewModel tab)
    {
        foreach (var row in tab.Document.Rows)
        {
            foreach (var cell in row.Cells)
            {
                cell.IsSelected = false;
            }
        }

        if (tab.VimState.SelectedRows.Count > 0)
        {
            foreach (int rowIndex in tab.VimState.SelectedRows)
            {
                if (rowIndex >= 0 && rowIndex < tab.Document.RowCount)
                {
                    var row = tab.Document.Rows[rowIndex];
                    foreach (var cell in row.Cells)
                    {
                        cell.IsSelected = true;
                    }
                }
            }
        }
        else if (tab.VimState.SelectedColumns.Count > 0)
        {
            foreach (var row in tab.Document.Rows)
            {
                foreach (int colIndex in tab.VimState.SelectedColumns)
                {
                    if (colIndex >= 0 && colIndex < row.Cells.Count)
                    {
                        row.Cells[colIndex].IsSelected = true;
                    }
                }
            }
        }
    }

    public void RowHeader_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingRow || _dragStartRowIndex < 0 || _capturedRowHeader == null)
            return;

        var tab = _viewModel.SelectedTab;
        if (tab == null)
            return;

        var grid = FindVisualParent<DataGrid>(_capturedRowHeader);
        if (grid == null)
            return;

        // Get the row under the mouse cursor
        var point = e.GetPosition(grid);
        var hitResult = VisualTreeHelper.HitTest(grid, point);
        if (hitResult?.VisualHit == null)
            return;

        var row = FindVisualParent<DataGridRow>(hitResult.VisualHit);
        if (row == null)
            return;

        int currentRowIndex = row.GetIndex();
        if (currentRowIndex < 0 || currentRowIndex >= tab.Document.RowCount)
            return;

        // Update selection range
        int startRow = Math.Min(_dragStartRowIndex, currentRowIndex);
        int endRow = Math.Max(_dragStartRowIndex, currentRowIndex);

        tab.VimState.SetRowRangeSelectionFromTo(startRow, endRow);
        tab.VimState.CursorPosition = new GridPosition(currentRowIndex, 0);

        tab.VimState.CurrentSelection = new SelectionRange(
            VisualType.Line,
            new GridPosition(startRow, 0),
            new GridPosition(endRow, tab.Document.ColumnCount - 1));

        UpdateHeaderSelectionHighlighting(tab);
    }

    public void RowHeader_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingRow)
        {
            _isDraggingRow = false;
            _dragStartRowIndex = -1;
            Mouse.Capture(null);
            _capturedRowHeader = null;
        }
    }

    public void ColumnHeader_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingColumn || _dragStartColumnIndex < 0 || _capturedColumnHeader == null)
            return;

        var tab = _viewModel.SelectedTab;
        if (tab == null)
            return;

        var grid = FindVisualParent<DataGrid>(_capturedColumnHeader);
        if (grid == null)
            return;

        // Get the column under the mouse cursor
        var point = e.GetPosition(grid);
        var hitResult = VisualTreeHelper.HitTest(grid, point);
        if (hitResult?.VisualHit == null)
            return;

        var columnHeader = FindVisualParent<DataGridColumnHeader>(hitResult.VisualHit);
        if (columnHeader?.Column == null)
            return;

        int currentColumnIndex = columnHeader.Column.DisplayIndex;
        if (currentColumnIndex < 0 || currentColumnIndex >= tab.Document.ColumnCount)
            return;

        // Update selection range
        int startCol = Math.Min(_dragStartColumnIndex, currentColumnIndex);
        int endCol = Math.Max(_dragStartColumnIndex, currentColumnIndex);

        tab.VimState.SetColumnRangeSelectionFromTo(startCol, endCol);
        tab.VimState.CursorPosition = new GridPosition(0, currentColumnIndex);

        tab.VimState.CurrentSelection = new SelectionRange(
            VisualType.Block,
            new GridPosition(0, startCol),
            new GridPosition(tab.Document.RowCount - 1, endCol));

        UpdateHeaderSelectionHighlighting(tab);
    }

    public void ColumnHeader_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingColumn)
        {
            _isDraggingColumn = false;
            _dragStartColumnIndex = -1;
            Mouse.Capture(null);
            _capturedColumnHeader = null;
        }
    }

    public void RowHeader_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var header = sender as DataGridRowHeader;
        if (header == null || _viewModel == null)
            return;

        var row = FindVisualParent<DataGridRow>(header);
        if (row == null)
            return;

        int rowIndex = row.GetIndex();
        var tab = _viewModel.SelectedTab;

        var contextMenu = new ContextMenu();

        var insertAboveMenuItem = new MenuItem { Header = "Insert Row Above" };
        insertAboveMenuItem.Click += (s, args) => _viewModel.InsertRowAboveCommand.Execute(rowIndex);
        contextMenu.Items.Add(insertAboveMenuItem);

        var insertBelowMenuItem = new MenuItem { Header = "Insert Row Below" };
        insertBelowMenuItem.Click += (s, args) => _viewModel.InsertRowBelowCommand.Execute(rowIndex);
        contextMenu.Items.Add(insertBelowMenuItem);

        contextMenu.Items.Add(new Separator());

        // Check if multiple rows are selected
        var selectedRows = tab?.VimState.SelectedRows;
        if (selectedRows != null && selectedRows.Count > 1)
        {
            var deleteRowsMenuItem = new MenuItem { Header = $"Delete {selectedRows.Count} Rows" };
            deleteRowsMenuItem.Click += (s, args) => _viewModel.DeleteSelectedRows(selectedRows.ToList());
            contextMenu.Items.Add(deleteRowsMenuItem);
        }
        else
        {
            var deleteRowMenuItem = new MenuItem { Header = "Delete Row" };
            deleteRowMenuItem.Click += (s, args) => _viewModel.DeleteRowCommand.Execute(rowIndex);
            contextMenu.Items.Add(deleteRowMenuItem);
        }

        header.ContextMenu = contextMenu;
        contextMenu.IsOpen = true;
    }

    public void ColumnHeader_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var header = sender as DataGridColumnHeader;
        if (header == null || _viewModel == null)
            return;

        int columnIndex = header.Column.DisplayIndex;
        var tab = _viewModel.SelectedTab;

        var contextMenu = new ContextMenu();

        var insertLeftMenuItem = new MenuItem { Header = "Insert Column Left" };
        insertLeftMenuItem.Click += (s, args) => _viewModel.InsertColumnLeftCommand.Execute(columnIndex);
        contextMenu.Items.Add(insertLeftMenuItem);

        var insertRightMenuItem = new MenuItem { Header = "Insert Column Right" };
        insertRightMenuItem.Click += (s, args) => _viewModel.InsertColumnRightCommand.Execute(columnIndex);
        contextMenu.Items.Add(insertRightMenuItem);

        contextMenu.Items.Add(new Separator());

        // Check if multiple columns are selected
        var selectedColumns = tab?.VimState.SelectedColumns;
        if (selectedColumns != null && selectedColumns.Count > 1)
        {
            var deleteColumnsMenuItem = new MenuItem { Header = $"Delete {selectedColumns.Count} Columns" };
            deleteColumnsMenuItem.Click += (s, args) => _viewModel.DeleteSelectedColumns(selectedColumns.ToList());
            contextMenu.Items.Add(deleteColumnsMenuItem);
        }
        else
        {
            var deleteColumnMenuItem = new MenuItem { Header = "Delete Column" };
            deleteColumnMenuItem.Click += (s, args) => _viewModel.DeleteColumnCommand.Execute(columnIndex);
            contextMenu.Items.Add(deleteColumnMenuItem);
        }

        header.ContextMenu = contextMenu;
        contextMenu.IsOpen = true;
    }

    private T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        while (child != null)
        {
            child = VisualTreeHelper.GetParent(child);
            if (child is T parent)
                return parent;
        }

        return null;
    }
}
