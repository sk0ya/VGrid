using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using VGrid.Commands;
using VGrid.Editor;
using VGrid.Models;
using VGrid.ViewModels;
using VGrid.VimEngine;

namespace VGrid.UI;

/// <summary>
/// Manages row and column header selection and context-menu operations.
/// </summary>
public class SelectionManager
{
    private readonly IEditorContext _context;

    private bool _isDraggingRow;
    private bool _isDraggingColumn;
    private int _dragStartRowIndex = -1;
    private int _dragStartColumnIndex = -1;
    private DataGridRowHeader? _capturedRowHeader;
    private DataGridColumnHeader? _capturedColumnHeader;

    public SelectionManager(IEditorContext context)
    {
        _context = context;
    }

    public void RowHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridRowHeader header) return;

        var row = FindVisualParent<DataGridRow>(header);
        if (row == null) return;

        int rowIndex = row.GetIndex();
        var tab = _context.SelectedTab;
        if (tab == null || rowIndex < 0 || rowIndex >= tab.Document.RowCount) return;

        bool isCtrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        bool isShift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        _isDraggingRow = true;
        _dragStartRowIndex = rowIndex;
        _capturedRowHeader = header;
        Mouse.Capture(header);

        HandleRowSelection(tab, rowIndex, isCtrl, isShift);
        e.Handled = true;
    }

    public void ColumnHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridColumnHeader header) return;

        int columnIndex = header.Column.DisplayIndex;
        var tab = _context.SelectedTab;
        if (tab == null || columnIndex < 0 || columnIndex >= tab.Document.ColumnCount) return;

        bool isCtrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        bool isShift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        _isDraggingColumn = true;
        _dragStartColumnIndex = columnIndex;
        _capturedColumnHeader = header;
        Mouse.Capture(header);

        HandleColumnSelection(tab, columnIndex, isCtrl, isShift);
        e.Handled = true;
    }

    private void HandleRowSelection(TabItemViewModel tab, int rowIndex, bool isCtrl, bool isShift)
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
        else if (isShift)
        {
            tab.VimState.SetRowRangeSelection(rowIndex);
        }
        else if (isCtrl)
        {
            tab.VimState.ToggleRowSelection(rowIndex);
            if (tab.VimState.SelectedRows.Count == 0)
                tab.VimState.SwitchMode(VimMode.Normal);
        }
        else
        {
            tab.VimState.SetSingleRowSelection(rowIndex);
            tab.VimState.CursorPosition = new GridPosition(rowIndex, 0);
        }

        UpdateHeaderSelectionHighlighting(tab);
    }

    private void HandleColumnSelection(TabItemViewModel tab, int columnIndex, bool isCtrl, bool isShift)
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
        else if (isShift)
        {
            tab.VimState.SetColumnRangeSelection(columnIndex);
        }
        else if (isCtrl)
        {
            tab.VimState.ToggleColumnSelection(columnIndex);
            if (tab.VimState.SelectedColumns.Count == 0)
                tab.VimState.SwitchMode(VimMode.Normal);
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
            foreach (var cell in row.Cells)
                cell.IsSelected = false;

        if (tab.VimState.SelectedRows.Count > 0)
        {
            foreach (int rowIndex in tab.VimState.SelectedRows)
                if (rowIndex >= 0 && rowIndex < tab.Document.RowCount)
                    foreach (var cell in tab.Document.Rows[rowIndex].Cells)
                        cell.IsSelected = true;
        }
        else if (tab.VimState.SelectedColumns.Count > 0)
        {
            foreach (var row in tab.Document.Rows)
                foreach (int colIndex in tab.VimState.SelectedColumns)
                    if (colIndex >= 0 && colIndex < row.Cells.Count)
                        row.Cells[colIndex].IsSelected = true;
        }
    }

    public void RowHeader_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingRow || _dragStartRowIndex < 0 || _capturedRowHeader == null) return;

        var tab = _context.SelectedTab;
        if (tab == null) return;

        var grid = FindVisualParent<DataGrid>(_capturedRowHeader);
        if (grid == null) return;

        var point = e.GetPosition(grid);
        var hitResult = VisualTreeHelper.HitTest(grid, point);
        if (hitResult?.VisualHit == null) return;

        var row = FindVisualParent<DataGridRow>(hitResult.VisualHit);
        if (row == null) return;

        int currentRowIndex = row.GetIndex();
        if (currentRowIndex < 0 || currentRowIndex >= tab.Document.RowCount) return;

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
        if (!_isDraggingColumn || _dragStartColumnIndex < 0 || _capturedColumnHeader == null) return;

        var tab = _context.SelectedTab;
        if (tab == null) return;

        var grid = FindVisualParent<DataGrid>(_capturedColumnHeader);
        if (grid == null) return;

        var point = e.GetPosition(grid);
        var hitResult = VisualTreeHelper.HitTest(grid, point);
        if (hitResult?.VisualHit == null) return;

        var columnHeader = FindVisualParent<DataGridColumnHeader>(hitResult.VisualHit);
        if (columnHeader?.Column == null) return;

        int currentColumnIndex = columnHeader.Column.DisplayIndex;
        if (currentColumnIndex < 0 || currentColumnIndex >= tab.Document.ColumnCount) return;

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
        if (sender is not DataGridRowHeader header) return;

        var row = FindVisualParent<DataGridRow>(header);
        if (row == null) return;

        int rowIndex = row.GetIndex();
        var tab = _context.SelectedTab;
        if (tab == null) return;

        var contextMenu = new ContextMenu();

        var insertAbove = new MenuItem { Header = "Insert Row Above" };
        insertAbove.Click += (s, args) =>
        {
            var cmd = new InsertRowCommand(tab.Document, rowIndex);
            tab.VimState.CommandHistory?.Execute(cmd);
        };
        contextMenu.Items.Add(insertAbove);

        var insertBelow = new MenuItem { Header = "Insert Row Below" };
        insertBelow.Click += (s, args) =>
        {
            var cmd = new InsertRowCommand(tab.Document, rowIndex + 1);
            tab.VimState.CommandHistory?.Execute(cmd);
        };
        contextMenu.Items.Add(insertBelow);

        contextMenu.Items.Add(new Separator());

        var selectedRows = tab.VimState.SelectedRows;
        if (selectedRows.Count > 1)
        {
            var rows = selectedRows.OrderByDescending(r => r).ToList();
            var deleteMultiple = new MenuItem { Header = $"Delete {rows.Count} Rows" };
            deleteMultiple.Click += (s, args) =>
            {
                foreach (int r in rows)
                {
                    var cmd = new DeleteRowCommand(tab.Document, r);
                    tab.VimState.CommandHistory?.Execute(cmd);
                }
            };
            contextMenu.Items.Add(deleteMultiple);
        }
        else
        {
            var deleteRow = new MenuItem { Header = "Delete Row" };
            deleteRow.Click += (s, args) =>
            {
                var cmd = new DeleteRowCommand(tab.Document, rowIndex);
                tab.VimState.CommandHistory?.Execute(cmd);
            };
            contextMenu.Items.Add(deleteRow);
        }

        header.ContextMenu = contextMenu;
        contextMenu.IsOpen = true;
    }

    public void ColumnHeader_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGridColumnHeader header) return;

        int columnIndex = header.Column.DisplayIndex;
        var tab = _context.SelectedTab;
        if (tab == null) return;

        var contextMenu = new ContextMenu();

        var insertLeft = new MenuItem { Header = "Insert Column Left" };
        insertLeft.Click += (s, args) =>
        {
            var cmd = new InsertColumnCommand(tab.Document, columnIndex);
            tab.VimState.CommandHistory?.Execute(cmd);
        };
        contextMenu.Items.Add(insertLeft);

        var insertRight = new MenuItem { Header = "Insert Column Right" };
        insertRight.Click += (s, args) =>
        {
            var cmd = new InsertColumnCommand(tab.Document, columnIndex + 1);
            tab.VimState.CommandHistory?.Execute(cmd);
        };
        contextMenu.Items.Add(insertRight);

        contextMenu.Items.Add(new Separator());

        var selectedColumns = tab.VimState.SelectedColumns;
        if (selectedColumns.Count > 1)
        {
            var cols = selectedColumns.OrderByDescending(c => c).ToList();
            var deleteMultiple = new MenuItem { Header = $"Delete {cols.Count} Columns" };
            deleteMultiple.Click += (s, args) =>
            {
                foreach (int col in cols)
                {
                    var cmd = new DeleteColumnCommand(tab.Document, col);
                    tab.VimState.CommandHistory?.Execute(cmd);
                }
            };
            contextMenu.Items.Add(deleteMultiple);
        }
        else
        {
            var deleteColumn = new MenuItem { Header = "Delete Column" };
            deleteColumn.Click += (s, args) =>
            {
                var cmd = new DeleteColumnCommand(tab.Document, columnIndex);
                tab.VimState.CommandHistory?.Execute(cmd);
            };
            contextMenu.Items.Add(deleteColumn);
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
