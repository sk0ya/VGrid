using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using VGrid.ViewModels;

namespace VGrid.Views;

/// <summary>
/// Diff viewer window with file list and single DataGrid
/// </summary>
public partial class DiffViewerWindow : Window
{
    private readonly DiffViewerViewModel _viewModel;

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
        if (_viewModel.LeftRows.Count > 0 && LeftDataGrid.Columns.Count == 0)
        {
            GenerateHorizontalDataGridColumns(LeftDataGrid);
        }
    }

    private void RightRows_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (_viewModel.RightRows.Count > 0 && RightDataGrid.Columns.Count == 0)
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
}
