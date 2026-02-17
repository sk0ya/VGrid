using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using VGrid.Commands;
using VGrid.Models;
using VGrid.ViewModels;
using VGrid.VimEngine;

namespace VGrid.UI;

/// <summary>
/// Manages DataGrid operations including column generation, selection, editing, and visual mode
/// </summary>
public class DataGridManager
{
    private readonly MainViewModel _viewModel;
    private bool _isUpdatingSelection = false;
    private bool _isDataGridDragging = false;
    private GridPosition? _dragStartPosition = null;

    // Store event handlers for each DataGrid to allow proper cleanup
    private readonly Dictionary<DataGrid, (TabItemViewModel tab, PropertyChangedEventHandler vimStateHandler, PropertyChangedEventHandler documentHandler, EventHandler<IEnumerable<int>> columnWidthHandler)> _dataGridHandlers
        = new Dictionary<DataGrid, (TabItemViewModel, PropertyChangedEventHandler, PropertyChangedEventHandler, EventHandler<IEnumerable<int>>)>();

    // Reverse lookup: tab to DataGrid
    private readonly Dictionary<TabItemViewModel, DataGrid> _tabToDataGrid = new Dictionary<TabItemViewModel, DataGrid>();

    // Store handlers per tab for reuse across tab switches (Phase 2 optimization)
    private readonly Dictionary<TabItemViewModel, (PropertyChangedEventHandler vimStateHandler, PropertyChangedEventHandler documentHandler, EventHandler<IEnumerable<int>> columnWidthHandler)> _tabHandlers
        = new Dictionary<TabItemViewModel, (PropertyChangedEventHandler, PropertyChangedEventHandler, EventHandler<IEnumerable<int>>)>();

    // Phase 1 optimization: Track column count per DataGrid to avoid unnecessary regeneration
    private readonly Dictionary<DataGrid, int> _dataGridColumnCount = new Dictionary<DataGrid, int>();

    // Cache styles to avoid recreating them for each column
    private Style? _cachedEditingStyle;
    private readonly Dictionary<int, Style> _cachedCellStyles = new Dictionary<int, Style>();

    // Store pending text input for non-Vim mode editing (Excel-like behavior)
    private string? _pendingTextInput = null;

    // Track current editing TextBox for real-time cell content preview
    private TextBox? _currentEditingTextBox = null;
    private TextChangedEventHandler? _currentTextChangedHandler = null;

    private void UnsubscribeFromCurrentEditingTextBox()
    {
        if (_currentEditingTextBox != null && _currentTextChangedHandler != null)
        {
            _currentEditingTextBox.TextChanged -= _currentTextChangedHandler;
        }
        _currentEditingTextBox = null;
        _currentTextChangedHandler = null;
    }

    public DataGridManager(MainViewModel viewModel)
    {
        _viewModel = viewModel;
    }

