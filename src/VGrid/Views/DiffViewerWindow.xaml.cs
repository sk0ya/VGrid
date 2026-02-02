using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using VGrid.ViewModels;

namespace VGrid.Views;

/// <summary>
/// Diff viewer window with file list and single DataGrid
/// </summary>
public partial class DiffViewerWindow : Window
{
    private readonly DiffViewerViewModel _viewModel;
    private HwndSource? _hwndSource;
    private ScrollViewer? _leftScrollViewer;
    private ScrollViewer? _rightScrollViewer;
    private bool _isScrollSyncing;
    private bool _isSelectionSyncing;

    // Win32 API for horizontal scroll
    private const int WM_MOUSEHWHEEL = 0x020E;

    public DiffViewerWindow(DiffViewerViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.CloseRequested += (s, e) => Close();

        // Listen to collection changes for DataGrids
        _viewModel.LeftRows.CollectionChanged += LeftRows_CollectionChanged;
        _viewModel.RightRows.CollectionChanged += RightRows_CollectionChanged;

        // Set row headers for DataGrids
        LeftDataGrid.LoadingRow += (s, e) =>
        {
            if (e.Row.Item is Models.DiffRow row)
                e.Row.Header = row.LeftLineNumber?.ToString() ?? string.Empty;
        };

        RightDataGrid.LoadingRow += (s, e) =>
        {
            if (e.Row.Item is Models.DiffRow row)
                e.Row.Header = row.RightLineNumber?.ToString() ?? string.Empty;
        };

        // Set up scroll synchronization and horizontal scroll support
        Loaded += DiffViewerWindow_Loaded;
        Closing += DiffViewerWindow_Closing;

        // Set up selection synchronization
        LeftDataGrid.SelectedCellsChanged += LeftDataGrid_SelectedCellsChanged;
        RightDataGrid.SelectedCellsChanged += RightDataGrid_SelectedCellsChanged;
    }

