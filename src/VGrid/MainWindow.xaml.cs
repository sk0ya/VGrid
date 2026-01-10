using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using VGrid.ViewModels;

namespace VGrid;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    private bool _isUpdatingSelection = false;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // Subscribe to SelectedFolderPath changes
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.SelectedFolderPath))
            {
                PopulateFolderTree();
            }
        };

        // Set focus to the window
        Loaded += (s, e) => Focus();
    }

    private void PopulateFolderTree()
    {
        FolderTreeView.Items.Clear();

        if (string.IsNullOrEmpty(_viewModel?.SelectedFolderPath))
            return;

        try
        {
            var rootItem = new TreeViewItem
            {
                Header = Path.GetFileName(_viewModel.SelectedFolderPath) ?? _viewModel.SelectedFolderPath,
                Tag = _viewModel.SelectedFolderPath,
                IsExpanded = true
            };

            PopulateTreeNode(rootItem, _viewModel.SelectedFolderPath);
            FolderTreeView.Items.Add(rootItem);

            // Handle double-click on tree items
            FolderTreeView.MouseDoubleClick += FolderTreeView_MouseDoubleClick;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error loading folder: {ex.Message}", "Error", MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void PopulateTreeNode(TreeViewItem node, string path)
    {
        try
        {
            // Add subdirectories
            var directories = Directory.GetDirectories(path);
            foreach (var dir in directories)
            {
                var dirItem = new TreeViewItem
                {
                    Header = Path.GetFileName(dir),
                    Tag = dir
                };
                // Add a dummy item for lazy loading
                dirItem.Items.Add("Loading...");
                dirItem.Expanded += TreeViewItem_Expanded;
                node.Items.Add(dirItem);
            }

            // Add files (only TSV-related)
            var files = Directory.GetFiles(path, "*.tsv")
                .Concat(Directory.GetFiles(path, "*.txt"))
                .Concat(Directory.GetFiles(path, "*.tab"));

            foreach (var file in files)
            {
                var fileItem = new TreeViewItem
                {
                    Header = Path.GetFileName(file),
                    Tag = file
                };
                node.Items.Add(fileItem);
            }
        }
        catch
        {
            // Ignore errors for inaccessible directories
        }
    }

    private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
    {
        var item = (TreeViewItem) sender;
        if (item.Items.Count == 1 && item.Items[0] is string)
        {
            item.Items.Clear();
            var path = item.Tag as string;
            if (path != null)
            {
                PopulateTreeNode(item, path);
            }
        }
    }

    private async void FolderTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FolderTreeView.SelectedItem is TreeViewItem item)
        {
            var path = item.Tag as string;
            if (path != null && File.Exists(path))
            {
                await _viewModel!.OpenFileAsync(path);
            }
        }
    }

    private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Regenerate columns for the selected tab
        if (_viewModel?.SelectedTab != null)
        {
            // Find the DataGrid in the current tab
            var tabControl = sender as System.Windows.Controls.TabControl;
            if (tabControl != null && tabControl.SelectedIndex >= 0)
            {
                // Need to wait for the content to be loaded
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var selectedTab = _viewModel.SelectedTab;
                    if (selectedTab != null)
                    {
                        // The DataGrid will be regenerated when it loads
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }
    }

    private void TsvGrid_Loaded(object sender, RoutedEventArgs e)
    {
        var grid = sender as DataGrid;
        if (grid == null)
            return;

        // Find the tab this grid belongs to
        var tabItem = grid.DataContext as TabItemViewModel;
        if (tabItem == null)
            return;

        try
        {
            GenerateColumns(grid, tabItem);

            // Subscribe to VimState cursor position changes
            tabItem.VimState.PropertyChanged += (s, evt) =>
            {
                if (evt.PropertyName == nameof(tabItem.VimState.CursorPosition) &&
                    tabItem == _viewModel?.SelectedTab)
                {
                    UpdateDataGridSelection(grid, tabItem);
                }
                else if (evt.PropertyName == nameof(tabItem.VimState.CurrentMode) &&
                         tabItem == _viewModel?.SelectedTab)
                {
                    HandleModeChange(grid, tabItem);
                }
            };

            // Subscribe to DataGrid selection changes to update VimState
            grid.CurrentCellChanged += (s, evt) => { TsvGrid_CurrentCellChanged(grid, tabItem); };

            // Set row headers
            grid.LoadingRow += (s, evt) => { evt.Row.Header = (evt.Row.GetIndex() + 1).ToString(); };

            // Wait for grid to finish loading before initial selection
            grid.Dispatcher.BeginInvoke(new Action(() => { UpdateDataGridSelection(grid, tabItem); }),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error loading grid: {ex.Message}", "Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private void GenerateColumns(DataGrid grid, TabItemViewModel tab)
    {
        if (grid == null || tab == null)
            return;

        grid.Columns.Clear();

        // Show many columns like Excel (at least 50 columns, up to data columns)
        var columnCount = Math.Max(50, tab.GridViewModel.ColumnCount);

        for (int i = 0; i < columnCount; i++)
        {
            var columnIndex = i; // Capture for closure

            // Create cell style with visual mode selection support
            var cellStyle = CreateVisualModeCellStyle(columnIndex);

            var column = new DataGridTextColumn
            {
                Header = GetExcelColumnName(i), // A, B, C, ... AA, AB, ...
                Binding = new System.Windows.Data.Binding($"Cells[{i}].Value")
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                },
                Width = new DataGridLength(100, DataGridLengthUnitType.Pixel),
                MinWidth = 60,
                CellStyle = cellStyle
            };

            grid.Columns.Add(column);
        }
    }

    private Style CreateVisualModeCellStyle(int columnIndex)
    {
        var style = new Style(typeof(DataGridCell));

        // Base setters
        style.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 224, 224))));
        style.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        style.Setters.Add(new Setter(DataGridCell.PaddingProperty, new Thickness(4, 2, 4, 2)));

        // DataGrid's built-in selection trigger (blue highlight for current cell)
        var selectionTrigger = new Trigger { Property = DataGridCell.IsSelectedProperty, Value = true };
        selectionTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(212, 231, 247))));
        selectionTrigger.Setters.Add(new Setter(DataGridCell.ForegroundProperty, System.Windows.Media.Brushes.Black));
        selectionTrigger.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 144, 226))));
        selectionTrigger.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(2, 2, 2, 2)));
        style.Triggers.Add(selectionTrigger);

        // Visual mode Cell.IsSelected trigger (orange highlight for visual selection)
        var visualTrigger = new DataTrigger();
        visualTrigger.Binding = new System.Windows.Data.Binding($"Cells[{columnIndex}].IsSelected");
        visualTrigger.Value = true;
        visualTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 230, 204)))); // Orange
        visualTrigger.Setters.Add(new Setter(DataGridCell.BorderBrushProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 153, 0))));
        visualTrigger.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(1, 1, 1, 1)));
        style.Triggers.Add(visualTrigger);

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

    private void UpdateDataGridSelection(DataGrid grid, TabItemViewModel tab)
    {
        if (grid == null || tab == null)
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

            var cellInfo = new DataGridCellInfo(
                grid.Items[pos.Row],
                grid.Columns[pos.Column]);

            grid.SelectedCells.Add(cellInfo);
            grid.CurrentCell = cellInfo;

            // 行＋列を指定してスクロール
            grid.ScrollIntoView(
                grid.Items[pos.Row],
                grid.Columns[pos.Column]);

            grid.Focus();
        }
        finally
        {
            _isUpdatingSelection = false;
        }
    }

    private void TsvGrid_CurrentCellChanged(DataGrid grid, TabItemViewModel tab)
    {
        if (grid == null || tab == null || _viewModel?.SelectedTab != tab)
            return;

        // Skip if we're updating selection from VimState (avoid circular updates)
        if (_isUpdatingSelection)
            return;

        // Update VimState cursor position when user clicks on a cell
        if (grid.CurrentCell.Item != null && grid.CurrentCell.Column != null)
        {
            var rowIndex = grid.Items.IndexOf(grid.CurrentCell.Item);
            var colIndex = grid.Columns.IndexOf(grid.CurrentCell.Column);

            if (rowIndex >= 0 && colIndex >= 0)
            {
                var newPosition = new Models.GridPosition(rowIndex, colIndex);

                // Only update if position actually changed to avoid infinite loop
                if (tab.VimState.CursorPosition.Row != rowIndex ||
                    tab.VimState.CursorPosition.Column != colIndex)
                {
                    tab.VimState.CursorPosition = newPosition;
                }
            }
        }
    }

    private void HandleModeChange(DataGrid grid, TabItemViewModel tab)
    {
        if (grid == null || tab == null)
            return;

        try
        {
            // Clear visual selection when exiting Visual mode
            if (tab.VimState.CurrentSelection == null)
            {
                ClearAllCellSelections(tab.Document);
            }
            else if (tab.VimState.CurrentMode == VimEngine.VimMode.Visual)
            {
                // When entering Visual mode, initialize visual selection immediately
                InitializeVisualSelection(tab);
            }

            if (tab.VimState.CurrentMode == VimEngine.VimMode.Insert)
            {
                // Enter Insert mode - begin editing the current cell
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
                // Exit Insert mode - commit the edit
                grid.CommitEdit(DataGridEditingUnit.Cell, true);
                grid.CommitEdit(DataGridEditingUnit.Row, true);
            }
        }
        catch
        {
            // Ignore errors during edit mode changes
        }
    }

    private void ClearAllCellSelections(Models.TsvDocument document)
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

        // Clear any existing selections first
        ClearAllCellSelections(document);

        // Set initial selection based on visual type
        switch (selection.Type)
        {
            case VimEngine.VisualType.Character:
                // Select current cell only
                if (selection.Start.Row < document.RowCount)
                {
                    var row = document.Rows[selection.Start.Row];
                    if (selection.Start.Column < row.Cells.Count)
                    {
                        row.Cells[selection.Start.Column].IsSelected = true;
                    }
                }
                break;

            case VimEngine.VisualType.Line:
                // Select entire current row
                if (selection.Start.Row < document.RowCount)
                {
                    var row = document.Rows[selection.Start.Row];
                    foreach (var cell in row.Cells)
                    {
                        cell.IsSelected = true;
                    }
                }
                break;

            case VimEngine.VisualType.Block:
                // Select entire current column
                if (selection.Start.Column < document.ColumnCount)
                {
                    foreach (var row in document.Rows)
                    {
                        if (selection.Start.Column < row.Cells.Count)
                        {
                            row.Cells[selection.Start.Column].IsSelected = true;
                        }
                    }
                }
                break;
        }
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_viewModel?.SelectedTab == null)
            return;

        // Handle key through Vim state of the selected tab
        var handled = _viewModel.SelectedTab.VimState.HandleKey(
            e.Key,
            Keyboard.Modifiers,
            _viewModel.SelectedTab.GridViewModel.Document);

        if (handled)
        {
            e.Handled = true;
        }
    }
}