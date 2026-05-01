using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using VGrid.Commands;
using VGrid.Editor;
using VGrid.Models;
using VGrid.ViewModels;
using VGrid.VimEngine;

namespace VGrid.UI;

/// <summary>
/// Manages DataGrid operations including column generation, selection, editing, and visual mode.
/// </summary>
public class DataGridManager
{
    private readonly IEditorContext _context;
    private bool _isUpdatingSelection = false;
    private bool _isDataGridDragging = false;
    private GridPosition? _dragStartPosition = null;

    private readonly Dictionary<DataGrid, (TabItemViewModel tab, PropertyChangedEventHandler vimStateHandler, PropertyChangedEventHandler documentHandler, EventHandler<IEnumerable<int>> columnWidthHandler)> _dataGridHandlers
        = new();

    private readonly Dictionary<TabItemViewModel, DataGrid> _tabToDataGrid = new();

    private readonly Dictionary<TabItemViewModel, (PropertyChangedEventHandler vimStateHandler, PropertyChangedEventHandler documentHandler, EventHandler<IEnumerable<int>> columnWidthHandler)> _tabHandlers
        = new();

    private readonly Dictionary<DataGrid, int> _dataGridColumnCount = new();

    private Style? _cachedEditingStyle;
    private readonly Dictionary<int, Style> _cachedCellStyles = new();

    private string? _pendingTextInput = null;

    private TextBox? _currentEditingTextBox = null;
    private TextChangedEventHandler? _currentTextChangedHandler = null;

    private void UnsubscribeFromCurrentEditingTextBox()
    {
        if (_currentEditingTextBox != null && _currentTextChangedHandler != null)
            _currentEditingTextBox.TextChanged -= _currentTextChangedHandler;
        _currentEditingTextBox = null;
        _currentTextChangedHandler = null;
    }

    public DataGridManager(IEditorContext context)
    {
        _context = context;
    }

    public void TsvGrid_Loaded(object sender, RoutedEventArgs e)
    {
        var grid = sender as DataGrid;
        if (grid == null) return;

        var tabItem = grid.DataContext as TabItemViewModel;
        if (tabItem == null)
        {
            DependencyPropertyChangedEventHandler? handler = null;
            handler = (s, args) =>
            {
                var tab = args.NewValue as TabItemViewModel;
                if (tab != null)
                {
                    grid.DataContextChanged -= handler;
                    InitializeDataGrid(grid, tab);
                    SetupVimStateHandlers(grid, tab);
                }
            };
            grid.DataContextChanged += handler;
            return;
        }

        InitializeDataGrid(grid, tabItem);
        SetupVimStateHandlers(grid, tabItem);
    }

