using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using VGrid.ViewModels;

namespace VGrid;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

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
            System.Windows.MessageBox.Show($"Error loading folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        var item = (TreeViewItem)sender;
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
        if (grid != null)
        {
            // Find the tab this grid belongs to
            var tabItem = grid.DataContext as TabItemViewModel;
            if (tabItem != null)
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
                };

                // Set row headers
                grid.LoadingRow += (s, evt) =>
                {
                    evt.Row.Header = (evt.Row.GetIndex() + 1).ToString();
                };

                // Initial selection update
                UpdateDataGridSelection(grid, tabItem);
            }
        }
    }

    private void GenerateColumns(DataGrid grid, TabItemViewModel tab)
    {
        grid.Columns.Clear();

        var columnCount = tab.GridViewModel.ColumnCount;
        for (int i = 0; i < columnCount; i++)
        {
            var columnIndex = i; // Capture for closure
            var column = new DataGridTextColumn
            {
                Header = GetExcelColumnName(i), // A, B, C, ... AA, AB, ...
                Binding = new System.Windows.Data.Binding($"Cells[{i}].Value")
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 60
            };

            grid.Columns.Add(column);
        }
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
        var pos = tab.VimState.CursorPosition;
        var doc = tab.GridViewModel.Document;

        // Ensure position is valid
        if (pos.Row >= 0 && pos.Row < doc.RowCount &&
            pos.Column >= 0 && pos.Column < grid.Columns.Count)
        {
            // Update DataGrid selection
            grid.SelectedIndex = pos.Row;
            if (grid.Items.Count > pos.Row)
            {
                grid.CurrentCell = new DataGridCellInfo(
                    grid.Items[pos.Row],
                    grid.Columns[pos.Column]);

                // Scroll into view
                grid.ScrollIntoView(grid.Items[pos.Row]);
            }
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

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Prevent default handling of some keys
        if (e.Key == Key.Escape || e.Key == Key.I || e.Key == Key.V ||
            e.Key == Key.H || e.Key == Key.J || e.Key == Key.K || e.Key == Key.L)
        {
            e.Handled = true;
        }
    }
}