    // Window Control Button Handlers
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }
        else
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void LeftRows_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        {
            LeftDataGrid.Columns.Clear();
        }
        else if (_viewModel.LeftRows.Count > 0 && LeftDataGrid.Columns.Count == 0)
        {
            GenerateHorizontalDataGridColumns(LeftDataGrid);
        }
    }

    private void RightRows_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
        {
            RightDataGrid.Columns.Clear();
        }
        else if (_viewModel.RightRows.Count > 0 && RightDataGrid.Columns.Count == 0)
        {
            GenerateHorizontalDataGridColumns(RightDataGrid);
        }
    }

    private void GenerateHorizontalDataGridColumns(DataGrid dataGrid)
    {
        dataGrid.Columns.Clear();

        // Find maximum column count across all rows
        int maxColumns = 0;
        foreach (var row in _viewModel.LeftRows)
        {
            maxColumns = Math.Max(maxColumns, row.Cells.Count);
        }
        foreach (var row in _viewModel.RightRows)
        {
            maxColumns = Math.Max(maxColumns, row.Cells.Count);
        }

        for (int i = 0; i < maxColumns; i++)
        {
            int columnIndex = i; // Capture for closure

            var columnHeader = GetColumnName(i);

            var column = new DataGridTemplateColumn
            {
                Header = columnHeader,
                Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                MinWidth = 50
            };

            // Create cell template with TextBlock only
            var cellTemplate = new DataTemplate();
            var textBlock = new FrameworkElementFactory(typeof(TextBlock));
            textBlock.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding($"Cells[{columnIndex}].Value"));
            textBlock.SetValue(TextBlock.PaddingProperty, new Thickness(4, 2, 4, 2));
            textBlock.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
            textBlock.SetResourceReference(TextBlock.ForegroundProperty, "DiffForegroundBrush");
            cellTemplate.VisualTree = textBlock;
            column.CellTemplate = cellTemplate;

            // Create cell style to set background based on DiffStatus
            var cellStyle = new Style(typeof(DataGridCell), (Style)FindResource("ExcelCellStyle"));
            var backgroundSetter = new Setter();
            backgroundSetter.Property = DataGridCell.BackgroundProperty;
            backgroundSetter.Value = new System.Windows.Data.Binding($"Cells[{columnIndex}].Status")
            {
                Converter = (IValueConverter)FindResource("DiffStatusToColorConverter")
            };
            cellStyle.Setters.Add(backgroundSetter);
            column.CellStyle = cellStyle;

            dataGrid.Columns.Add(column);
        }
    }

    private string GetColumnName(int index)
    {
        // Excel-style column names: A, B, C, ..., Z, AA, AB, ...
        string result = "";
        while (index >= 0)
        {
            result = (char)('A' + (index % 26)) + result;
            index = (index / 26) - 1;
        }
        return result;
    }

    // Window lifecycle handlers for scroll setup
    private void DiffViewerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Set up horizontal scroll support via WndProc hook
        var windowHelper = new WindowInteropHelper(this);
        _hwndSource = HwndSource.FromHwnd(windowHelper.Handle);
        if (_hwndSource != null)
        {
            _hwndSource.AddHook(WndProc);
        }

        // Set up scroll synchronization after DataGrids are loaded
        SetupScrollSynchronization();
    }

    private void DiffViewerWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Clean up WndProc hook
        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }
    }

    private void SetupScrollSynchronization()
    {
        // Find ScrollViewers inside DataGrids
        _leftScrollViewer = FindVisualChild<ScrollViewer>(LeftDataGrid);
        _rightScrollViewer = FindVisualChild<ScrollViewer>(RightDataGrid);

        if (_leftScrollViewer != null && _rightScrollViewer != null)
        {
            _leftScrollViewer.ScrollChanged += LeftScrollViewer_ScrollChanged;
            _rightScrollViewer.ScrollChanged += RightScrollViewer_ScrollChanged;
        }
    }

    private void LeftScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isScrollSyncing || _rightScrollViewer == null) return;

        _isScrollSyncing = true;
        _rightScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
        _rightScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
        _isScrollSyncing = false;
    }

    private void RightScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isScrollSyncing || _leftScrollViewer == null) return;

        _isScrollSyncing = true;
        _leftScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
        _leftScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
        _isScrollSyncing = false;
    }

    // Selection synchronization
    private void LeftDataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
    {
        if (_isSelectionSyncing) return;
        SyncSelection(LeftDataGrid, RightDataGrid);
    }

    private void RightDataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
    {
        if (_isSelectionSyncing) return;
        SyncSelection(RightDataGrid, LeftDataGrid);
    }

    private void SyncSelection(DataGrid source, DataGrid target)
    {
        if (source.SelectedCells.Count == 0) return;

        _isSelectionSyncing = true;
        try
        {
            // Clear target selection
            target.SelectedCells.Clear();

            // Sync all selected cells
            foreach (var selectedCell in source.SelectedCells)
            {
                int rowIndex = source.Items.IndexOf(selectedCell.Item);
                int columnIndex = source.Columns.IndexOf(selectedCell.Column);

                if (rowIndex >= 0 && rowIndex < target.Items.Count &&
                    columnIndex >= 0 && columnIndex < target.Columns.Count)
                {
                    var targetCell = new DataGridCellInfo(target.Items[rowIndex], target.Columns[columnIndex]);
                    target.SelectedCells.Add(targetCell);
                }
            }
        }
        finally
        {
            _isSelectionSyncing = false;
        }
    }

    // Horizontal mouse wheel (tilt wheel) support
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_MOUSEHWHEEL)
        {
            int delta = (short)((wParam.ToInt64() >> 16) & 0xFFFF);

            // Find ScrollViewer under mouse and scroll it
            var element = Mouse.DirectlyOver as DependencyObject;
            var scrollViewer = FindVisualParent<ScrollViewer>(element);
            if (scrollViewer != null)
            {
                double scrollAmount = delta > 0 ? -50 : 50;
                scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + scrollAmount);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    // Helper method to find visual child of type T
    private static T? FindVisualChild<T>(DependencyObject? parent) where T : DependencyObject
    {
        if (parent == null) return null;

        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found)
                return found;

            var result = FindVisualChild<T>(child);
            if (result != null)
                return result;
        }
        return null;
    }

    // Helper method to find visual parent of type T
    private static T? FindVisualParent<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element != null)
        {
            if (element is T found)
                return found;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    // Context Menu Copy Handler
    private void ContextMenu_Copy_Click(object sender, RoutedEventArgs e)
    {
        // Determine which DataGrid triggered the context menu
        DataGrid? sourceGrid = null;
        if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu)
        {
            sourceGrid = contextMenu.PlacementTarget as DataGrid;
        }

        if (sourceGrid == null) return;

        var selectedCells = sourceGrid.SelectedCells;
        if (selectedCells.Count == 0) return;

        // Find the bounds of the selection
        int minRow = int.MaxValue, maxRow = int.MinValue;
        int minCol = int.MaxValue, maxCol = int.MinValue;

        var cellData = new Dictionary<(int row, int col), string>();

        foreach (var cellInfo in selectedCells)
        {
            int rowIndex = sourceGrid.Items.IndexOf(cellInfo.Item);
            int colIndex = sourceGrid.Columns.IndexOf(cellInfo.Column);

            if (rowIndex < 0 || colIndex < 0) continue;

            minRow = Math.Min(minRow, rowIndex);
            maxRow = Math.Max(maxRow, rowIndex);
            minCol = Math.Min(minCol, colIndex);
            maxCol = Math.Max(maxCol, colIndex);

            // Get the cell value
            if (cellInfo.Item is Models.DiffRow diffRow && colIndex < diffRow.Cells.Count)
            {
                cellData[(rowIndex, colIndex)] = diffRow.Cells[colIndex].Value ?? string.Empty;
            }
        }

        if (minRow == int.MaxValue) return;

        // Build TSV string
        var sb = new System.Text.StringBuilder();
        for (int r = minRow; r <= maxRow; r++)
        {
            for (int c = minCol; c <= maxCol; c++)
            {
                if (c > minCol) sb.Append('\t');
                if (cellData.TryGetValue((r, c), out var value))
                {
                    sb.Append(value);
                }
            }
            if (r < maxRow) sb.AppendLine();
        }

        try
        {
            Clipboard.SetText(sb.ToString());
        }
        catch
        {
            // Ignore clipboard errors
        }
    }
}