    private void SetupVimStateHandlers(DataGrid grid, TabItemViewModel tab)
    {
        if (_dataGridHandlers.TryGetValue(grid, out var existingInfo) && existingInfo.tab == tab)
            return;

        if (existingInfo.tab != null)
        {
            existingInfo.tab.VimState.PropertyChanged -= existingInfo.vimStateHandler;
            existingInfo.tab.Document.PropertyChanged -= existingInfo.documentHandler;
            existingInfo.tab.VimState.ColumnWidthUpdateRequested -= existingInfo.columnWidthHandler;
            _dataGridHandlers.Remove(grid);
            _tabToDataGrid.Remove(existingInfo.tab);
        }

        if (!_tabHandlers.TryGetValue(tab, out var handlers))
        {
            PropertyChangedEventHandler vimStateHandler = (s, evt) =>
            {
                if (evt.PropertyName == nameof(tab.VimState.CursorPosition) && tab == _context?.SelectedTab)
                {
                    if (_tabToDataGrid.TryGetValue(tab, out var g))
                        UpdateDataGridSelection(g, tab);
                }
                else if (evt.PropertyName == nameof(tab.VimState.CurrentMode) && tab == _context?.SelectedTab)
                {
                    if (_tabToDataGrid.TryGetValue(tab, out var g))
                        HandleModeChange(g, tab);
                }
                else if (evt.PropertyName == nameof(tab.VimState.CurrentSelection) && tab == _context?.SelectedTab && tab.VimState.CurrentMode == VimMode.Visual)
                {
                    InitializeVisualSelection(tab);
                }
            };

            PropertyChangedEventHandler documentHandler = (s, evt) =>
            {
                if (evt.PropertyName == nameof(TsvDocument.ColumnCount) && tab == _context?.SelectedTab)
                {
                    if (_tabToDataGrid.TryGetValue(tab, out var g))
                        GenerateColumns(g, tab);
                }
            };

            EventHandler<IEnumerable<int>> columnWidthHandler = (s, columnIndices) =>
            {
                if (tab == _context?.SelectedTab)
                {
                    if (_tabToDataGrid.TryGetValue(tab, out var g))
                        AutoFitColumns(g, tab, columnIndices);
                }
            };

            handlers = (vimStateHandler, documentHandler, columnWidthHandler);
            _tabHandlers[tab] = handlers;
        }

        tab.VimState.PropertyChanged += handlers.vimStateHandler;
        tab.Document.PropertyChanged += handlers.documentHandler;
        tab.VimState.ColumnWidthUpdateRequested += handlers.columnWidthHandler;

        _dataGridHandlers[grid] = (tab, handlers.vimStateHandler, handlers.documentHandler, handlers.columnWidthHandler);
        _tabToDataGrid[tab] = grid;

        bool suppressFocusOnLoad = _context.IsRestoringSession;
        grid.Dispatcher.BeginInvoke(new Action(() =>
        {
            UpdateDataGridSelection(grid, tab, suppressFocusOnLoad);
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void InitializeDataGrid(DataGrid grid, TabItemViewModel tabItem)
    {
        if (grid.Tag as string == "Initialized") return;
        grid.Tag = "Initialized";

        try
        {
            GenerateColumns(grid, tabItem);

            if (tabItem.ColumnWidths.Count == 0)
                AutoFitAllColumns(grid, tabItem);

            grid.CurrentCellChanged -= TsvGrid_CurrentCellChangedHandler;
            grid.CurrentCellChanged += TsvGrid_CurrentCellChangedHandler;

            grid.PreparingCellForEdit -= TsvGrid_PreparingCellForEdit;
            grid.PreparingCellForEdit += TsvGrid_PreparingCellForEdit;

            grid.PreviewMouseLeftButtonDown -= TsvGrid_PreviewMouseLeftButtonDown;
            grid.PreviewMouseLeftButtonDown += TsvGrid_PreviewMouseLeftButtonDown;
            grid.PreviewMouseLeftButtonUp -= TsvGrid_PreviewMouseLeftButtonUp;
            grid.PreviewMouseLeftButtonUp += TsvGrid_PreviewMouseLeftButtonUp;

            grid.PreviewTextInput -= TsvGrid_PreviewTextInput;
            grid.PreviewTextInput += TsvGrid_PreviewTextInput;

            grid.PreviewMouseWheel -= TsvGrid_PreviewMouseWheel;
            grid.PreviewMouseWheel += TsvGrid_PreviewMouseWheel;

            grid.LoadingRow += (s, evt) => { evt.Row.Header = (evt.Row.GetIndex() + 1).ToString(); };

            grid.ItemContainerGenerator.StatusChanged += (s, evt) =>
            {
                if (grid.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                    UpdateAllRowHeaders(grid);
            };

            bool suppressFocusOnInit = _context.IsRestoringSession;
            grid.Dispatcher.BeginInvoke(new Action(() => { UpdateDataGridSelection(grid, tabItem, suppressFocusOnInit); }),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading grid: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateColumnWidths(DataGrid grid, TabItemViewModel tab)
    {
        if (grid == null || tab == null) return;
        for (int i = 0; i < grid.Columns.Count; i++)
        {
            if (grid.Columns[i] is DataGridTextColumn textColumn)
            {
                textColumn.Width = tab.ColumnWidths.ContainsKey(i)
                    ? new DataGridLength(tab.ColumnWidths[i], DataGridLengthUnitType.Pixel)
                    : new DataGridLength(100, DataGridLengthUnitType.Pixel);
            }
        }
    }

    private void GenerateColumns(DataGrid grid, TabItemViewModel tab)
    {
        if (grid == null || tab == null) return;

        var requiredColumnCount = Math.Max(20, tab.GridViewModel.ColumnCount);

        if (_dataGridColumnCount.TryGetValue(grid, out var existingCount) && existingCount == requiredColumnCount)
        {
            UpdateColumnWidths(grid, tab);
            return;
        }

        grid.Columns.Clear();
        _cachedEditingStyle ??= CreateEditingStyle();

        for (int i = 0; i < requiredColumnCount; i++)
        {
            if (!_cachedCellStyles.TryGetValue(i, out var cellStyle))
            {
                cellStyle = CreateVisualModeCellStyle(i);
                _cachedCellStyles[i] = cellStyle;
            }

            var column = new DataGridTextColumn
            {
                Header = GetExcelColumnName(i),
                Binding = new Binding($"Cells[{i}].Value")
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                },
                Width = tab.ColumnWidths.ContainsKey(i)
                    ? new DataGridLength(tab.ColumnWidths[i], DataGridLengthUnitType.Pixel)
                    : new DataGridLength(100, DataGridLengthUnitType.Pixel),
                MinWidth = 60,
                CellStyle = cellStyle,
                EditingElementStyle = _cachedEditingStyle
            };

            grid.Columns.Add(column);
        }

        _dataGridColumnCount[grid] = requiredColumnCount;
    }

    private void AutoFitAllColumns(DataGrid grid, TabItemViewModel tab)
    {
        if (grid == null || tab == null) return;

        var typeface = new Typeface(
            grid.FontFamily ?? new FontFamily("Segoe UI"),
            grid.FontStyle, grid.FontWeight, grid.FontStretch);
        double fontSize = grid.FontSize > 0 ? grid.FontSize : 11;

        var widths = _context.ColumnWidthService.CalculateAllColumnWidths(tab.Document, typeface, fontSize);
        tab.ColumnWidths = widths;
        tab.ResetManualResizeTracking();

        for (int i = 0; i < grid.Columns.Count && i < widths.Count; i++)
        {
            if (grid.Columns[i] is DataGridTextColumn column)
                column.Width = new DataGridLength(widths[i], DataGridLengthUnitType.Pixel);
        }
    }

    private void AutoFitColumn(DataGrid grid, TabItemViewModel tab, int columnIndex)
    {
        if (grid == null || tab == null) return;
        if (tab.ManuallyResizedColumns.Contains(columnIndex)) return;

        var typeface = new Typeface(
            grid.FontFamily ?? new FontFamily("Segoe UI"),
            grid.FontStyle, grid.FontWeight, grid.FontStretch);
        double fontSize = grid.FontSize > 0 ? grid.FontSize : 11;

        double width = _context.ColumnWidthService.CalculateColumnWidth(tab.Document, columnIndex, typeface, fontSize);
        tab.ColumnWidths[columnIndex] = width;

        if (columnIndex < grid.Columns.Count && grid.Columns[columnIndex] is DataGridTextColumn column)
            column.Width = new DataGridLength(width, DataGridLengthUnitType.Pixel);
    }

    public void AutoFitColumns(DataGrid grid, TabItemViewModel tab, IEnumerable<int> columnIndices)
    {
        if (grid == null || tab == null || columnIndices == null) return;
        foreach (var columnIndex in columnIndices)
            AutoFitColumn(grid, tab, columnIndex);
    }

    public void TsvGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        UnsubscribeFromCurrentEditingTextBox();
        if (_context?.SelectedTab == null || e.Cancel) return;

        var grid = sender as DataGrid;
        if (grid == null) return;

        var tab = _context.SelectedTab;
        int columnIndex = e.Column.DisplayIndex;
        int rowIndex = e.Row != null ? tab.GridViewModel.Document.Rows.IndexOf((Row)e.Row.Item) : -1;

        if (tab.VimState.InsertModeStartPosition != null && e.EditingElement is TextBox textBox)
        {
            string newValue = textBox.Text;
            string originalValue = tab.VimState.InsertModeOriginalValue;

            if (newValue != originalValue && rowIndex >= 0 && columnIndex >= 0)
            {
                if (tab.VimState.PendingInsertType != ChangeType.None)
                {
                    string insertedText = ExtractInsertedText(originalValue, newValue, tab.VimState.CellEditCaretPosition);
                    tab.VimState.LastChange = new LastChange
                    {
                        Type = tab.VimState.PendingInsertType,
                        Count = 1,
                        InsertedText = insertedText,
                        CaretPosition = tab.VimState.CellEditCaretPosition
                    };
                }

                var position = new GridPosition(rowIndex, columnIndex);
                var command = new EditCellCommand(tab.GridViewModel.Document, position, newValue, originalValue);
                tab.VimState.CommandHistory?.AddExecutedCommand(command);
            }

            tab.VimState.PendingInsertType = ChangeType.None;
            tab.VimState.InsertModeStartPosition = null;
            tab.VimState.InsertModeOriginalValue = string.Empty;
        }

        grid.Dispatcher.BeginInvoke(new Action(() =>
        {
            AutoFitColumn(grid, tab, columnIndex);
            tab.RefreshSelectedCellContent();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private Style CreateEditingStyle()
    {
        var editingStyle = new Style(typeof(TextBox));
        editingStyle.Setters.Add(new Setter(TextBox.BackgroundProperty, new DynamicResourceExtension("DataGridBackgroundBrush")));
        editingStyle.Setters.Add(new Setter(TextBox.ForegroundProperty, new DynamicResourceExtension("DataGridForegroundBrush")));
        editingStyle.Setters.Add(new Setter(TextBox.CaretBrushProperty, new DynamicResourceExtension("DataGridForegroundBrush")));
        editingStyle.Setters.Add(new Setter(TextBox.BorderThicknessProperty, new Thickness(0)));
        editingStyle.Setters.Add(new Setter(TextBox.PaddingProperty, new Thickness(2, 0, 2, 0)));
        return editingStyle;
    }

    private Style CreateVisualModeCellStyle(int columnIndex)
    {
        var style = new Style(typeof(DataGridCell));
        style.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, new DynamicResourceExtension("DataGridCellBorderBrush")));
        style.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        style.Setters.Add(new Setter(DataGridCell.PaddingProperty, new Thickness(4, 2, 4, 2)));

        var selectionTrigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        selectionTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty, new DynamicResourceExtension("DataGridCellSelectedBrush")));
        selectionTrigger.Setters.Add(new Setter(DataGridCell.ForegroundProperty, new DynamicResourceExtension("DataGridForegroundBrush")));
        selectionTrigger.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, new DynamicResourceExtension("DataGridCellSelectedBorderBrush")));
        selectionTrigger.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));

        var visualTrigger = new DataTrigger();
        visualTrigger.Binding = new Binding($"Cells[{columnIndex}].IsSelected");
        visualTrigger.Value = true;
        visualTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty, new DynamicResourceExtension("DataGridCellSelectedBrush")));
        visualTrigger.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, new DynamicResourceExtension("DataGridCellSelectedBorderBrush")));
        visualTrigger.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        style.Triggers.Add(visualTrigger);
        style.Triggers.Add(selectionTrigger);

        var searchTrigger = new DataTrigger();
        searchTrigger.Binding = new Binding($"Cells[{columnIndex}].IsSearchMatch");
        searchTrigger.Value = true;
        searchTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty, new DynamicResourceExtension("SearchHighlightBackgroundBrush")));
        searchTrigger.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, new DynamicResourceExtension("SearchHighlightBorderBrush")));
        searchTrigger.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        style.Triggers.Add(searchTrigger);

        var currentSearchTrigger = new DataTrigger();
        currentSearchTrigger.Binding = new Binding($"Cells[{columnIndex}].IsCurrentSearchMatch");
        currentSearchTrigger.Value = true;
        currentSearchTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty, new DynamicResourceExtension("CurrentSearchHighlightBackgroundBrush")));
        currentSearchTrigger.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, new DynamicResourceExtension("CurrentSearchHighlightBorderBrush")));
        currentSearchTrigger.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(2)));
        style.Triggers.Add(currentSearchTrigger);

        return style;
    }

    private string GetExcelColumnName(int columnIndex)
    {
        string columnName = "";
        int dividend = columnIndex + 1;
        while (dividend > 0)
        {
            int modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar(65 + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }
        return columnName;
    }

    private void UpdateAllRowHeaders(DataGrid grid)
    {
        if (grid == null) return;
        for (int i = 0; i < grid.Items.Count; i++)
        {
            var row = grid.ItemContainerGenerator.ContainerFromIndex(i) as DataGridRow;
            if (row != null)
                row.Header = (i + 1).ToString();
        }
    }

    public void UpdateDataGridSelection(DataGrid grid, TabItemViewModel tab, bool suppressFocus = false)
    {
        if (grid == null || tab == null) return;
        if (tab.VimState.CurrentMode == VimMode.Visual) return;

        var pos = tab.VimState.CursorPosition;
        if (grid.Items.Count == 0 || grid.Columns.Count == 0) return;
        if (pos.Row < 0 || pos.Row >= grid.Items.Count) return;
        if (pos.Column < 0 || pos.Column >= grid.Columns.Count) return;

        try
        {
            _isUpdatingSelection = true;
            grid.SelectedCells.Clear();
            var cellInfo = new DataGridCellInfo(grid.Items[pos.Row], grid.Columns[pos.Column]);
            grid.SelectedCells.Add(cellInfo);
            grid.CurrentCell = cellInfo;
            grid.ScrollIntoView(grid.Items[pos.Row], grid.Columns[pos.Column]);

            bool isFindReplaceOpen = tab.FindReplaceViewModel?.IsVisible ?? false;
            bool isInsertMode = tab.VimState.CurrentMode == VimMode.Insert;
            bool isPendingBulkEdit = tab.VimState.PendingBulkEditRange != null;
            bool isRestoring = suppressFocus || _context.IsRestoringSession;

            if (!isFindReplaceOpen && !isInsertMode && !isPendingBulkEdit && !isRestoring)
            {
                grid.UpdateLayout();
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        _isUpdatingSelection = true;
                        if (tab.VimState.CurrentMode == VimMode.Insert) return;
                        var row = grid.ItemContainerGenerator.ContainerFromIndex(pos.Row) as DataGridRow;
                        if (row != null)
                        {
                            var cell = GetCell(grid, row, pos.Column);
                            cell?.Focus();
                        }
                    }
                    catch { }
                    finally { _isUpdatingSelection = false; }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }
        finally { _isUpdatingSelection = false; }
    }

    private DataGridCell? GetCell(DataGrid grid, DataGridRow row, int columnIndex)
    {
        if (row == null || columnIndex < 0 || columnIndex >= grid.Columns.Count) return null;
        var presenter = FindVisualChild<DataGridCellsPresenter>(row);
        if (presenter == null) return null;
        var cell = presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell;
        if (cell == null)
        {
            grid.ScrollIntoView(row.Item, grid.Columns[columnIndex]);
            cell = presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell;
        }
        return cell;
    }

    private T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild) return typedChild;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    private void TsvGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            var scrollViewer = FindVisualChild<ScrollViewer>(grid);
            if (scrollViewer != null)
            {
                double scrollAmount = e.Delta > 0 ? -50 : 50;
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + scrollAmount);
                e.Handled = true;
            }
        }
    }

    private void TsvGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (_context.IsVimModeEnabled) return;
        if (Keyboard.FocusedElement is TextBox) return;

        if (sender is DataGrid grid && grid.DataContext is TabItemViewModel)
        {
            if (!grid.IsReadOnly && grid.CurrentCell.Column != null)
            {
                _pendingTextInput = e.Text;
                grid.BeginEdit();
                e.Handled = true;
            }
        }
    }

    private void TsvGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is DataGrid grid && grid.DataContext is TabItemViewModel tab)
        {
            var cell = FindVisualParent<DataGridCell>(e.OriginalSource as DependencyObject);
            if (cell?.Column != null)
            {
                var row = FindVisualParent<DataGridRow>(cell);
                if (row != null)
                {
                    var rowIndex = row.GetIndex();
                    var colIndex = grid.Columns.IndexOf(cell.Column);
                    if (rowIndex >= 0 && colIndex >= 0)
                    {
                        tab.VimState.CursorPosition = new GridPosition(rowIndex, colIndex);
                        if (_context.IsVimModeEnabled && tab.VimState.CurrentMode != VimMode.Insert)
                        {
                            tab.VimState.SwitchMode(VimMode.Insert);
                            tab.VimState.CellEditCaretPosition = CellEditCaretPosition.End;
                        }
                        grid.CurrentCell = new DataGridCellInfo(cell);
                        grid.BeginEdit();
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        _isDataGridDragging = true;
        if (sender is DataGrid grid2 && grid2.DataContext is TabItemViewModel tab2)
        {
            var cell = FindVisualParent<DataGridCell>(e.OriginalSource as DependencyObject);
            if (cell?.Column != null)
            {
                var row = FindVisualParent<DataGridRow>(cell);
                if (row != null)
                {
                    var rowIndex = row.GetIndex();
                    var colIndex = grid2.Columns.IndexOf(cell.Column);
                    if (rowIndex >= 0 && colIndex >= 0)
                    {
                        _dragStartPosition = new GridPosition(rowIndex, colIndex);
                        if (tab2.VimState.CurrentMode == VimMode.Visual)
                            tab2.VimState.SwitchMode(VimMode.Normal);
                    }
                }
            }
        }
    }

    private void TsvGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDataGridDragging = false;
        if (sender is DataGrid grid && grid.DataContext is TabItemViewModel tab)
        {
            var cell = FindVisualParent<DataGridCell>(e.OriginalSource as DependencyObject);
            if (cell?.Column != null)
            {
                var row = FindVisualParent<DataGridRow>(cell);
                if (row != null)
                {
                    var endRowIndex = row.GetIndex();
                    var endColIndex = grid.Columns.IndexOf(cell.Column);
                    if (endRowIndex >= 0 && endColIndex >= 0)
                    {
                        var endPosition = new GridPosition(endRowIndex, endColIndex);
                        if (_dragStartPosition != null &&
                            (_dragStartPosition.Row != endPosition.Row || _dragStartPosition.Column != endPosition.Column))
                        {
                            _isUpdatingSelection = true;
                            try
                            {
                                tab.VimState.CurrentSelection = new SelectionRange(VisualType.Character, _dragStartPosition, endPosition);
                                tab.VimState.CursorPosition = endPosition;
                                grid.SelectedCells.Clear();
                            }
                            finally { _isUpdatingSelection = false; }
                            tab.VimState.SwitchMode(VimMode.Visual);
                        }
                        else
                        {
                            _isUpdatingSelection = true;
                            try
                            {
                                tab.VimState.CurrentSelection = new SelectionRange(VisualType.Character, endPosition, endPosition);
                                tab.VimState.CursorPosition = endPosition;
                            }
                            finally { _isUpdatingSelection = false; }
                        }
                        _dragStartPosition = null;
                    }
                }
            }
            else { _dragStartPosition = null; }
        }
        else { _dragStartPosition = null; }
    }

    private void TsvGrid_CurrentCellChangedHandler(object? sender, EventArgs e)
    {
        if (sender is DataGrid grid)
            TsvGrid_CurrentCellChanged(grid, grid.DataContext as TabItemViewModel);
    }

    private void TsvGrid_CurrentCellChanged(DataGrid grid, TabItemViewModel? tab)
    {
        if (grid == null || tab == null || _context?.SelectedTab != tab) return;
        if (_isUpdatingSelection || _isDataGridDragging) return;

        if (grid.CurrentCell.Item != null && grid.CurrentCell.Column != null)
        {
            var rowIndex = grid.Items.IndexOf(grid.CurrentCell.Item);
            var colIndex = grid.Columns.IndexOf(grid.CurrentCell.Column);
            if (rowIndex >= 0 && colIndex >= 0)
            {
                var newPosition = new GridPosition(rowIndex, colIndex);
                if (tab.VimState.CursorPosition.Row != rowIndex || tab.VimState.CursorPosition.Column != colIndex)
                {
                    _isUpdatingSelection = true;
                    try { tab.VimState.CursorPosition = newPosition; }
                    finally { _isUpdatingSelection = false; }
                }
            }
        }
    }

    public void TsvGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (_context?.SelectedTab == null) return;
        var tab = _context.SelectedTab;

        if (!_context.IsVimModeEnabled) return;

        var currentMode = tab.VimState.CurrentMode;
        if (currentMode == VimMode.Insert)
        {
            int rowIndex = e.Row != null ? tab.GridViewModel.Document.Rows.IndexOf((Row)e.Row.Item) : -1;
            int columnIndex = e.Column?.DisplayIndex ?? -1;
            if (rowIndex >= 0 && columnIndex >= 0)
            {
                var cell = tab.GridViewModel.Document.GetCell(rowIndex, columnIndex);
                tab.VimState.InsertModeOriginalValue = cell?.Value ?? string.Empty;
                if (tab.VimState.InsertModeStartPosition == null)
                    tab.VimState.InsertModeStartPosition = new GridPosition(rowIndex, columnIndex);
            }
            return;
        }
        e.Cancel = true;
    }

    private void TsvGrid_PreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (e.EditingElement is TextBox textBox && _context?.SelectedTab != null)
        {
            var tab = _context.SelectedTab;

            UnsubscribeFromCurrentEditingTextBox();
            _currentEditingTextBox = textBox;
            _currentTextChangedHandler = (s, evt) =>
            {
                if (_context?.SelectedTab != null && textBox != null)
                    _context.SelectedTab.SelectedCellContent = textBox.Text;
            };
            textBox.TextChanged += _currentTextChangedHandler;

            textBox.Loaded += (s, evt) =>
            {
                if (!_context.IsVimModeEnabled && _pendingTextInput != null)
                {
                    textBox.Text = _pendingTextInput;
                    textBox.CaretIndex = textBox.Text.Length;
                    _pendingTextInput = null;
                }
                else if (tab.VimState.CellEditCaretPosition == CellEditCaretPosition.Start)
                    textBox.CaretIndex = 0;
                else
                    textBox.CaretIndex = textBox.Text.Length;
                textBox.Focus();
            };

            textBox.PreviewKeyDown += (s, evt) =>
            {
                Key actualKey = evt.Key == Key.ImeProcessed ? evt.ImeProcessedKey : evt.Key;

                if (actualKey == Key.OemPlus && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
                {
                    string currentDate = DateTime.Now.ToString("yyyy/MM/dd");
                    int caretIndex = textBox.CaretIndex;
                    textBox.Text = textBox.Text.Insert(caretIndex, currentDate);
                    textBox.CaretIndex = caretIndex + currentDate.Length;
                    evt.Handled = true;
                    return;
                }

                if (!_context.IsVimModeEnabled)
                {
                    if (evt.Key != Key.ImeProcessed &&
                        (actualKey == Key.Up || actualKey == Key.Down || actualKey == Key.Left || actualKey == Key.Right))
                    {
                        if (sender is DataGrid grid)
                        {
                            grid.CommitEdit(DataGridEditingUnit.Cell, true);
                            var pos = tab.VimState.CursorPosition;
                            int newRow = pos.Row, newCol = pos.Column;
                            int maxRow = tab.GridViewModel.Document.RowCount - 1;
                            int maxCol = tab.GridViewModel.Document.ColumnCount - 1;
                            switch (actualKey)
                            {
                                case Key.Up: newRow = Math.Max(0, pos.Row - 1); break;
                                case Key.Down: newRow = Math.Min(maxRow, pos.Row + 1); break;
                                case Key.Left: newCol = Math.Max(0, pos.Column - 1); break;
                                case Key.Right: newCol = Math.Min(maxCol, pos.Column + 1); break;
                            }
                            tab.VimState.CursorPosition = new GridPosition(newRow, newCol);
                            evt.Handled = true;
                        }
                    }
                    return;
                }

                if (actualKey == Key.Enter && evt.Key != Key.ImeProcessed)
                {
                    tab.VimState.SwitchMode(VimMode.Normal);
                    evt.Handled = true;
                }
                else if (actualKey == Key.Escape)
                {
                    tab.VimState.SwitchMode(VimMode.Normal);
                    evt.Handled = true;
                }
                else if (actualKey == Key.J && Keyboard.Modifiers == ModifierKeys.None)
                {
                    if (tab.VimState.PendingKeys.Keys.Count == 1 &&
                        tab.VimState.PendingKeys.Keys[0] == Key.J &&
                        !tab.VimState.PendingKeys.IsExpired(TimeSpan.FromMilliseconds(500)))
                    {
                        tab.VimState.PendingKeys.Clear();
                        if (textBox.CaretIndex > 0 && textBox.Text.Length > 0)
                        {
                            int caretIndex = textBox.CaretIndex;
                            textBox.Text = textBox.Text.Remove(caretIndex - 1, 1);
                            textBox.CaretIndex = caretIndex - 1;
                        }
                        tab.VimState.SwitchMode(VimMode.Normal);
                        evt.Handled = true;
                    }
                    else
                    {
                        tab.VimState.PendingKeys.Clear();
                        tab.VimState.PendingKeys.Add(Key.J);
                    }
                }
                else
                {
                    if (tab.VimState.PendingKeys.Keys.Count > 0)
                        tab.VimState.PendingKeys.Clear();
                }
            };
        }
    }

    public void HandleModeChange(DataGrid grid, TabItemViewModel tab)
    {
        if (grid == null || tab == null) return;
        try
        {
            if (tab.VimState.CurrentMode == VimMode.Visual)
            {
                _isUpdatingSelection = true;
                grid.SelectedCells.Clear();
                _isUpdatingSelection = false;
                InitializeVisualSelection(tab);
            }
            else if (tab.VimState.CurrentSelection == null)
            {
                ClearAllCellSelections(tab.Document);
                tab.VimState.ClearRowSelections();
                tab.VimState.ClearColumnSelections();
                UpdateDataGridSelection(grid, tab);
            }

            if (tab.VimState.CurrentMode == VimMode.Command)
            {
                grid.CommitEdit(DataGridEditingUnit.Cell, true);
                grid.CommitEdit(DataGridEditingUnit.Row, true);
            }
            else if (tab.VimState.CurrentMode == VimMode.Insert)
            {
                grid.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (grid.CurrentCell.Item != null && grid.CurrentCell.Column != null)
                        grid.BeginEdit();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
            else
            {
                grid.Dispatcher.BeginInvoke(new Action(() =>
                {
                    grid.CommitEdit(DataGridEditingUnit.Cell, true);
                    grid.CommitEdit(DataGridEditingUnit.Row, true);
                    if (tab.VimState.PendingBulkEditRange != null)
                        ApplyBulkEdit(tab);
                    grid.Focus();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }
        catch { }
    }

    private HashSet<(int row, int col)>? _previouslySelectedCells;

    private void ClearPreviousSelection(TsvDocument document)
    {
        if (document == null || _previouslySelectedCells == null || _previouslySelectedCells.Count == 0) return;
        foreach (var (row, col) in _previouslySelectedCells)
        {
            if (row < document.RowCount && col < document.Rows[row].Cells.Count)
            {
                var cell = document.Rows[row].Cells[col];
                if (cell.IsSelected) cell.IsSelected = false;
            }
        }
        _previouslySelectedCells.Clear();
    }

    private void ClearAllCellSelections(TsvDocument document)
    {
        if (document == null) return;
        if (_previouslySelectedCells != null && _previouslySelectedCells.Count > 0)
        {
            ClearPreviousSelection(document);
            return;
        }
        foreach (var row in document.Rows)
            foreach (var cell in row.Cells)
                if (cell.IsSelected) cell.IsSelected = false;
    }

    private void InitializeVisualSelection(TabItemViewModel tab)
    {
        if (tab == null || tab.VimState.CurrentSelection == null) return;
        var selection = tab.VimState.CurrentSelection;
        var document = tab.Document;
        ClearPreviousSelection(document);
        _previouslySelectedCells ??= new HashSet<(int, int)>();

        switch (selection.Type)
        {
            case VisualType.Character:
                int startRow = Math.Min(selection.Start.Row, selection.End.Row);
                int endRow = Math.Max(selection.Start.Row, selection.End.Row);
                int startCol = Math.Min(selection.Start.Column, selection.End.Column);
                int endCol = Math.Max(selection.Start.Column, selection.End.Column);
                for (int row = startRow; row <= endRow && row < document.RowCount; row++)
                {
                    var rowObj = document.Rows[row];
                    for (int col = startCol; col <= endCol && col < rowObj.Cells.Count; col++)
                    {
                        if (!rowObj.Cells[col].IsSelected) rowObj.Cells[col].IsSelected = true;
                        _previouslySelectedCells.Add((row, col));
                    }
                }
                break;

            case VisualType.Line:
                int lineStartRow = Math.Min(selection.Start.Row, selection.End.Row);
                int lineEndRow = Math.Max(selection.Start.Row, selection.End.Row);
                for (int row = lineStartRow; row <= lineEndRow && row < document.RowCount; row++)
                {
                    var rowObj = document.Rows[row];
                    for (int col = 0; col < rowObj.Cells.Count; col++)
                    {
                        if (!rowObj.Cells[col].IsSelected) rowObj.Cells[col].IsSelected = true;
                        _previouslySelectedCells.Add((row, col));
                    }
                }
                break;

            case VisualType.Block:
                int blockStartCol = Math.Min(selection.Start.Column, selection.End.Column);
                int blockEndCol = Math.Max(selection.Start.Column, selection.End.Column);
                for (int rowIdx = 0; rowIdx < document.RowCount; rowIdx++)
                {
                    var rowObj = document.Rows[rowIdx];
                    for (int col = blockStartCol; col <= blockEndCol && col < rowObj.Cells.Count; col++)
                    {
                        if (!rowObj.Cells[col].IsSelected) rowObj.Cells[col].IsSelected = true;
                        _previouslySelectedCells.Add((rowIdx, col));
                    }
                }
                break;
        }
    }

    private T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
    {
        while (child != null)
        {
            child = VisualTreeHelper.GetParent(child);
            if (child is T parent) return parent;
        }
        return null;
    }

    public void TsvGrid_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        var dataGrid = sender as DataGrid;
        if (dataGrid == null) return;

        var oldTab = e.OldValue as TabItemViewModel;
        if (oldTab != null)
        {
            bool hasTrackedCellSelection = _previouslySelectedCells is { Count: > 0 };
            bool hasExplicitSelectionState = oldTab.VimState.CurrentMode == VimMode.Visual ||
                                             oldTab.VimState.SelectedRows.Count > 0 ||
                                             oldTab.VimState.SelectedColumns.Count > 0;
            if (hasTrackedCellSelection || hasExplicitSelectionState)
                ClearAllCellSelections(oldTab.Document);
        }
        _previouslySelectedCells?.Clear();

        var newTab = e.NewValue as TabItemViewModel;
        if (newTab != null)
        {
            bool isNewGrid = dataGrid.Tag as string != "Initialized";
            InitializeDataGrid(dataGrid, newTab);
            SetupVimStateHandlers(dataGrid, newTab);

            if (!isNewGrid)
            {
                GenerateColumns(dataGrid, newTab);
                if (newTab.ColumnWidths.Count == 0)
                    AutoFitAllColumns(dataGrid, newTab);
            }

            bool suppressFocusOnContextChange = _context.IsRestoringSession;
            dataGrid.Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateDataGridSelection(dataGrid, newTab, suppressFocusOnContextChange);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    public void ScrollToCenterForTab(TabItemViewModel tab)
    {
        if (!_tabToDataGrid.TryGetValue(tab, out var grid)) return;
        ScrollToCenter(grid, tab);
    }

    public void ScrollToCenter(DataGrid grid, TabItemViewModel tab)
    {
        if (grid == null || tab == null) return;
        var pos = tab.VimState.CursorPosition;
        if (grid.Items.Count == 0 || pos.Row < 0 || pos.Row >= grid.Items.Count) return;

        grid.ScrollIntoView(grid.Items[pos.Row]);
        grid.UpdateLayout();

        var scrollViewer = FindVisualChild<ScrollViewer>(grid);
        if (scrollViewer == null) return;

        var row = grid.ItemContainerGenerator.ContainerFromIndex(pos.Row) as DataGridRow;
        if (row == null) return;

        var transform = row.TransformToAncestor(scrollViewer);
        var rowPosition = transform.Transform(new Point(0, 0));
        double desiredOffset = scrollViewer.VerticalOffset + rowPosition.Y - (scrollViewer.ViewportHeight - row.ActualHeight) / 2;
        desiredOffset = Math.Max(0, Math.Min(desiredOffset, scrollViewer.ScrollableHeight));
        scrollViewer.ScrollToVerticalOffset(desiredOffset);
    }

    private void ApplyBulkEdit(TabItemViewModel tab)
    {
        if (tab.VimState.PendingBulkEditRange == null) return;
        var range = tab.VimState.PendingBulkEditRange;
        var document = tab.Document;
        var caretPosition = tab.VimState.CellEditCaretPosition;
        var originalValue = tab.VimState.OriginalCellValueForBulkEdit;

        var editedCell = document.GetCell(new GridPosition(range.StartRow, range.StartColumn));
        if (editedCell == null)
        {
            tab.VimState.PendingBulkEditRange = null;
            tab.VimState.OriginalCellValueForBulkEdit = string.Empty;
            return;
        }

        var newValue = editedCell.Value;
        string insertedText = string.Empty;
        if (caretPosition == CellEditCaretPosition.Start)
        {
            if (newValue.EndsWith(originalValue))
                insertedText = newValue.Substring(0, newValue.Length - originalValue.Length);
        }
        else
        {
            if (newValue.StartsWith(originalValue))
                insertedText = newValue.Substring(originalValue.Length);
        }

        var cellUpdates = new Dictionary<GridPosition, string>();
        for (int r = 0; r < range.RowCount; r++)
        {
            for (int c = 0; c < range.ColumnCount; c++)
            {
                int docRow = range.StartRow + r;
                int docCol = range.StartColumn + c;
                if (docRow == range.StartRow && docCol == range.StartColumn) continue;
                if (docRow < document.RowCount && docCol < document.Rows[docRow].Cells.Count)
                {
                    var cell = document.Rows[docRow].Cells[docCol];
                    cellUpdates[new GridPosition(docRow, docCol)] = caretPosition == CellEditCaretPosition.Start
                        ? insertedText + cell.Value
                        : cell.Value + insertedText;
                }
            }
        }

        if (cellUpdates.Count > 0)
        {
            var command = new BulkEditCellsWithValuesCommand(document, cellUpdates);
            tab.VimState.CommandHistory?.Execute(command) ;
        }

        tab.VimState.PendingBulkEditRange = null;
        tab.VimState.OriginalCellValueForBulkEdit = string.Empty;
    }

    private string ExtractInsertedText(string originalValue, string newValue, CellEditCaretPosition caretPosition)
    {
        if (string.IsNullOrEmpty(originalValue)) return newValue ?? string.Empty;
        if (string.IsNullOrEmpty(newValue)) return string.Empty;
        if (caretPosition == CellEditCaretPosition.Start)
        {
            if (newValue.EndsWith(originalValue))
                return newValue.Substring(0, newValue.Length - originalValue.Length);
        }
        else
        {
            if (newValue.StartsWith(originalValue))
                return newValue.Substring(originalValue.Length);
        }
        return newValue;
    }

    public void RecalculateAllColumnWidths()
    {
        foreach (var kvp in _tabToDataGrid)
        {
            var tab = kvp.Key;
            var grid = kvp.Value;
            if (grid != null && tab != null)
            {
                tab.ResetManualResizeTracking();
                tab.ColumnWidths.Clear();
                AutoFitAllColumns(grid, tab);
            }
        }
    }

    public void FocusCurrentTab(TabItemViewModel tab)
    {
        if (tab == null || !_tabToDataGrid.TryGetValue(tab, out var grid)) return;
        var pos = tab.VimState.CursorPosition;
        if (grid.Items.Count == 0 || pos.Row < 0 || pos.Row >= grid.Items.Count ||
            pos.Column < 0 || pos.Column >= grid.Columns.Count) return;
        try
        {
            grid.UpdateLayout();
            var row = grid.ItemContainerGenerator.ContainerFromIndex(pos.Row) as DataGridRow;
            if (row != null) GetCell(grid, row, pos.Column)?.Focus();
        }
        catch { }
    }

    public void CleanupTab(TabItemViewModel tab)
    {
        if (tab == null) return;
        _tabHandlers.Remove(tab);
        _tabToDataGrid.Remove(tab);
        var dataGridToRemove = _dataGridHandlers.FirstOrDefault(kvp => kvp.Value.tab == tab).Key;
        if (dataGridToRemove != null)
        {
            _dataGridHandlers.Remove(dataGridToRemove);
            _dataGridColumnCount.Remove(dataGridToRemove);
        }
    }
}