    public void TsvGrid_Loaded(object sender, RoutedEventArgs e)
    {
        var grid = sender as DataGrid;
        if (grid == null)
            return;

        var tabItem = grid.DataContext as TabItemViewModel;
        if (tabItem == null)
        {
            // DataContext is not yet set (can happen on first tab).
            // Subscribe to DataContextChanged to initialize when DataContext is set.
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
        // Check if already registered for this tab
        if (_dataGridHandlers.TryGetValue(grid, out var existingInfo) && existingInfo.tab == tab)
            return;

        // Unsubscribe old handlers if any
        if (existingInfo.tab != null)
        {
            existingInfo.tab.VimState.PropertyChanged -= existingInfo.vimStateHandler;
            existingInfo.tab.Document.PropertyChanged -= existingInfo.documentHandler;
            existingInfo.tab.VimState.ColumnWidthUpdateRequested -= existingInfo.columnWidthHandler;
            _dataGridHandlers.Remove(grid);
            _tabToDataGrid.Remove(existingInfo.tab);
        }

        // Reuse handlers if they already exist for this tab
        if (!_tabHandlers.TryGetValue(tab, out var handlers))
        {
            // Create handlers only once per tab
            PropertyChangedEventHandler vimStateHandler = (s, evt) =>
            {
                if (evt.PropertyName == nameof(tab.VimState.CursorPosition) && tab == _viewModel?.SelectedTab)
                {
                    if (_tabToDataGrid.TryGetValue(tab, out var g))
                        UpdateDataGridSelection(g, tab);
                }
                else if (evt.PropertyName == nameof(tab.VimState.CurrentMode) && tab == _viewModel?.SelectedTab)
                {
                    if (_tabToDataGrid.TryGetValue(tab, out var g))
                        HandleModeChange(g, tab);
                }
                else if (evt.PropertyName == nameof(tab.VimState.CurrentSelection) && tab == _viewModel?.SelectedTab && tab.VimState.CurrentMode == VimMode.Visual)
                {
                    InitializeVisualSelection(tab);
                }
            };

            PropertyChangedEventHandler documentHandler = (s, evt) =>
            {
                if (evt.PropertyName == nameof(TsvDocument.ColumnCount) && tab == _viewModel?.SelectedTab)
                {
                    if (_tabToDataGrid.TryGetValue(tab, out var g))
                        GenerateColumns(g, tab);
                }
            };

            EventHandler<IEnumerable<int>> columnWidthHandler = (s, columnIndices) =>
            {
                if (tab == _viewModel?.SelectedTab)
                {
                    if (_tabToDataGrid.TryGetValue(tab, out var g))
                        AutoFitColumns(g, tab, columnIndices);
                }
            };

            handlers = (vimStateHandler, documentHandler, columnWidthHandler);
            _tabHandlers[tab] = handlers;
        }

        // Subscribe to events
        tab.VimState.PropertyChanged += handlers.vimStateHandler;
        tab.Document.PropertyChanged += handlers.documentHandler;
        tab.VimState.ColumnWidthUpdateRequested += handlers.columnWidthHandler;

        _dataGridHandlers[grid] = (tab, handlers.vimStateHandler, handlers.documentHandler, handlers.columnWidthHandler);
        _tabToDataGrid[tab] = grid;

        // Update selection (capture IsRestoringSession now; lambda runs after restore may complete)
        bool suppressFocusOnLoad = _viewModel.IsRestoringSession;
        grid.Dispatcher.BeginInvoke(new Action(() =>
        {
            UpdateDataGridSelection(grid, tab, suppressFocusOnLoad);
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void InitializeDataGrid(DataGrid grid, TabItemViewModel tabItem)
    {
        if (grid.Tag as string == "Initialized")
            return;

        grid.Tag = "Initialized";

        try
        {
            GenerateColumns(grid, tabItem);

            // Only auto-fit if column widths haven't been calculated yet
            // (TsvGrid_OnDataContextChanged may have already done this)
            if (tabItem.ColumnWidths.Count == 0)
            {
                AutoFitAllColumns(grid, tabItem);
            }

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
                {
                    UpdateAllRowHeaders(grid);
                }
            };

            bool suppressFocusOnInit = _viewModel.IsRestoringSession;
            grid.Dispatcher.BeginInvoke(new Action(() => { UpdateDataGridSelection(grid, tabItem, suppressFocusOnInit); }),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading grid: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Phase 1 optimization: Update column widths only (bindings auto-update with DataContext)
    /// </summary>
    private void UpdateColumnWidths(DataGrid grid, TabItemViewModel tab)
    {
        if (grid == null || tab == null)
            return;

        // Only update column widths - bindings automatically work with new DataContext
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
        if (grid == null || tab == null)
            return;

        // Use actual column count with reasonable minimum for new documents
        // Large minimum values slow down initial display significantly
        var requiredColumnCount = Math.Max(20, tab.GridViewModel.ColumnCount);

        // Phase 1 optimization: Only regenerate if column count changed
        if (_dataGridColumnCount.TryGetValue(grid, out var existingCount) && existingCount == requiredColumnCount)
        {
            // Column count matches - just update widths (bindings auto-work with DataContext)
            UpdateColumnWidths(grid, tab);
            return;
        }

        // Column count changed - need to regenerate
        grid.Columns.Clear();

        // Cache editing style (shared across all columns)
        _cachedEditingStyle ??= CreateEditingStyle();

        for (int i = 0; i < requiredColumnCount; i++)
        {
            // Use cached cell style or create and cache new one
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

        // Track the column count for this DataGrid
        _dataGridColumnCount[grid] = requiredColumnCount;
    }

    private void AutoFitAllColumns(DataGrid grid, TabItemViewModel tab)
    {
        if (_viewModel == null || grid == null || tab == null)
            return;

        var typeface = new Typeface(
            grid.FontFamily ?? new FontFamily("Segoe UI"),
            grid.FontStyle,
            grid.FontWeight,
            grid.FontStretch
        );
        double fontSize = grid.FontSize > 0 ? grid.FontSize : 11;

        var widths = _viewModel.ColumnWidthService.CalculateAllColumnWidths(tab.Document, typeface, fontSize);

        tab.ColumnWidths = widths;
        tab.ResetManualResizeTracking();

        for (int i = 0; i < grid.Columns.Count && i < widths.Count; i++)
        {
            if (grid.Columns[i] is DataGridTextColumn column)
            {
                column.Width = new DataGridLength(widths[i], DataGridLengthUnitType.Pixel);
            }
        }
    }

    private void AutoFitColumn(DataGrid grid, TabItemViewModel tab, int columnIndex)
    {
        if (_viewModel == null || grid == null || tab == null)
            return;

        if (tab.ManuallyResizedColumns.Contains(columnIndex))
            return;

        var typeface = new Typeface(
            grid.FontFamily ?? new FontFamily("Segoe UI"),
            grid.FontStyle,
            grid.FontWeight,
            grid.FontStretch
        );
        double fontSize = grid.FontSize > 0 ? grid.FontSize : 11;

        double width = _viewModel.ColumnWidthService.CalculateColumnWidth(tab.Document, columnIndex, typeface, fontSize);

        tab.ColumnWidths[columnIndex] = width;

        if (columnIndex < grid.Columns.Count && grid.Columns[columnIndex] is DataGridTextColumn column)
        {
            column.Width = new DataGridLength(width, DataGridLengthUnitType.Pixel);
        }
    }

    /// <summary>
    /// Auto-fits the specified columns for the given DataGrid and tab
    /// Called after paste operations to adjust column widths
    /// </summary>
    public void AutoFitColumns(DataGrid grid, TabItemViewModel tab, IEnumerable<int> columnIndices)
    {
        if (grid == null || tab == null || columnIndices == null)
            return;

        foreach (var columnIndex in columnIndices)
        {
            AutoFitColumn(grid, tab, columnIndex);
        }
    }

    public void TsvGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        // Unsubscribe from TextChanged event
        UnsubscribeFromCurrentEditingTextBox();

        if (_viewModel?.SelectedTab == null || e.Cancel)
            return;

        var grid = sender as DataGrid;
        if (grid == null)
            return;

        var tab = _viewModel.SelectedTab;
        int columnIndex = e.Column.DisplayIndex;
        int rowIndex = e.Row != null ? tab.GridViewModel.Document.Rows.IndexOf((Row)e.Row.Item) : -1;

        // Record insert mode change for dot command and undo support (if applicable)
        if (tab.VimState.InsertModeStartPosition != null &&
            e.EditingElement is TextBox textBox)
        {
            string newValue = textBox.Text;
            string originalValue = tab.VimState.InsertModeOriginalValue;

            // Only record if something actually changed
            if (newValue != originalValue && rowIndex >= 0 && columnIndex >= 0)
            {
                // Record for dot command - extract only the inserted text portion
                if (tab.VimState.PendingInsertType != ChangeType.None)
                {
                    // Calculate the actually inserted text based on caret position
                    string insertedText = ExtractInsertedText(originalValue, newValue, tab.VimState.CellEditCaretPosition);

                    tab.VimState.LastChange = new LastChange
                    {
                        Type = tab.VimState.PendingInsertType,
                        Count = 1,  // Insert operations don't use count prefix
                        InsertedText = insertedText,
                        CaretPosition = tab.VimState.CellEditCaretPosition
                    };
                }

                // Add to command history for undo support
                // The value has already been applied by data binding, so we use AddExecutedCommand
                var position = new GridPosition(rowIndex, columnIndex);
                var command = new EditCellCommand(tab.GridViewModel.Document, position, newValue, originalValue);
                tab.VimState.CommandHistory?.AddExecutedCommand(command);
            }

            // Clear the tracking state
            tab.VimState.PendingInsertType = ChangeType.None;
            tab.VimState.InsertModeStartPosition = null;
            tab.VimState.InsertModeOriginalValue = string.Empty;
        }

        grid.Dispatcher.BeginInvoke(new Action(() =>
        {
            AutoFitColumn(grid, tab, columnIndex);
            // Refresh selected cell content preview
            tab.RefreshSelectedCellContent();
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// Creates editing style for TextBox (shared across all columns)
    /// </summary>
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

        // Use DynamicResourceExtension for theme-aware colors
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

        // Search highlight for all matches
        var searchTrigger = new DataTrigger();
        searchTrigger.Binding = new Binding($"Cells[{columnIndex}].IsSearchMatch");
        searchTrigger.Value = true;
        searchTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty, new DynamicResourceExtension("SearchHighlightBackgroundBrush")));
        searchTrigger.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, new DynamicResourceExtension("SearchHighlightBorderBrush")));
        searchTrigger.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        style.Triggers.Add(searchTrigger);

        // Current search match highlight (highest priority)
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
        if (grid == null)
            return;

        for (int i = 0; i < grid.Items.Count; i++)
        {
            var row = grid.ItemContainerGenerator.ContainerFromIndex(i) as DataGridRow;
            if (row != null)
            {
                row.Header = (i + 1).ToString();
            }
        }
    }

    public void UpdateDataGridSelection(DataGrid grid, TabItemViewModel tab, bool suppressFocus = false)
    {
        if (grid == null || tab == null)
            return;

        if (tab.VimState.CurrentMode == VimMode.Visual)
            return;

        var pos = tab.VimState.CursorPosition;

        if (grid.Items.Count == 0 || grid.Columns.Count == 0)
            return;

        if (pos.Row < 0 || pos.Row >= grid.Items.Count)
            return;
        if (pos.Column < 0 || pos.Column >= grid.Columns.Count)
            return;

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
            bool isRestoring = suppressFocus || _viewModel.IsRestoringSession;

            if (!isFindReplaceOpen && !isInsertMode && !isPendingBulkEdit && !isRestoring)
            {
                grid.UpdateLayout();

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        _isUpdatingSelection = true;

                        if (tab.VimState.CurrentMode == VimMode.Insert)
                            return;

                        var row = grid.ItemContainerGenerator.ContainerFromIndex(pos.Row) as DataGridRow;
                        if (row != null)
                        {
                            var cell = GetCell(grid, row, pos.Column);
                            if (cell != null)
                            {
                                cell.Focus();
                            }
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        _isUpdatingSelection = false;
                    }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    private DataGridCell? GetCell(DataGrid grid, DataGridRow row, int columnIndex)
    {
        if (row == null || columnIndex < 0 || columnIndex >= grid.Columns.Count)
            return null;

        var presenter = FindVisualChild<DataGridCellsPresenter>(row);
        if (presenter == null)
            return null;

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
        if (parent == null)
            return null;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
                return typedChild;

            var result = FindVisualChild<T>(child);
            if (result != null)
                return result;
        }

        return null;
    }

    private void TsvGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DataGrid grid)
            return;

        // Check if Shift is held for horizontal scrolling
        bool isShiftPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        if (isShiftPressed)
        {
            // Find the ScrollViewer inside the DataGrid
            var scrollViewer = FindVisualChild<ScrollViewer>(grid);
            if (scrollViewer != null)
            {
                // Scroll horizontally: negative delta = scroll right, positive = scroll left
                double scrollAmount = e.Delta > 0 ? -50 : 50;
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + scrollAmount);
                e.Handled = true;
            }
        }
    }

    private void TsvGrid_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Only handle when Vim mode is disabled - enable Excel-like typing to edit
        if (_viewModel.IsVimModeEnabled)
            return;

        // If already editing (TextBox has focus), let the TextBox handle the input
        if (Keyboard.FocusedElement is TextBox)
            return;

        if (sender is DataGrid grid && grid.DataContext is TabItemViewModel tab)
        {
            // If not already in edit mode, start editing and insert the typed text
            if (!grid.IsReadOnly && grid.CurrentCell.Column != null)
            {
                // Store the pending text to be inserted when TextBox is ready
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
            if (cell != null && cell.Column != null)
            {
                var row = FindVisualParent<DataGridRow>(cell);
                if (row != null)
                {
                    var rowIndex = row.GetIndex();
                    var colIndex = grid.Columns.IndexOf(cell.Column);

                    if (rowIndex >= 0 && colIndex >= 0)
                    {
                        tab.VimState.CursorPosition = new GridPosition(rowIndex, colIndex);

                        // Only switch to Insert mode if Vim mode is enabled
                        if (_viewModel.IsVimModeEnabled && tab.VimState.CurrentMode != VimMode.Insert)
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
            if (cell != null && cell.Column != null)
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
                        {
                            tab2.VimState.SwitchMode(VimMode.Normal);
                        }
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
            if (cell != null && cell.Column != null)
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
                            (_dragStartPosition.Row != endPosition.Row ||
                             _dragStartPosition.Column != endPosition.Column))
                        {
                            _isUpdatingSelection = true;
                            try
                            {
                                tab.VimState.CurrentSelection = new SelectionRange(
                                    VisualType.Character,
                                    _dragStartPosition,
                                    endPosition);

                                tab.VimState.CursorPosition = endPosition;
                                grid.SelectedCells.Clear();
                            }
                            finally
                            {
                                _isUpdatingSelection = false;
                            }

                            tab.VimState.SwitchMode(VimMode.Visual);
                        }
                        else
                        {
                            _isUpdatingSelection = true;
                            try
                            {
                                tab.VimState.CurrentSelection = new SelectionRange(
                                    VisualType.Character,
                                    endPosition,
                                    endPosition);
                                tab.VimState.CursorPosition = endPosition;
                            }
                            finally
                            {
                                _isUpdatingSelection = false;
                            }
                        }

                        _dragStartPosition = null;
                    }
                }
            }
            else
            {
                _dragStartPosition = null;
            }
        }
        else
        {
            _dragStartPosition = null;
        }
    }

    private void TsvGrid_CurrentCellChangedHandler(object? sender, EventArgs e)
    {
        if (sender is DataGrid grid)
        {
            TsvGrid_CurrentCellChanged(grid, grid.DataContext as TabItemViewModel);
        }
    }

    private void TsvGrid_CurrentCellChanged(DataGrid grid, TabItemViewModel? tab)
    {
        if (grid == null || tab == null || _viewModel?.SelectedTab != tab)
            return;

        if (_isUpdatingSelection)
            return;

        if (_isDataGridDragging)
            return;

        if (grid.CurrentCell.Item != null && grid.CurrentCell.Column != null)
        {
            var rowIndex = grid.Items.IndexOf(grid.CurrentCell.Item);
            var colIndex = grid.Columns.IndexOf(grid.CurrentCell.Column);

            if (rowIndex >= 0 && colIndex >= 0)
            {
                var newPosition = new GridPosition(rowIndex, colIndex);

                if (tab.VimState.CursorPosition.Row != rowIndex ||
                    tab.VimState.CursorPosition.Column != colIndex)
                {
                    _isUpdatingSelection = true;
                    try
                    {
                        tab.VimState.CursorPosition = newPosition;
                    }
                    finally
                    {
                        _isUpdatingSelection = false;
                    }
                }
            }
        }
    }

    public void TsvGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (_viewModel?.SelectedTab == null)
            return;

        var tab = _viewModel.SelectedTab;

        // If Vim mode is disabled, allow editing without any Vim state checks
        if (!_viewModel.IsVimModeEnabled)
        {
            return;
        }

        var currentMode = tab.VimState.CurrentMode;

        if (currentMode == VimMode.Insert)
        {
            // Save the original cell value when entering insert mode (for undo and dot command tracking)
            int rowIndex = e.Row != null ? tab.GridViewModel.Document.Rows.IndexOf((Row)e.Row.Item) : -1;
            int columnIndex = e.Column?.DisplayIndex ?? -1;

            if (rowIndex >= 0 && columnIndex >= 0)
            {
                var cell = tab.GridViewModel.Document.GetCell(rowIndex, columnIndex);
                tab.VimState.InsertModeOriginalValue = cell?.Value ?? string.Empty;

                // Set InsertModeStartPosition if not already set (e.g., when entering via double-click)
                if (tab.VimState.InsertModeStartPosition == null)
                {
                    tab.VimState.InsertModeStartPosition = new GridPosition(rowIndex, columnIndex);
                }
            }
            return;
        }

        e.Cancel = true;
    }

    private void TsvGrid_PreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (e.EditingElement is TextBox textBox && _viewModel?.SelectedTab != null)
        {
            var tab = _viewModel.SelectedTab;

            // Subscribe to TextChanged for real-time cell content preview
            UnsubscribeFromCurrentEditingTextBox();
            _currentEditingTextBox = textBox;
            _currentTextChangedHandler = (s, evt) =>
            {
                if (_viewModel?.SelectedTab != null && textBox != null)
                {
                    _viewModel.SelectedTab.SelectedCellContent = textBox.Text;
                }
            };
            textBox.TextChanged += _currentTextChangedHandler;

            textBox.Loaded += (s, evt) =>
            {
                // Handle pending text input for non-Vim mode (Excel-like behavior)
                if (!_viewModel.IsVimModeEnabled && _pendingTextInput != null)
                {
                    textBox.Text = _pendingTextInput;
                    textBox.CaretIndex = textBox.Text.Length;
                    _pendingTextInput = null;
                }
                else if (tab.VimState.CellEditCaretPosition == CellEditCaretPosition.Start)
                {
                    textBox.CaretIndex = 0;
                }
                else
                {
                    textBox.CaretIndex = textBox.Text.Length;
                }
                textBox.Focus();
            };

            textBox.PreviewKeyDown += (s, evt) =>
            {
                Key actualKey = evt.Key;
                if (evt.Key == Key.ImeProcessed)
                {
                    actualKey = evt.ImeProcessedKey;
                }

                // Ctrl+Shift+; for date insertion works regardless of Vim mode
                if (actualKey == Key.OemPlus && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
                {
                    string currentDate = DateTime.Now.ToString("yyyy/MM/dd");
                    int caretIndex = textBox.CaretIndex;
                    textBox.Text = textBox.Text.Insert(caretIndex, currentDate);
                    textBox.CaretIndex = caretIndex + currentDate.Length;
                    evt.Handled = true;
                    return;
                }

                // Handle non-Vim mode: arrow keys move to adjacent cells (Excel-like behavior)
                if (!_viewModel.IsVimModeEnabled)
                {
                    // Only handle arrow keys when not processed by IME
                    if (evt.Key != Key.ImeProcessed &&
                        (actualKey == Key.Up || actualKey == Key.Down || actualKey == Key.Left || actualKey == Key.Right))
                    {
                        if (sender is DataGrid grid)
                        {
                            // Commit the edit first
                            grid.CommitEdit(DataGridEditingUnit.Cell, true);

                            // Calculate new position
                            var pos = tab.VimState.CursorPosition;
                            int newRow = pos.Row;
                            int newCol = pos.Column;
                            int maxRow = tab.GridViewModel.Document.RowCount - 1;
                            int maxCol = tab.GridViewModel.Document.ColumnCount - 1;

                            switch (actualKey)
                            {
                                case Key.Up:
                                    newRow = Math.Max(0, pos.Row - 1);
                                    break;
                                case Key.Down:
                                    newRow = Math.Min(maxRow, pos.Row + 1);
                                    break;
                                case Key.Left:
                                    newCol = Math.Max(0, pos.Column - 1);
                                    break;
                                case Key.Right:
                                    newCol = Math.Min(maxCol, pos.Column + 1);
                                    break;
                            }

                            // Move cursor
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
                    {
                        tab.VimState.PendingKeys.Clear();
                    }
                }
            };
        }
    }

    public void HandleModeChange(DataGrid grid, TabItemViewModel tab)
    {
        if (grid == null || tab == null)
            return;

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
                    {
                        grid.BeginEdit();
                    }
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
            else
            {
                // Commit edit asynchronously to allow TextBox key event processing to complete
                grid.Dispatcher.BeginInvoke(new Action(() =>
                {
                    grid.CommitEdit(DataGridEditingUnit.Cell, true);
                    grid.CommitEdit(DataGridEditingUnit.Row, true);

                    if (tab.VimState.PendingBulkEditRange != null)
                    {
                        ApplyBulkEdit(tab);
                    }

                    grid.Focus();
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }
        catch
        {
        }
    }

    // Track previously selected cells to avoid full document iteration
    private HashSet<(int row, int col)>? _previouslySelectedCells;

    private void ClearPreviousSelection(TsvDocument document)
    {
        if (document == null || _previouslySelectedCells == null || _previouslySelectedCells.Count == 0)
            return;

        // Only clear cells that were previously selected
        foreach (var (row, col) in _previouslySelectedCells)
        {
            if (row < document.RowCount && col < document.Rows[row].Cells.Count)
            {
                var cell = document.Rows[row].Cells[col];
                if (cell.IsSelected)
                {
                    cell.IsSelected = false;
                }
            }
        }
        _previouslySelectedCells.Clear();
    }

    private void ClearAllCellSelections(TsvDocument document)
    {
        if (document == null)
            return;

        // If we have tracked cells, only clear those
        if (_previouslySelectedCells != null && _previouslySelectedCells.Count > 0)
        {
            ClearPreviousSelection(document);
            return;
        }

        // Fallback: clear all (only needed for initial state or after tab switch)
        foreach (var row in document.Rows)
        {
            foreach (var cell in row.Cells)
            {
                if (cell.IsSelected)
                {
                    cell.IsSelected = false;
                }
            }
        }
    }

    private void InitializeVisualSelection(TabItemViewModel tab)
    {
        if (tab == null || tab.VimState.CurrentSelection == null)
            return;

        var selection = tab.VimState.CurrentSelection;
        var document = tab.Document;

        // Clear only previously selected cells
        ClearPreviousSelection(document);

        // Initialize tracking set
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
                        if (!rowObj.Cells[col].IsSelected)
                        {
                            rowObj.Cells[col].IsSelected = true;
                        }
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
                        if (!rowObj.Cells[col].IsSelected)
                        {
                            rowObj.Cells[col].IsSelected = true;
                        }
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
                        if (!rowObj.Cells[col].IsSelected)
                        {
                            rowObj.Cells[col].IsSelected = true;
                        }
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
            if (child is T parent)
                return parent;
        }

        return null;
    }

    public void TsvGrid_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        var dataGrid = sender as DataGrid;
        if (dataGrid == null)
            return;

        // Clear selection tracking when switching tabs
        var oldTab = e.OldValue as TabItemViewModel;
        if (oldTab != null)
        {
            ClearAllCellSelections(oldTab.Document);
        }
        _previouslySelectedCells?.Clear();

        var newTab = e.NewValue as TabItemViewModel;

        if (newTab != null)
        {
            // Check if this is a new DataGrid or same DataGrid with different tab
            bool isNewGrid = dataGrid.Tag as string != "Initialized";

            // Initialize DataGrid and setup VimState handlers
            InitializeDataGrid(dataGrid, newTab);
            SetupVimStateHandlers(dataGrid, newTab);

            // Only regenerate columns if this is not the first initialization
            // (InitializeDataGrid already handles the first time)
            if (!isNewGrid)
            {
                GenerateColumns(dataGrid, newTab);

                // Only auto-fit if column widths haven't been calculated yet
                if (newTab.ColumnWidths.Count == 0)
                {
                    AutoFitAllColumns(dataGrid, newTab);
                }
            }

            // Update DataGrid selection to sync with VimState cursor position
            // This ensures the selection is correct when switching tabs or opening files
            bool suppressFocusOnContextChange = _viewModel.IsRestoringSession;
            dataGrid.Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateDataGridSelection(dataGrid, newTab, suppressFocusOnContextChange);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    /// <summary>
    /// Scrolls the DataGrid for the specified tab so that the current cursor position is centered vertically
    /// </summary>
    public void ScrollToCenterForTab(TabItemViewModel tab)
    {
        System.Diagnostics.Debug.WriteLine("[DataGridManager] ScrollToCenterForTab called");

        if (!_tabToDataGrid.TryGetValue(tab, out var grid))
        {
            System.Diagnostics.Debug.WriteLine("[DataGridManager] DataGrid not found for tab");
            return;
        }

        System.Diagnostics.Debug.WriteLine("[DataGridManager] DataGrid found for tab");
        ScrollToCenter(grid, tab);
    }

    /// <summary>
    /// Scrolls the DataGrid so that the current cursor position is centered vertically
    /// </summary>
    public void ScrollToCenter(DataGrid grid, TabItemViewModel tab)
    {
        System.Diagnostics.Debug.WriteLine("[DataGridManager] ScrollToCenter called");
        if (grid == null || tab == null)
        {
            System.Diagnostics.Debug.WriteLine("[DataGridManager] Grid or tab is null");
            return;
        }

        var pos = tab.VimState.CursorPosition;
        System.Diagnostics.Debug.WriteLine($"[DataGridManager] Cursor position: Row={pos.Row}, Items.Count={grid.Items.Count}");

        if (grid.Items.Count == 0 || pos.Row < 0 || pos.Row >= grid.Items.Count)
        {
            System.Diagnostics.Debug.WriteLine("[DataGridManager] Invalid row position");
            return;
        }

        // First, ensure the row is in view
        grid.ScrollIntoView(grid.Items[pos.Row]);
        grid.UpdateLayout();

        // Find the ScrollViewer in the DataGrid's visual tree
        var scrollViewer = FindVisualChild<ScrollViewer>(grid);
        if (scrollViewer == null)
        {
            System.Diagnostics.Debug.WriteLine("[DataGridManager] ScrollViewer not found");
            return;
        }

        // Calculate the vertical position to center the current row
        var row = grid.ItemContainerGenerator.ContainerFromIndex(pos.Row) as DataGridRow;
        if (row == null)
        {
            System.Diagnostics.Debug.WriteLine("[DataGridManager] DataGridRow not found");
            return;
        }

        // Get the row's position relative to the viewport
        var transform = row.TransformToAncestor(scrollViewer);
        var rowPosition = transform.Transform(new Point(0, 0));

        // Calculate center offset
        double viewportHeight = scrollViewer.ViewportHeight;
        double rowHeight = row.ActualHeight;
        double currentOffset = scrollViewer.VerticalOffset;

        // Calculate the offset needed to center the row
        // We want the row to be at: (viewportHeight - rowHeight) / 2
        double desiredOffset = currentOffset + rowPosition.Y - (viewportHeight - rowHeight) / 2;

        // Clamp to valid scroll range
        desiredOffset = Math.Max(0, Math.Min(desiredOffset, scrollViewer.ScrollableHeight));

        System.Diagnostics.Debug.WriteLine($"[DataGridManager] Scrolling to offset: {desiredOffset} (current: {currentOffset}, viewport: {viewportHeight})");

        // Scroll to the calculated position
        scrollViewer.ScrollToVerticalOffset(desiredOffset);
    }

    private void ApplyBulkEdit(TabItemViewModel tab)
    {
        if (tab.VimState.PendingBulkEditRange == null)
            return;

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
            {
                insertedText = newValue.Substring(0, newValue.Length - originalValue.Length);
            }
        }
        else
        {
            if (newValue.StartsWith(originalValue))
            {
                insertedText = newValue.Substring(originalValue.Length);
            }
        }

        var cellUpdates = new Dictionary<GridPosition, string>();
        for (int r = 0; r < range.RowCount; r++)
        {
            for (int c = 0; c < range.ColumnCount; c++)
            {
                int docRow = range.StartRow + r;
                int docCol = range.StartColumn + c;

                if (docRow == range.StartRow && docCol == range.StartColumn)
                    continue;

                if (docRow < document.RowCount && docCol < document.Rows[docRow].Cells.Count)
                {
                    var cell = document.Rows[docRow].Cells[docCol];
                    string updatedValue;

                    if (caretPosition == CellEditCaretPosition.Start)
                    {
                        updatedValue = insertedText + cell.Value;
                    }
                    else
                    {
                        updatedValue = cell.Value + insertedText;
                    }

                    cellUpdates[new GridPosition(docRow, docCol)] = updatedValue;
                }
            }
        }

        if (cellUpdates.Count > 0)
        {
            var command = new BulkEditCellsWithValuesCommand(document, cellUpdates);

            if (tab.VimState.CommandHistory != null)
            {
                tab.VimState.CommandHistory.Execute(command);
            }
            else
            {
                command.Execute();
            }
        }

        tab.VimState.PendingBulkEditRange = null;
        tab.VimState.OriginalCellValueForBulkEdit = string.Empty;
    }

    /// <summary>
    /// Extracts the actually inserted text by comparing old and new values based on caret position
    /// </summary>
    /// <param name="originalValue">The cell value before editing</param>
    /// <param name="newValue">The cell value after editing</param>
    /// <param name="caretPosition">Where the caret was positioned (Start = 'i', End = 'a')</param>
    /// <returns>The text that was actually inserted</returns>
    private string ExtractInsertedText(string originalValue, string newValue, CellEditCaretPosition caretPosition)
    {
        if (string.IsNullOrEmpty(originalValue))
        {
            // If original was empty, everything is inserted text
            return newValue ?? string.Empty;
        }

        if (string.IsNullOrEmpty(newValue))
        {
            // If new value is empty, nothing was inserted (or everything was deleted)
            return string.Empty;
        }

        // For 'i' (caret at start): new text was prepended
        // For 'a' (caret at end): new text was appended
        if (caretPosition == CellEditCaretPosition.Start)
        {
            // Text was inserted at the beginning
            // Expected: newValue = insertedText + originalValue
            if (newValue.EndsWith(originalValue))
            {
                return newValue.Substring(0, newValue.Length - originalValue.Length);
            }
        }
        else // CellEditCaretPosition.End
        {
            // Text was appended at the end
            // Expected: newValue = originalValue + insertedText
            if (newValue.StartsWith(originalValue))
            {
                return newValue.Substring(originalValue.Length);
            }
        }

        // Fallback: if we can't determine the exact insertion, return the full new value
        // This handles cases where the user edited in the middle or made complex changes
        return newValue;
    }

    /// <summary>
    /// Recalculates and applies column widths for all open tabs
    /// Called when MaxColumnWidth setting is changed
    /// </summary>
    public void RecalculateAllColumnWidths()
    {
        foreach (var kvp in _tabToDataGrid)
        {
            var tab = kvp.Key;
            var grid = kvp.Value;

            if (grid != null && tab != null)
            {
                // Reset manual resize tracking so all columns can be resized
                tab.ResetManualResizeTracking();
                tab.ColumnWidths.Clear();

                // Recalculate all column widths
                AutoFitAllColumns(grid, tab);
            }
        }
    }

    /// <summary>
    /// Focuses the current cell of the specified tab's DataGrid.
    /// Call this after session restoration to restore keyboard focus.
    /// </summary>
    public void FocusCurrentTab(TabItemViewModel tab)
    {
        if (tab == null || !_tabToDataGrid.TryGetValue(tab, out var grid))
            return;

        var pos = tab.VimState.CursorPosition;
        if (grid.Items.Count == 0 || pos.Row < 0 || pos.Row >= grid.Items.Count ||
            pos.Column < 0 || pos.Column >= grid.Columns.Count)
            return;

        try
        {
            grid.UpdateLayout();
            var row = grid.ItemContainerGenerator.ContainerFromIndex(pos.Row) as DataGridRow;
            if (row != null)
            {
                var cell = GetCell(grid, row, pos.Column);
                cell?.Focus();
            }
        }
        catch { }
    }

    /// <summary>
    /// Cleans up cached event handlers for a tab (called when tab is closed)
    /// Phase 2 optimization: Prevents memory leaks from cached handlers
    /// </summary>
    public void CleanupTab(TabItemViewModel tab)
    {
        if (tab == null)
            return;

        // Remove cached handlers for this tab
        _tabHandlers.Remove(tab);

        // Remove from reverse lookup
        _tabToDataGrid.Remove(tab);

        // Remove from DataGrid handlers if present
        var dataGridToRemove = _dataGridHandlers.FirstOrDefault(kvp => kvp.Value.tab == tab).Key;
        if (dataGridToRemove != null)
        {
            _dataGridHandlers.Remove(dataGridToRemove);

            // Phase 1 optimization: Also remove column count tracking for this DataGrid
            _dataGridColumnCount.Remove(dataGridToRemove);
        }
    }
}
