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
            return;

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

            grid.LoadingRow += (s, evt) => { evt.Row.Header = (evt.Row.GetIndex() + 1).ToString(); };

            grid.ItemContainerGenerator.StatusChanged += (s, evt) =>
            {
                if (grid.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
                {
                    UpdateAllRowHeaders(grid);
                }
            };

            grid.Dispatcher.BeginInvoke(new Action(() => { UpdateDataGridSelection(grid, tabItem); }),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading grid: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Phase 1 optimization: Update column bindings and widths without recreating columns
    /// </summary>
    private void UpdateColumnBindings(DataGrid grid, TabItemViewModel tab)
    {
        if (grid == null || tab == null)
            return;

        for (int i = 0; i < grid.Columns.Count; i++)
        {
            if (grid.Columns[i] is DataGridTextColumn textColumn)
            {
                // Update binding path (in case columns are reused across different documents)
                textColumn.Binding = new Binding($"Cells[{i}].Value")
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                };

                // Update width from tab's saved column widths
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

        var requiredColumnCount = Math.Max(50, tab.GridViewModel.ColumnCount);

        // Phase 1 optimization: Only regenerate if column count changed
        if (_dataGridColumnCount.TryGetValue(grid, out var existingCount) && existingCount == requiredColumnCount)
        {
            // Column count matches - just update bindings and widths
            UpdateColumnBindings(grid, tab);
            return;
        }

        // Column count changed - need to regenerate
        grid.Columns.Clear();

        for (int i = 0; i < requiredColumnCount; i++)
        {
            var columnIndex = i;
            var cellStyle = CreateVisualModeCellStyle(columnIndex);

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
                CellStyle = cellStyle
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
                // Record for dot command
                if (tab.VimState.PendingInsertType != ChangeType.None)
                {
                    tab.VimState.LastChange = new LastChange
                    {
                        Type = tab.VimState.PendingInsertType,
                        Count = 1,  // Insert operations don't use count prefix
                        InsertedText = newValue,
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
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private Style CreateVisualModeCellStyle(int columnIndex)
    {
        var style = new Style(typeof(DataGridCell));

        style.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(224, 224, 224))));
        style.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        style.Setters.Add(new Setter(DataGridCell.PaddingProperty, new Thickness(4, 2, 4, 2)));

        var selectionTrigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        selectionTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty, new SolidColorBrush(Color.FromRgb(212, 231, 247))));
        selectionTrigger.Setters.Add(new Setter(DataGridCell.ForegroundProperty, Brushes.Black));
        selectionTrigger.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(74, 144, 226))));
        selectionTrigger.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));

        var searchTrigger = new DataTrigger();
        searchTrigger.Binding = new Binding($"Cells[{columnIndex}].IsSearchMatch");
        searchTrigger.Value = true;
        searchTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty, new SolidColorBrush(Color.FromRgb(255, 255, 153))));
        searchTrigger.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(255, 215, 0))));
        searchTrigger.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        style.Triggers.Add(searchTrigger);

        var visualTrigger = new DataTrigger();
        visualTrigger.Binding = new Binding($"Cells[{columnIndex}].IsSelected");
        visualTrigger.Value = true;
        visualTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty, new SolidColorBrush(Color.FromRgb(212, 231, 247))));
        visualTrigger.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(74, 144, 226))));
        visualTrigger.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        style.Triggers.Add(visualTrigger);

        style.Triggers.Add(selectionTrigger);

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

    public void UpdateDataGridSelection(DataGrid grid, TabItemViewModel tab)
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

            if (!isFindReplaceOpen && !isInsertMode && !isPendingBulkEdit)
            {
                grid.Focus();
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

                        if (tab.VimState.CurrentMode != VimMode.Insert)
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

            textBox.Loaded += (s, evt) =>
            {
                if (tab.VimState.CellEditCaretPosition == CellEditCaretPosition.Start)
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

                if (actualKey == Key.OemPlus && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
                {
                    string currentDate = DateTime.Now.ToString("yyyy/MM/dd");
                    int caretIndex = textBox.CaretIndex;
                    textBox.Text = textBox.Text.Insert(caretIndex, currentDate);
                    textBox.CaretIndex = caretIndex + currentDate.Length;
                    evt.Handled = true;
                }
                else if (actualKey == Key.Enter && evt.Key != Key.ImeProcessed)
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

    private void ClearAllCellSelections(TsvDocument document)
    {
        if (document == null)
            return;

        foreach (var row in document.Rows)
        {
            foreach (var cell in row.Cells)
            {
                cell.IsSelected = false;
            }
        }
    }

    private void InitializeVisualSelection(TabItemViewModel tab)
    {
        if (tab == null || tab.VimState.CurrentSelection == null)
            return;

        var selection = tab.VimState.CurrentSelection;
        var document = tab.Document;

        ClearAllCellSelections(document);

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
                        rowObj.Cells[col].IsSelected = true;
                    }
                }
                break;

            case VisualType.Line:
                int lineStartRow = Math.Min(selection.Start.Row, selection.End.Row);
                int lineEndRow = Math.Max(selection.Start.Row, selection.End.Row);

                for (int row = lineStartRow; row <= lineEndRow && row < document.RowCount; row++)
                {
                    var rowObj = document.Rows[row];
                    foreach (var cell in rowObj.Cells)
                    {
                        cell.IsSelected = true;
                    }
                }
                break;

            case VisualType.Block:
                int blockStartCol = Math.Min(selection.Start.Column, selection.End.Column);
                int blockEndCol = Math.Max(selection.Start.Column, selection.End.Column);

                foreach (var row in document.Rows)
                {
                    for (int col = blockStartCol; col <= blockEndCol && col < row.Cells.Count; col++)
                    {
                        row.Cells[col].IsSelected = true;
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

        var newTab = e.NewValue as TabItemViewModel;

        // Phase 2 optimization: Unsubscribe old handlers from DataGrid
        if (_dataGridHandlers.TryGetValue(dataGrid, out var existingInfo))
        {
            if (existingInfo.tab == newTab)
                return;

            // Unsubscribe from old tab (but keep handlers cached for reuse)
            existingInfo.tab.VimState.PropertyChanged -= existingInfo.vimStateHandler;
            existingInfo.tab.Document.PropertyChanged -= existingInfo.documentHandler;
            existingInfo.tab.VimState.ColumnWidthUpdateRequested -= existingInfo.columnWidthHandler;
            _dataGridHandlers.Remove(dataGrid);
            _tabToDataGrid.Remove(existingInfo.tab);
        }

        if (newTab != null)
        {
            // Phase 2 optimization: Reuse handlers if they already exist for this tab
            if (!_tabHandlers.TryGetValue(newTab, out var handlers))
            {
                // Create handlers only once per tab
                PropertyChangedEventHandler vimStateHandler = (s, evt) =>
                {
                    if (evt.PropertyName == nameof(newTab.VimState.CursorPosition) && newTab == _viewModel?.SelectedTab)
                    {
                        if (_tabToDataGrid.TryGetValue(newTab, out var grid))
                            UpdateDataGridSelection(grid, newTab);
                    }
                    else if (evt.PropertyName == nameof(newTab.VimState.CurrentMode) && newTab == _viewModel?.SelectedTab)
                    {
                        if (_tabToDataGrid.TryGetValue(newTab, out var grid))
                            HandleModeChange(grid, newTab);
                    }
                    else if (evt.PropertyName == nameof(newTab.VimState.CurrentSelection) && newTab == _viewModel?.SelectedTab && newTab.VimState.CurrentMode == VimMode.Visual)
                    {
                        InitializeVisualSelection(newTab);
                    }
                };

                PropertyChangedEventHandler documentHandler = (s, evt) =>
                {
                    if (evt.PropertyName == nameof(TsvDocument.ColumnCount) && newTab == _viewModel?.SelectedTab)
                    {
                        if (_tabToDataGrid.TryGetValue(newTab, out var grid))
                            GenerateColumns(grid, newTab);
                    }
                };

                EventHandler<IEnumerable<int>> columnWidthHandler = (s, columnIndices) =>
                {
                    if (newTab == _viewModel?.SelectedTab)
                    {
                        if (_tabToDataGrid.TryGetValue(newTab, out var grid))
                            AutoFitColumns(grid, newTab, columnIndices);
                    }
                };

                handlers = (vimStateHandler, documentHandler, columnWidthHandler);
                _tabHandlers[newTab] = handlers;
            }

            // Subscribe to events (reusing cached handlers)
            newTab.VimState.PropertyChanged += handlers.vimStateHandler;
            newTab.Document.PropertyChanged += handlers.documentHandler;
            newTab.VimState.ColumnWidthUpdateRequested += handlers.columnWidthHandler;

            _dataGridHandlers[dataGrid] = (newTab, handlers.vimStateHandler, handlers.documentHandler, handlers.columnWidthHandler);
            _tabToDataGrid[newTab] = dataGrid;

            // Regenerate columns and auto-fit when switching to a new tab
            // This ensures column widths are properly applied when opening files
            GenerateColumns(dataGrid, newTab);

            // Only auto-fit if column widths haven't been calculated yet
            if (newTab.ColumnWidths.Count == 0)
            {
                AutoFitAllColumns(dataGrid, newTab);
            }

            // Update DataGrid selection to sync with VimState cursor position
            // This ensures the selection is correct when switching tabs or opening files
            dataGrid.Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateDataGridSelection(dataGrid, newTab);
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
