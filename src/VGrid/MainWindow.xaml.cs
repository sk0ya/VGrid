using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using VGrid.Models;
using VGrid.ViewModels;
using VGrid.VimEngine;

namespace VGrid;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    private bool _isUpdatingSelection = false;
    private bool _isDataGridDragging = false;
    private Models.GridPosition? _dragStartPosition = null;
    private System.Windows.Point _dragStartPoint;
    private TreeViewItem? _draggedItem;
    private HwndSource? _hwndSource;

    // Win32 API for clipboard monitoring
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    private const int WM_CLIPBOARDUPDATE = 0x031D;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // Subscribe to SelectedFolderPath and FilterText changes
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.SelectedFolderPath))
            {
                PopulateFolderTree();
            }
            else if (e.PropertyName == nameof(_viewModel.FilterText))
            {
                PopulateFolderTree();
            }
        };

        // Set focus to the window and restore session asynchronously
        Loaded += MainWindow_Loaded;

        // Save session on window closing
        Closing += MainWindow_Closing;
    }

    private void PopulateFolderTree()
    {
        FolderTreeView.Items.Clear();

        if (string.IsNullOrEmpty(_viewModel?.SelectedFolderPath))
            return;

        try
        {
            var rootName = Path.GetFileName(_viewModel.SelectedFolderPath) ?? _viewModel.SelectedFolderPath;
            var filterText = _viewModel.FilterText ?? string.Empty;

            var rootItem = new TreeViewItem
            {
                Header = CreateHeaderWithIcon(rootName, filterText, true),
                Tag = _viewModel.SelectedFolderPath,
                IsExpanded = true,
                AllowDrop = true
            };

            // Add drag-and-drop event handlers for root
            rootItem.DragOver += TreeViewItem_DragOver;
            rootItem.Drop += TreeViewItem_Drop;
            rootItem.DragLeave += TreeViewItem_DragLeave;

            // Add context menu for root directory
            var rootContextMenu = new ContextMenu();

            var newFileMenuItem = new MenuItem { Header = "新しいファイル(_F)" };
            newFileMenuItem.Click += NewFileMenuItem_Click;
            rootContextMenu.Items.Add(newFileMenuItem);

            var newFolderMenuItem = new MenuItem { Header = "新しいフォルダ(_N)" };
            newFolderMenuItem.Click += NewFolderMenuItem_Click;
            rootContextMenu.Items.Add(newFolderMenuItem);

            rootContextMenu.Items.Add(new Separator());

            var openRootInExplorerMenuItem = new MenuItem { Header = "エクスプローラーで開く(_E)" };
            openRootInExplorerMenuItem.Click += OpenFolderInExplorerMenuItem_Click;
            rootContextMenu.Items.Add(openRootInExplorerMenuItem);

            rootItem.ContextMenu = rootContextMenu;

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
            var filterText = _viewModel?.FilterText ?? string.Empty;
            var hasFilter = !string.IsNullOrWhiteSpace(filterText);

            // Add subdirectories
            var directories = Directory.GetDirectories(path);
            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);

                // Skip hidden folders starting with '.'
                if (dirName.StartsWith("."))
                    continue;

                // If filter is active, check if directory name matches or if it contains matching files
                if (hasFilter)
                {
                    // Check if directory name matches filter
                    bool dirMatches = dirName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0;

                    // Check if directory contains matching files (recursively)
                    bool hasMatchingContent = dirMatches || DirectoryContainsMatchingFiles(dir, filterText);

                    if (!hasMatchingContent)
                        continue;
                }

                var dirItem = new TreeViewItem
                {
                    Header = CreateHeaderWithIcon(dirName, filterText, true),
                    Tag = dir,
                    AllowDrop = true
                };
                // Add a dummy item for lazy loading
                dirItem.Items.Add("Loading...");
                dirItem.Expanded += TreeViewItem_Expanded;

                // Add drag-and-drop event handlers
                dirItem.PreviewMouseLeftButtonDown += TreeViewItem_PreviewMouseLeftButtonDown;
                dirItem.PreviewMouseMove += TreeViewItem_PreviewMouseMove;
                dirItem.DragOver += TreeViewItem_DragOver;
                dirItem.Drop += TreeViewItem_Drop;
                dirItem.DragLeave += TreeViewItem_DragLeave;

                // Add context menu for directories
                var contextMenu = new ContextMenu();

                var newFileMenuItem = new MenuItem { Header = "新しいファイル(_F)" };
                newFileMenuItem.Click += NewFileMenuItem_Click;
                contextMenu.Items.Add(newFileMenuItem);

                var newFolderMenuItem = new MenuItem { Header = "新しいフォルダ(_N)" };
                newFolderMenuItem.Click += NewFolderMenuItem_Click;
                contextMenu.Items.Add(newFolderMenuItem);

                contextMenu.Items.Add(new Separator());

                var renameFolderMenuItem = new MenuItem { Header = "フォルダ名の変更(_R)" };
                renameFolderMenuItem.Click += RenameFolderMenuItem_Click;
                contextMenu.Items.Add(renameFolderMenuItem);

                var deleteFolderMenuItem = new MenuItem { Header = "削除(_D)" };
                deleteFolderMenuItem.Click += DeleteFolderMenuItem_Click;
                contextMenu.Items.Add(deleteFolderMenuItem);

                contextMenu.Items.Add(new Separator());

                var openFolderInExplorerMenuItem = new MenuItem { Header = "エクスプローラーで開く(_E)" };
                openFolderInExplorerMenuItem.Click += OpenFolderInExplorerMenuItem_Click;
                contextMenu.Items.Add(openFolderInExplorerMenuItem);

                dirItem.ContextMenu = contextMenu;

                node.Items.Add(dirItem);
            }

            // Add files (only TSV-related)
            var files = Directory.GetFiles(path, "*.tsv")
                .Concat(Directory.GetFiles(path, "*.txt"))
                .Concat(Directory.GetFiles(path, "*.tab"));

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);

                // Apply filter if active
                if (hasFilter && fileName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var fileItem = new TreeViewItem
                {
                    Header = CreateHeaderWithIcon(fileName, filterText, false),
                    Tag = file
                };

                // Add drag-and-drop event handlers for files
                fileItem.PreviewMouseLeftButtonDown += TreeViewItem_PreviewMouseLeftButtonDown;
                fileItem.PreviewMouseMove += TreeViewItem_PreviewMouseMove;

                // Add context menu only for files
                var contextMenu = new ContextMenu();
                var renameMenuItem = new MenuItem
                {
                    Header = "名前の変更(_R)"
                };
                renameMenuItem.Click += RenameMenuItem_Click;
                contextMenu.Items.Add(renameMenuItem);

                var deleteMenuItem = new MenuItem
                {
                    Header = "削除(_D)"
                };
                deleteMenuItem.Click += DeleteFileMenuItem_Click;
                contextMenu.Items.Add(deleteMenuItem);

                contextMenu.Items.Add(new Separator());

                var openInExplorerMenuItem = new MenuItem
                {
                    Header = "エクスプローラーで開く(_E)"
                };
                openInExplorerMenuItem.Click += OpenFileInExplorerMenuItem_Click;
                contextMenu.Items.Add(openInExplorerMenuItem);

                fileItem.ContextMenu = contextMenu;

                node.Items.Add(fileItem);
            }
        }
        catch
        {
            // Ignore errors for inaccessible directories
        }
    }

    private bool DirectoryContainsMatchingFiles(string path, string filterText)
    {
        try
        {
            // Check files in current directory
            var files = Directory.GetFiles(path, "*.tsv")
                .Concat(Directory.GetFiles(path, "*.txt"))
                .Concat(Directory.GetFiles(path, "*.tab"));

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);
                if (fileName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            // Check subdirectories recursively
            var directories = Directory.GetDirectories(path);
            foreach (var dir in directories)
            {
                var dirName = Path.GetFileName(dir);

                // Skip hidden folders starting with '.'
                if (dirName.StartsWith("."))
                    continue;

                // Check if directory name matches
                if (dirName.IndexOf(filterText, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                // Check if subdirectory contains matching files
                if (DirectoryContainsMatchingFiles(dir, filterText))
                    return true;
            }

            return false;
        }
        catch
        {
            // Ignore errors for inaccessible directories
            return false;
        }
    }

    private object CreateHighlightedHeader(string text, string filterText)
    {
        // If no filter, return plain text
        if (string.IsNullOrWhiteSpace(filterText))
            return text;

        var textBlock = new System.Windows.Controls.TextBlock();
        int currentIndex = 0;

        while (currentIndex < text.Length)
        {
            // Find next match
            int matchIndex = text.IndexOf(filterText, currentIndex, StringComparison.OrdinalIgnoreCase);

            if (matchIndex == -1)
            {
                // No more matches - add remaining text
                if (currentIndex < text.Length)
                {
                    textBlock.Inlines.Add(new System.Windows.Documents.Run(text.Substring(currentIndex)));
                }
                break;
            }

            // Add text before match
            if (matchIndex > currentIndex)
            {
                textBlock.Inlines.Add(new System.Windows.Documents.Run(text.Substring(currentIndex, matchIndex - currentIndex)));
            }

            // Add highlighted match
            var matchRun = new System.Windows.Documents.Run(text.Substring(matchIndex, filterText.Length))
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 0)), // Yellow
                FontWeight = FontWeights.Bold
            };
            textBlock.Inlines.Add(matchRun);

            currentIndex = matchIndex + filterText.Length;
        }

        return textBlock;
    }

    private object CreateHeaderWithIcon(string text, string filterText, bool isFolder)
    {
        var stackPanel = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal
        };

        // Add icon
        var icon = new System.Windows.Controls.TextBlock
        {
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI Emoji"),
            FontSize = 14,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        if (isFolder)
        {
            icon.Text = "📁"; // Folder icon
            icon.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 193, 7)); // Amber
        }
        else
        {
            // Determine file icon based on extension
            var ext = Path.GetExtension(text).ToLower();
            if (ext == ".tsv" || ext == ".tab")
            {
                icon.Text = "📊"; // Chart icon for TSV files
                icon.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80)); // Green
            }
            else if (ext == ".txt")
            {
                icon.Text = "📄"; // Document icon for text files
                icon.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(96, 125, 139)); // Blue Grey
            }
            else
            {
                icon.Text = "📄"; // Default document icon
                icon.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(158, 158, 158)); // Grey
            }
        }

        stackPanel.Children.Add(icon);

        // Add text (with highlighting if filter is active)
        var textContent = CreateHighlightedHeader(text, filterText);
        if (textContent is string str)
        {
            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = str,
                VerticalAlignment = VerticalAlignment.Center
            };
            stackPanel.Children.Add(textBlock);
        }
        else if (textContent is System.Windows.Controls.TextBlock tb)
        {
            tb.VerticalAlignment = VerticalAlignment.Center;
            stackPanel.Children.Add(tb);
        }

        return stackPanel;
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


    private void TsvGrid_Loaded(object sender, RoutedEventArgs e)
    {
        var grid = sender as DataGrid;
        if (grid == null)
            return;

        // Find the tab this grid belongs to
        var tabItem = grid.DataContext as TabItemViewModel;
        if (tabItem == null)
            return;

        // Check if this grid has already been initialized to prevent duplicate event handler registration
        // Only initialize DataGrid-level handlers once
        if (grid.Tag as string == "Initialized")
            return;

        grid.Tag = "Initialized";

        try
        {
            GenerateColumns(grid, tabItem);

            // Auto-fit columns on file load
            AutoFitAllColumns(grid, tabItem);

            // Subscribe to DataGrid selection changes to update VimState
            // Remove any existing handler first to prevent duplicate subscriptions
            grid.CurrentCellChanged -= TsvGrid_CurrentCellChangedHandler;
            grid.CurrentCellChanged += TsvGrid_CurrentCellChangedHandler;

            // Subscribe to cell editing to handle Enter/Escape keys in Insert mode
            grid.PreparingCellForEdit -= TsvGrid_PreparingCellForEdit;
            grid.PreparingCellForEdit += TsvGrid_PreparingCellForEdit;

            // Subscribe to mouse events to detect dragging
            grid.PreviewMouseLeftButtonDown -= TsvGrid_PreviewMouseLeftButtonDown;
            grid.PreviewMouseLeftButtonDown += TsvGrid_PreviewMouseLeftButtonDown;
            grid.PreviewMouseLeftButtonUp -= TsvGrid_PreviewMouseLeftButtonUp;
            grid.PreviewMouseLeftButtonUp += TsvGrid_PreviewMouseLeftButtonUp;

            // Set row headers
            grid.LoadingRow += (s, evt) => { evt.Row.Header = (evt.Row.GetIndex() + 1).ToString(); };

            // Update row headers when items are regenerated (after add/delete operations)
            grid.ItemContainerGenerator.StatusChanged += (s, evt) =>
            {
                if (grid.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
                {
                    UpdateAllRowHeaders(grid);
                }
            };

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
                // Use calculated width if available, otherwise use default
                Width = tab.ColumnWidths.ContainsKey(i)
                    ? new DataGridLength(tab.ColumnWidths[i], DataGridLengthUnitType.Pixel)
                    : new DataGridLength(100, DataGridLengthUnitType.Pixel),
                MinWidth = 60,
                CellStyle = cellStyle
            };

            grid.Columns.Add(column);
        }
    }

    /// <summary>
    /// Auto-fits all columns based on content (called on file load)
    /// </summary>
    private void AutoFitAllColumns(DataGrid grid, TabItemViewModel tab)
    {
        if (_viewModel == null || grid == null || tab == null)
            return;

        // Get font settings from DataGrid
        var typeface = new Typeface(
            grid.FontFamily ?? new System.Windows.Media.FontFamily("Segoe UI"),
            grid.FontStyle,
            grid.FontWeight,
            grid.FontStretch
        );
        double fontSize = grid.FontSize > 0 ? grid.FontSize : 11;

        // Calculate widths for all columns
        var widths = _viewModel.ColumnWidthService.CalculateAllColumnWidths(
            tab.Document,
            typeface,
            fontSize
        );

        // Store widths in tab
        tab.ColumnWidths = widths;
        tab.ResetManualResizeTracking();

        // Apply widths to grid columns
        for (int i = 0; i < grid.Columns.Count && i < widths.Count; i++)
        {
            if (grid.Columns[i] is DataGridTextColumn column)
            {
                column.Width = new DataGridLength(widths[i], DataGridLengthUnitType.Pixel);
            }
        }
    }

    /// <summary>
    /// Auto-fits a single column based on content (called after cell edit)
    /// </summary>
    private void AutoFitColumn(DataGrid grid, TabItemViewModel tab, int columnIndex)
    {
        if (_viewModel == null || grid == null || tab == null)
            return;

        // Skip if user manually resized this column
        if (tab.ManuallyResizedColumns.Contains(columnIndex))
            return;

        // Get font settings from DataGrid
        var typeface = new Typeface(
            grid.FontFamily ?? new System.Windows.Media.FontFamily("Segoe UI"),
            grid.FontStyle,
            grid.FontWeight,
            grid.FontStretch
        );
        double fontSize = grid.FontSize > 0 ? grid.FontSize : 11;

        // Calculate width for this column
        double width = _viewModel.ColumnWidthService.CalculateColumnWidth(
            tab.Document,
            columnIndex,
            typeface,
            fontSize
        );

        // Update stored width
        tab.ColumnWidths[columnIndex] = width;

        // Apply width to grid column
        if (columnIndex < grid.Columns.Count && grid.Columns[columnIndex] is DataGridTextColumn column)
        {
            column.Width = new DataGridLength(width, DataGridLengthUnitType.Pixel);
        }
    }

    /// <summary>
    /// Handles cell edit ending - triggers auto-fit for edited column
    /// </summary>
    private void TsvGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (_viewModel?.SelectedTab == null || e.Cancel)
            return;

        var grid = sender as DataGrid;
        if (grid == null)
            return;

        var tab = _viewModel.SelectedTab;
        int columnIndex = e.Column.DisplayIndex;

        // Schedule auto-fit after edit completes (use Dispatcher to ensure edit is committed)
        grid.Dispatcher.BeginInvoke(new Action(() =>
        {
            AutoFitColumn(grid, tab, columnIndex);
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private Style CreateVisualModeCellStyle(int columnIndex)
    {
        var style = new Style(typeof(DataGridCell));

        // Base setters
        style.Setters.Add(new Setter(DataGridCell.BorderBrushProperty,
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 224, 224))));
        style.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        style.Setters.Add(new Setter(DataGridCell.PaddingProperty, new Thickness(4, 2, 4, 2)));

        // DataGrid's built-in selection trigger (blue highlight for current cell)
        var selectionTrigger = new Trigger {Property = DataGridCell.IsSelectedProperty, Value = true};
        selectionTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty,
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(212, 231, 247))));
        selectionTrigger.Setters.Add(new Setter(DataGridCell.ForegroundProperty, System.Windows.Media.Brushes.Black));
        selectionTrigger.Setters.Add(new Setter(DataGridCell.BorderBrushProperty,
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 144, 226))));
        selectionTrigger.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));

        // Search match Cell.IsSearchMatch trigger (yellow highlight for current search match)
        // Added FIRST so that blue selection can override it
        var searchTrigger = new DataTrigger();
        searchTrigger.Binding = new System.Windows.Data.Binding($"Cells[{columnIndex}].IsSearchMatch");
        searchTrigger.Value = true;
        searchTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty,
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 153)))); // Yellow
        searchTrigger.Setters.Add(new Setter(DataGridCell.BorderBrushProperty,
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0))));
        searchTrigger.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        style.Triggers.Add(searchTrigger);

        // Visual mode Cell.IsSelected trigger (blue highlight for visual selection)
        // Added AFTER search trigger so blue selection overrides yellow search
        var visualTrigger = new DataTrigger();
        visualTrigger.Binding = new System.Windows.Data.Binding($"Cells[{columnIndex}].IsSelected");
        visualTrigger.Value = true;
        visualTrigger.Setters.Add(new Setter(DataGridCell.BackgroundProperty,
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(212, 231, 247)))); // Blue
        visualTrigger.Setters.Add(new Setter(DataGridCell.BorderBrushProperty,
            new SolidColorBrush(System.Windows.Media.Color.FromRgb(74, 144, 226))));
        visualTrigger.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0, 0, 1, 1)));
        style.Triggers.Add(visualTrigger);

        // Selection with focus trigger (highest priority - added last)
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

    private void UpdateDataGridSelection(DataGrid grid, TabItemViewModel tab)
    {
        if (grid == null || tab == null)
            return;

        // Visual mode時はDataGridの組み込み選択を使わない（Cell.IsSelectedのみを使用）
        if (tab.VimState.CurrentMode == VimEngine.VimMode.Visual)
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

            // Check if FindReplace panel is open - if so, don't move focus to the cell
            bool isFindReplaceOpen = tab.FindReplaceViewModel?.IsVisible ?? false;

            // Don't steal focus from TextBox if in Insert mode or about to enter Insert mode for bulk edit
            bool isInsertMode = tab.VimState.CurrentMode == VimEngine.VimMode.Insert;
            bool isPendingBulkEdit = tab.VimState.PendingBulkEditRange != null;

            if (!isFindReplaceOpen && !isInsertMode && !isPendingBulkEdit)
            {
                // Focus the DataGrid and try to focus the specific cell
                grid.Focus();

                // Force update layout to ensure cell is generated
                grid.UpdateLayout();

                // Try to focus the specific cell for better visibility
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // Double-check Insert mode status before focusing
                        // (mode might have changed after CursorPosition update)
                        if (tab.VimState.CurrentMode == VimEngine.VimMode.Insert)
                            return;

                        // Get the row container
                        var row = grid.ItemContainerGenerator.ContainerFromIndex(pos.Row) as DataGridRow;
                        if (row != null)
                        {
                            // Get the cell
                            var cell = GetCell(grid, row, pos.Column);
                            if (cell != null)
                            {
                                cell.Focus();
                            }
                        }
                    }
                    catch
                    {
                        // Ignore errors - cell focusing is best-effort
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
            // Cell might be virtualized, force it to be generated
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
        System.Diagnostics.Debug.WriteLine($"[MouseDown] Starting drag, ClickCount={e.ClickCount}");

        // Handle double-click to enter Insert mode
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
                        System.Diagnostics.Debug.WriteLine($"[DoubleClick] Position: ({rowIndex},{colIndex})");

                        // Update cursor position
                        tab.VimState.CursorPosition = new Models.GridPosition(rowIndex, colIndex);

                        // Switch to Insert mode if not already in it
                        if (tab.VimState.CurrentMode != VimEngine.VimMode.Insert)
                        {
                            tab.VimState.SwitchMode(VimEngine.VimMode.Insert);
                            tab.VimState.CellEditCaretPosition = VimEngine.CellEditCaretPosition.End;
                        }

                        // Begin editing the cell
                        grid.CurrentCell = new DataGridCellInfo(cell);
                        grid.BeginEdit();

                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        _isDataGridDragging = true;

        // Record the starting position for drag selection
        if (sender is DataGrid grid2 && grid2.DataContext is TabItemViewModel tab2)
        {
            // 実際にクリックされたセルを取得
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
                        _dragStartPosition = new Models.GridPosition(rowIndex, colIndex);
                        System.Diagnostics.Debug.WriteLine($"[MouseDown] Start position: ({rowIndex},{colIndex})");

                        // ドラッグ開始時に以前のVisual mode選択状態をクリア
                        if (tab2.VimState.CurrentMode == VimEngine.VimMode.Visual)
                        {
                            // Visual modeから抜けて、以前の選択をクリア
                            tab2.VimState.SwitchMode(VimEngine.VimMode.Normal);
                        }
                    }
                }
            }
        }
    }

    private void TsvGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[MouseUp] Ending drag");
        _isDataGridDragging = false;

        // Update VimState with the drag selection range
        if (sender is DataGrid grid && grid.DataContext is TabItemViewModel tab)
        {
            // 実際にクリックされたセルを取得
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
                        var endPosition = new Models.GridPosition(endRowIndex, endColIndex);
                        System.Diagnostics.Debug.WriteLine($"[MouseUp] End position: ({endRowIndex},{endColIndex})");

                        // Check if this was a drag (multiple cells selected) or a single click
                        if (_dragStartPosition != null &&
                            (_dragStartPosition.Row != endPosition.Row ||
                             _dragStartPosition.Column != endPosition.Column))
                        {
                            // This was a drag - enter Visual mode with selection range
                            System.Diagnostics.Debug.WriteLine($"[MouseUp] Drag detected, entering Visual mode");

                            _isUpdatingSelection = true;
                            try
                            {
                                tab.VimState.CurrentSelection = new VimEngine.SelectionRange(
                                    VimEngine.VisualType.Character,
                                    _dragStartPosition,
                                    endPosition);

                                tab.VimState.CursorPosition = endPosition;

                                // DataGridの組み込み選択をクリア（Visual modeではCell.IsSelectedを使う）
                                grid.SelectedCells.Clear();
                            }
                            finally
                            {
                                _isUpdatingSelection = false;
                            }

                            // モード切り替え（HandleModeChangeが呼ばれて選択範囲が設定される）
                            tab.VimState.SwitchMode(VimEngine.VimMode.Visual);
                        }
                        else
                        {
                            // Single click - just move cursor
                            System.Diagnostics.Debug.WriteLine($"[MouseUp] Single click, updating cursor position");
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
        System.Diagnostics.Debug.WriteLine($"[CurrentCellChanged] Tab:{tab?.Header}, Grid:{grid?.GetHashCode()}, IsUpdating:{_isUpdatingSelection}, IsDragging:{_isDataGridDragging}");

        if (grid == null || tab == null || _viewModel?.SelectedTab != tab)
        {
            System.Diagnostics.Debug.WriteLine($"[CurrentCellChanged] Early return: grid={grid!=null}, tab={tab!=null}, isSelected={_viewModel?.SelectedTab == tab}");
            return;
        }

        // Skip if we're updating selection from VimState (avoid circular updates)
        if (_isUpdatingSelection)
        {
            System.Diagnostics.Debug.WriteLine($"[CurrentCellChanged] Skip: _isUpdatingSelection=true");
            return;
        }

        // Skip if user is dragging (will update on mouse up instead)
        if (_isDataGridDragging)
        {
            System.Diagnostics.Debug.WriteLine($"[CurrentCellChanged] Skip: dragging in progress");
            return;
        }

        // Update VimState cursor position when user clicks on a cell
        if (grid.CurrentCell.Item != null && grid.CurrentCell.Column != null)
        {
            var rowIndex = grid.Items.IndexOf(grid.CurrentCell.Item);
            var colIndex = grid.Columns.IndexOf(grid.CurrentCell.Column);

            System.Diagnostics.Debug.WriteLine($"[CurrentCellChanged] Cell: ({rowIndex},{colIndex})");

            if (rowIndex >= 0 && colIndex >= 0)
            {
                var newPosition = new Models.GridPosition(rowIndex, colIndex);

                // Only update if position actually changed to avoid infinite loop
                if (tab.VimState.CursorPosition.Row != rowIndex ||
                    tab.VimState.CursorPosition.Column != colIndex)
                {
                    System.Diagnostics.Debug.WriteLine($"[CurrentCellChanged] Updating VimState.CursorPosition to ({rowIndex},{colIndex})");
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
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[CurrentCellChanged] Position unchanged, skipping");
                }
            }
        }
    }

    private void TsvGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if (_viewModel?.SelectedTab == null)
            return;

        var tab = _viewModel.SelectedTab;
        var currentMode = tab.VimState.CurrentMode;

        // Only allow edit in Insert mode
        // In Normal mode, we handle mode switching explicitly in HandleKey
        if (currentMode == VimEngine.VimMode.Insert)
        {
            return;
        }

        // Cancel edit in all other modes (Normal, Command, Visual)
        // The mode switching will be handled by VimState.HandleKey
        e.Cancel = true;
    }

    private void TsvGrid_PreparingCellForEdit(object? sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (e.EditingElement is System.Windows.Controls.TextBox textBox && _viewModel?.SelectedTab != null)
        {
            var tab = _viewModel.SelectedTab;

            // Set caret position based on VimState setting
            textBox.Loaded += (s, evt) =>
            {
                if (tab.VimState.CellEditCaretPosition == VimEngine.CellEditCaretPosition.Start)
                {
                    textBox.CaretIndex = 0;
                }
                else
                {
                    textBox.CaretIndex = textBox.Text.Length;
                }
                textBox.Focus();
            };

            // Attach KeyDown handler to the editing TextBox
            textBox.PreviewKeyDown += (s, evt) =>
            {
                // Get the actual key - handle IME processed keys
                Key actualKey = evt.Key;
                if (evt.Key == Key.ImeProcessed)
                {
                    // When IME is on, the actual key is in ImeProcessedKey
                    actualKey = evt.ImeProcessedKey;
                }

                // Handle Enter key to exit Insert mode (but not if IME is processing)
                // If evt.Key == Key.ImeProcessed, it means IME is handling the key (e.g., confirming conversion)
                // In this case, we should let IME handle it and not switch to Normal mode
                if (actualKey == Key.Enter && evt.Key != Key.ImeProcessed)
                {
                    // Switch to Normal mode
                    tab.VimState.SwitchMode(VimEngine.VimMode.Normal);
                    evt.Handled = true;
                }
                // Handle Escape key to exit Insert mode
                else if (actualKey == Key.Escape)
                {
                    // Switch to Normal mode
                    tab.VimState.SwitchMode(VimEngine.VimMode.Normal);
                    evt.Handled = true;
                }
                // Handle 'jj' sequence (works with IME on/off)
                else if (actualKey == Key.J && Keyboard.Modifiers == ModifierKeys.None)
                {
                    // Check if we already have a 'j' pending
                    if (tab.VimState.PendingKeys.Keys.Count == 1 &&
                        tab.VimState.PendingKeys.Keys[0] == Key.J &&
                        !tab.VimState.PendingKeys.IsExpired(TimeSpan.FromMilliseconds(500)))
                    {
                        // Second 'j' pressed within timeout - switch to normal mode
                        tab.VimState.PendingKeys.Clear();

                        // Remove the first character that was typed (could be 'j' or IME character like 'っ')
                        if (textBox.CaretIndex > 0 && textBox.Text.Length > 0)
                        {
                            int caretIndex = textBox.CaretIndex;
                            textBox.Text = textBox.Text.Remove(caretIndex - 1, 1);
                            textBox.CaretIndex = caretIndex - 1;
                        }

                        tab.VimState.SwitchMode(VimEngine.VimMode.Normal);
                        evt.Handled = true; // Prevent the second 'j' from being typed
                    }
                    else
                    {
                        // First 'j' pressed - add to pending keys
                        tab.VimState.PendingKeys.Clear();
                        tab.VimState.PendingKeys.Add(Key.J);
                        // Let the 'j' be processed normally (either as 'j' or through IME)
                    }
                }
                else
                {
                    // Any other key - clear pending keys
                    if (tab.VimState.PendingKeys.Keys.Count > 0)
                    {
                        tab.VimState.PendingKeys.Clear();
                    }
                }
            };
        }
    }

    private void HandleModeChange(DataGrid grid, TabItemViewModel tab)
    {
        if (grid == null || tab == null)
            return;

        try
        {
            if (tab.VimState.CurrentMode == VimEngine.VimMode.Visual)
            {
                // Visual modeに入る時：DataGridの組み込み選択をクリア
                _isUpdatingSelection = true;
                grid.SelectedCells.Clear();
                _isUpdatingSelection = false;

                // セルのIsSelectedを初期化
                InitializeVisualSelection(tab);
            }
            else if (tab.VimState.CurrentSelection == null)
            {
                // Visual modeから抜ける時：セルのIsSelectedをクリア
                ClearAllCellSelections(tab.Document);
                // Clear header selections as well
                tab.VimState.ClearRowSelections();
                tab.VimState.ClearColumnSelections();

                // DataGridの選択を現在のカーソル位置の1つのセルに設定
                // （Normal modeに戻った時に適切な選択状態にする）
                UpdateDataGridSelection(grid, tab);
            }

            if (tab.VimState.CurrentMode == VimEngine.VimMode.Command)
            {
                // Command mode - commit edits, stay in viewing mode
                grid.CommitEdit(DataGridEditingUnit.Cell, true);
                grid.CommitEdit(DataGridEditingUnit.Row, true);
            }
            else if (tab.VimState.CurrentMode == VimEngine.VimMode.Insert)
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
                // Exit Insert mode - commit the edit and return focus to grid
                grid.CommitEdit(DataGridEditingUnit.Cell, true);
                grid.CommitEdit(DataGridEditingUnit.Row, true);

                // Check if we need to apply bulk edit from Visual mode
                if (tab.VimState.PendingBulkEditRange != null)
                {
                    ApplyBulkEdit(tab);
                }

                // Ensure focus returns to the grid so Normal mode key handling works
                grid.Dispatcher.BeginInvoke(new Action(() =>
                {
                    grid.Focus();
                }), System.Windows.Threading.DispatcherPriority.Input);
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
                // Select the rectangular range of cells
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

            case VimEngine.VisualType.Line:
                // Select entire rows in the range
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

            case VimEngine.VisualType.Block:
                // Select entire columns in the range
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

    private void RowHeader_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var header = sender as System.Windows.Controls.Primitives.DataGridRowHeader;
        if (header == null || _viewModel?.SelectedTab == null)
            return;

        var grid = FindVisualParent<DataGrid>(header);
        if (grid == null)
            return;

        // Get row index from DataGridRow
        var row = FindVisualParent<DataGridRow>(header);
        if (row == null)
            return;

        int rowIndex = row.GetIndex();
        var tab = _viewModel.SelectedTab;

        if (rowIndex < 0 || rowIndex >= tab.Document.RowCount)
            return;

        bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        HandleRowSelection(tab, rowIndex, isCtrlPressed, isShiftPressed);
        e.Handled = true;
    }

    private void ColumnHeader_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var header = sender as System.Windows.Controls.Primitives.DataGridColumnHeader;
        if (header == null || _viewModel?.SelectedTab == null)
            return;

        var grid = FindVisualParent<DataGrid>(header);
        if (grid == null)
            return;

        int columnIndex = header.Column.DisplayIndex;
        var tab = _viewModel.SelectedTab;

        if (columnIndex < 0 || columnIndex >= tab.Document.ColumnCount)
            return;

        bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

        HandleColumnSelection(tab, columnIndex, isCtrlPressed, isShiftPressed);
        e.Handled = true;
    }

    private void HandleRowSelection(ViewModels.TabItemViewModel tab, int rowIndex, bool isCtrlPressed,
        bool isShiftPressed)
    {
        // If not in Visual Line mode, enter it
        if (tab.VimState.CurrentMode != VimEngine.VimMode.Visual ||
            tab.VimState.CurrentSelection?.Type != VimEngine.VisualType.Line)
        {
            // Clear any column selections
            tab.VimState.ClearColumnSelections();

            // Set single row selection
            tab.VimState.SetSingleRowSelection(rowIndex);

            // Move cursor to first cell of row
            tab.VimState.CursorPosition = new Models.GridPosition(rowIndex, 0);

            // Enter Visual Line mode
            tab.VimState.CurrentSelection = new VimEngine.SelectionRange(
                VimEngine.VisualType.Line,
                new Models.GridPosition(rowIndex, 0),
                new Models.GridPosition(rowIndex, tab.Document.ColumnCount - 1));

            tab.VimState.SwitchMode(VimEngine.VimMode.Visual);
        }
        else if (isShiftPressed)
        {
            // Range selection from last selected row to current row
            tab.VimState.SetRowRangeSelection(rowIndex);
        }
        else if (isCtrlPressed)
        {
            // Toggle this row in multi-selection
            tab.VimState.ToggleRowSelection(rowIndex);

            // If no rows selected, exit Visual mode
            if (tab.VimState.SelectedRows.Count == 0)
            {
                tab.VimState.SwitchMode(VimEngine.VimMode.Normal);
            }
        }
        else
        {
            // Single click without Ctrl/Shift - replace selection
            tab.VimState.SetSingleRowSelection(rowIndex);
            tab.VimState.CursorPosition = new Models.GridPosition(rowIndex, 0);
        }

        // Update cell highlighting
        UpdateHeaderSelectionHighlighting(tab);
    }

    private void HandleColumnSelection(ViewModels.TabItemViewModel tab, int columnIndex, bool isCtrlPressed,
        bool isShiftPressed)
    {
        // If not in Visual Block mode, enter it
        if (tab.VimState.CurrentMode != VimEngine.VimMode.Visual ||
            tab.VimState.CurrentSelection?.Type != VimEngine.VisualType.Block)
        {
            // Clear any row selections
            tab.VimState.ClearRowSelections();

            // Set single column selection
            tab.VimState.SetSingleColumnSelection(columnIndex);

            // Move cursor to first row of column
            tab.VimState.CursorPosition = new Models.GridPosition(0, columnIndex);

            // Enter Visual Block mode
            tab.VimState.CurrentSelection = new VimEngine.SelectionRange(
                VimEngine.VisualType.Block,
                new Models.GridPosition(0, columnIndex),
                new Models.GridPosition(tab.Document.RowCount - 1, columnIndex));

            tab.VimState.SwitchMode(VimEngine.VimMode.Visual);
        }
        else if (isShiftPressed)
        {
            // Range selection from last selected column to current column
            tab.VimState.SetColumnRangeSelection(columnIndex);
        }
        else if (isCtrlPressed)
        {
            // Toggle this column in multi-selection
            tab.VimState.ToggleColumnSelection(columnIndex);

            // If no columns selected, exit Visual mode
            if (tab.VimState.SelectedColumns.Count == 0)
            {
                tab.VimState.SwitchMode(VimEngine.VimMode.Normal);
            }
        }
        else
        {
            // Single click without Ctrl/Shift - replace selection
            tab.VimState.SetSingleColumnSelection(columnIndex);
            tab.VimState.CursorPosition = new Models.GridPosition(0, columnIndex);
        }

        // Update cell highlighting
        UpdateHeaderSelectionHighlighting(tab);
    }

    private void UpdateHeaderSelectionHighlighting(ViewModels.TabItemViewModel tab)
    {
        // Clear all cell selections first
        foreach (var row in tab.Document.Rows)
        {
            foreach (var cell in row.Cells)
            {
                cell.IsSelected = false;
            }
        }

        // Highlight cells based on selected rows/columns
        if (tab.VimState.SelectedRows.Count > 0)
        {
            // Visual Line mode - highlight all rows
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
            // Visual Block mode - highlight all columns
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

    private T? FindVisualParent<T>(System.Windows.DependencyObject child) where T : System.Windows.DependencyObject
    {
        while (child != null)
        {
            child = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (child is T parent)
                return parent;
        }

        return null;
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_viewModel?.SelectedTab == null)
            return;

        // Don't handle keys if a TextBox has focus (for file rename or FindReplace panel)
        // This must be checked BEFORE Ctrl+F handling to prevent conflicts
        if (Keyboard.FocusedElement is System.Windows.Controls.TextBox textBox)
        {
            // Allow Ctrl+F even when TextBox has focus (to open/focus FindReplace panel)
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
            {
                var findReplaceVM = _viewModel.SelectedTab.FindReplaceViewModel;
                if (findReplaceVM != null)
                {
                    // Clear Vim search when opening FindReplace
                    _viewModel.SelectedTab.VimState.ClearSearch();

                    // Open FindReplace panel
                    findReplaceVM.Open();

                    e.Handled = true;
                    return;
                }
            }

            // For all other keys, let TextBox handle them normally
            return;
        }

        // Handle Ctrl+F for Find/Replace panel (works regardless of Vim mode)
        if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            var findReplaceVM = _viewModel.SelectedTab.FindReplaceViewModel;
            if (findReplaceVM != null)
            {
                // Clear Vim search when opening FindReplace
                _viewModel.SelectedTab.VimState.ClearSearch();

                // Open FindReplace panel
                findReplaceVM.Open();

                e.Handled = true;
                return;
            }
        }

        // Handle Ctrl+G for Git History (works regardless of Vim mode)
        if (e.Key == Key.G && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (_viewModel.ViewGitHistoryCommand.CanExecute(null))
            {
                _viewModel.ViewGitHistoryCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }

        // Handle Ctrl+S for Save File (works regardless of Vim mode)
        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (_viewModel.SaveFileCommand.CanExecute(null))
            {
                _viewModel.SaveFileCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }

        // Handle Ctrl+Shift+E for Select in Folder Tree (works regardless of Vim mode)
        if (e.Key == Key.E && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            SelectCurrentFileInFolderTree();
            e.Handled = true;
            return;
        }

        // If Vim mode is disabled, let DataGrid handle keys normally
        if (!_viewModel.IsVimModeEnabled)
            return;

        var tab = _viewModel.SelectedTab;
        var currentMode = tab.VimState.CurrentMode;

        // Get the actual key - handle IME processed keys
        Key actualKey = e.Key;
        if (e.Key == Key.ImeProcessed)
        {
            // When IME is on, the actual key is in ImeProcessedKey
            actualKey = e.ImeProcessedKey;
        }

        // Debug: Output key information to help identify the correct key
        System.Diagnostics.Debug.WriteLine($"Key: {e.Key}, ImeProcessedKey: {e.ImeProcessedKey}, ActualKey: {actualKey}, Modifiers: {Keyboard.Modifiers}");

        // Special handling for ':' key in Normal mode to ensure it enters Command mode
        // before DataGrid tries to start editing
        // Key.Oem1 is ':' on Japanese keyboard and ';' on US keyboard
        // Accept both with/without Shift to support both keyboard layouts
        if (currentMode == VimEngine.VimMode.Normal && actualKey == Key.Oem1)
        {
            tab.VimState.CurrentCommandType = VimEngine.CommandType.ExCommand;
            tab.VimState.SwitchMode(VimEngine.VimMode.Command);
            e.Handled = true;
            return;
        }

        // Special handling for '/' key in Normal mode to ensure it enters Search mode
        // before DataGrid tries to start editing
        if (currentMode == VimEngine.VimMode.Normal &&
            actualKey == Key.OemQuestion &&
            !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            tab.VimState.CurrentCommandType = VimEngine.CommandType.Search;
            tab.VimState.SwitchMode(VimEngine.VimMode.Command);
            e.Handled = true;
            return;
        }

        // Handle key through Vim state of the selected tab
        var handled = tab.VimState.HandleKey(
            actualKey,
            Keyboard.Modifiers,
            tab.GridViewModel.Document);

        if (handled)
        {
            e.Handled = true;
        }
    }

    private async void FolderTreeView_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (FolderTreeView.SelectedItem is TreeViewItem item)
        {
            var itemPath = item.Tag as string;
            if (!string.IsNullOrEmpty(itemPath))
            {
                if (e.Key == Key.Enter)
                {
                    if (File.Exists(itemPath))
                    {
                        // Open file
                        await _viewModel!.OpenFileAsync(itemPath);
                        e.Handled = true;
                    }
                    else if (Directory.Exists(itemPath))
                    {
                        // Expand/collapse folder
                        item.IsExpanded = !item.IsExpanded;
                        e.Handled = true;
                    }
                }
                else if (e.Key == Key.F2)
                {
                    if (File.Exists(itemPath))
                    {
                        BeginRenameTreeItem(item, false);
                        e.Handled = true;
                    }
                    else if (Directory.Exists(itemPath))
                    {
                        BeginRenameTreeItem(item, true);
                        e.Handled = true;
                    }
                }
            }
        }
    }

    private void RenameMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Get the TreeViewItem from the MenuItem's parent ContextMenu
        if (sender is MenuItem menuItem &&
            menuItem.Parent is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is TreeViewItem item)
        {
            var filePath = item.Tag as string;
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                BeginRenameTreeItem(item, false);
            }
        }
    }

    private void RenameFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Get the TreeViewItem from the MenuItem's parent ContextMenu
        if (sender is MenuItem menuItem &&
            menuItem.Parent is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is TreeViewItem item)
        {
            var folderPath = item.Tag as string;
            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                BeginRenameTreeItem(item, true);
            }
        }
    }

    private void NewFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Get the TreeViewItem from the MenuItem's parent ContextMenu
        if (sender is MenuItem menuItem &&
            menuItem.Parent is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is TreeViewItem item)
        {
            var folderPath = item.Tag as string;
            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                CreateNewFile(item, folderPath);
            }
        }
    }

    private void NewFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Get the TreeViewItem from the MenuItem's parent ContextMenu
        if (sender is MenuItem menuItem &&
            menuItem.Parent is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is TreeViewItem item)
        {
            var folderPath = item.Tag as string;
            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                CreateNewFolder(item, folderPath);
            }
        }
    }

    private void DeleteFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Get the TreeViewItem from the MenuItem's parent ContextMenu
        if (sender is MenuItem menuItem &&
            menuItem.Parent is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is TreeViewItem item)
        {
            var filePath = item.Tag as string;
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                var fileName = Path.GetFileName(filePath);
                var result = System.Windows.MessageBox.Show(
                    $"ファイル '{fileName}' を削除してもよろしいですか？",
                    "ファイルの削除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Close the file if it's open in a tab
                        var openTab = _viewModel?.Tabs.FirstOrDefault(t => t.FilePath == filePath);
                        if (openTab != null)
                        {
                            _viewModel?.CloseTab(openTab);
                        }

                        // Delete the file
                        File.Delete(filePath);

                        // Remove from tree view
                        if (item.Parent is ItemsControl parent)
                        {
                            parent.Items.Remove(item);
                        }

                        _viewModel?.StatusBarViewModel.ShowMessage($"ファイル '{fileName}' を削除しました。");
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(
                            $"ファイルの削除に失敗しました: {ex.Message}",
                            "エラー",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
        }
    }

    private void DeleteFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Get the TreeViewItem from the MenuItem's parent ContextMenu
        if (sender is MenuItem menuItem &&
            menuItem.Parent is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is TreeViewItem item)
        {
            var folderPath = item.Tag as string;
            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                var folderName = new DirectoryInfo(folderPath).Name;
                var result = System.Windows.MessageBox.Show(
                    $"フォルダ '{folderName}' とその中身を削除してもよろしいですか？\n\nこの操作は元に戻せません。",
                    "フォルダの削除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Close any files from this folder that are open in tabs
                        if (_viewModel?.Tabs != null)
                        {
                            var tabsToClose = _viewModel.Tabs
                                .Where(t => !string.IsNullOrEmpty(t.FilePath) &&
                                       t.FilePath.StartsWith(folderPath + Path.DirectorySeparatorChar))
                                .ToList();

                            foreach (var tab in tabsToClose)
                            {
                                _viewModel.CloseTab(tab);
                            }
                        }

                        // Delete the folder and all its contents
                        Directory.Delete(folderPath, true);

                        // Remove from tree view
                        if (item.Parent is ItemsControl parent)
                        {
                            parent.Items.Remove(item);
                        }

                        _viewModel?.StatusBarViewModel.ShowMessage($"フォルダ '{folderName}' を削除しました。");
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show(
                            $"フォルダの削除に失敗しました: {ex.Message}",
                            "エラー",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
            }
        }
    }

    private void OpenFileInExplorerMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Get the TreeViewItem from the MenuItem's parent ContextMenu
        if (sender is MenuItem menuItem &&
            menuItem.Parent is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is TreeViewItem item)
        {
            var filePath = item.Tag as string;
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                try
                {
                    // Open Windows Explorer and select the file
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"エクスプローラーでの表示に失敗しました: {ex.Message}",
                        "エラー",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
    }

    private void OpenFolderInExplorerMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Get the TreeViewItem from the MenuItem's parent ContextMenu
        if (sender is MenuItem menuItem &&
            menuItem.Parent is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is TreeViewItem item)
        {
            var folderPath = item.Tag as string;
            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                try
                {
                    // Open Windows Explorer to the folder
                    System.Diagnostics.Process.Start("explorer.exe", $"\"{folderPath}\"");
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        $"エクスプローラーでの表示に失敗しました: {ex.Message}",
                        "エラー",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
    }

    private void BeginRenameTreeItem(TreeViewItem item, bool isFolder)
    {
        var itemPath = item.Tag as string;
        if (string.IsNullOrEmpty(itemPath))
            return;

        var itemName = isFolder ? new DirectoryInfo(itemPath).Name : Path.GetFileName(itemPath);
        var itemNameWithoutExt = isFolder ? itemName : Path.GetFileNameWithoutExtension(itemPath);

        // Flag to prevent double processing
        bool isProcessed = false;

        // Create a TextBox for editing
        var textBox = new System.Windows.Controls.TextBox
        {
            Text = itemName,
            Margin = new Thickness(0),
            Padding = new Thickness(2),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Colors.CornflowerBlue),
            Tag = itemPath, // Store original path
            Focusable = true
        };

        // Handle Enter key to commit rename
        textBox.KeyDown += (s, e) =>
        {
            if (isProcessed)
                return;

            if (e.Key == Key.Enter)
            {
                isProcessed = true;
                var newName = textBox.Text.Trim();
                if (!string.IsNullOrEmpty(newName) && newName != itemName)
                {
                    if (isFolder)
                        RenameFolder(itemPath, newName, item);
                    else
                        RenameFile(itemPath, newName, item);
                }
                else
                {
                    // Restore original header with highlighting
                    var filterText = _viewModel?.FilterText ?? string.Empty;
                    item.Header = CreateHeaderWithIcon(itemName, filterText, isFolder);
                }

                // Return focus to TreeView
                FolderTreeView.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                isProcessed = true;
                // Cancel rename - restore original header with highlighting
                var filterText = _viewModel?.FilterText ?? string.Empty;
                item.Header = CreateHighlightedHeader(itemName, filterText);
                // Return focus to TreeView
                FolderTreeView.Focus();
                e.Handled = true;
            }
        };

        // Handle lost focus to commit or cancel rename
        textBox.LostFocus += (s, e) =>
        {
            if (isProcessed)
                return;

            isProcessed = true;
            var newName = textBox.Text.Trim();
            if (!string.IsNullOrEmpty(newName) && newName != itemName)
            {
                if (isFolder)
                    RenameFolder(itemPath, newName, item);
                else
                    RenameFile(itemPath, newName, item);
            }
            else
            {
                // Restore original header with highlighting
                var filterText = _viewModel?.FilterText ?? string.Empty;
                item.Header = CreateHighlightedHeader(itemName, filterText);
            }
        };

        // Replace header with TextBox
        item.Header = textBox;

        // Focus the TextBox and select item name without extension
        Dispatcher.BeginInvoke(new Action(() =>
        {
            textBox.Focus();
            Keyboard.Focus(textBox);
            // Select item name without extension
            textBox.Select(0, itemNameWithoutExt.Length);
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void RenameFile(string oldFilePath, string newFileName, TreeViewItem item)
    {
        try
        {
            var directory = Path.GetDirectoryName(oldFilePath);
            if (string.IsNullOrEmpty(directory))
                return;

            var newFilePath = Path.Combine(directory, newFileName);

            // Check if file already exists
            if (File.Exists(newFilePath) && newFilePath != oldFilePath)
            {
                System.Windows.MessageBox.Show(
                    $"ファイル '{newFileName}' は既に存在します。",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                var filterText = _viewModel?.FilterText ?? string.Empty;
                item.Header = CreateHeaderWithIcon(Path.GetFileName(oldFilePath), filterText, false);
                return;
            }

            // Rename the file
            File.Move(oldFilePath, newFilePath);

            // Update TreeViewItem with highlighting
            var currentFilterText = _viewModel?.FilterText ?? string.Empty;
            item.Header = CreateHeaderWithIcon(newFileName, currentFilterText, false);
            item.Tag = newFilePath;

            // Update any open tabs that reference this file
            UpdateOpenTabsForRename(oldFilePath, newFilePath);

            _viewModel?.StatusBarViewModel.ShowMessage($"Renamed: {newFileName}");

            // Return focus to TreeView
            FolderTreeView.Focus();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"ファイル名の変更に失敗しました: {ex.Message}",
                "エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            var filterText = _viewModel?.FilterText ?? string.Empty;
            item.Header = CreateHeaderWithIcon(Path.GetFileName(oldFilePath), filterText, false);

            // Return focus to TreeView
            FolderTreeView.Focus();
        }
    }

    private void RenameFolder(string oldFolderPath, string newFolderName, TreeViewItem item)
    {
        try
        {
            var parentDirectory = Path.GetDirectoryName(oldFolderPath);
            if (string.IsNullOrEmpty(parentDirectory))
                return;

            var newFolderPath = Path.Combine(parentDirectory, newFolderName);

            // Check if folder already exists
            if (Directory.Exists(newFolderPath) && newFolderPath != oldFolderPath)
            {
                System.Windows.MessageBox.Show(
                    $"フォルダ '{newFolderName}' は既に存在します。",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                var filterText = _viewModel?.FilterText ?? string.Empty;
                item.Header = CreateHeaderWithIcon(new DirectoryInfo(oldFolderPath).Name, filterText, true);
                return;
            }

            // Rename the folder
            Directory.Move(oldFolderPath, newFolderPath);

            // Update TreeViewItem with highlighting
            var currentFilterText = _viewModel?.FilterText ?? string.Empty;
            item.Header = CreateHeaderWithIcon(newFolderName, currentFilterText, true);
            item.Tag = newFolderPath;

            // Update any open tabs that reference files in this folder
            UpdateOpenTabsForFolderRename(oldFolderPath, newFolderPath);

            _viewModel?.StatusBarViewModel.ShowMessage($"Renamed folder: {newFolderName}");

            // Return focus to TreeView
            FolderTreeView.Focus();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"フォルダ名の変更に失敗しました: {ex.Message}",
                "エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            var filterText = _viewModel?.FilterText ?? string.Empty;
            item.Header = CreateHeaderWithIcon(new DirectoryInfo(oldFolderPath).Name, filterText, true);

            // Return focus to TreeView
            FolderTreeView.Focus();
        }
    }

    private void UpdateOpenTabsForRename(string oldFilePath, string newFilePath)
    {
        if (_viewModel == null)
            return;

        foreach (var tab in _viewModel.Tabs)
        {
            if (tab.FilePath == oldFilePath)
            {
                tab.FilePath = newFilePath;
                var newFileName = Path.GetFileName(newFilePath);
                tab.Header = tab.IsDirty ? $"{newFileName}*" : newFileName;
            }
        }
    }

    private void UpdateOpenTabsForFolderRename(string oldFolderPath, string newFolderPath)
    {
        if (_viewModel == null)
            return;

        foreach (var tab in _viewModel.Tabs)
        {
            if (!string.IsNullOrEmpty(tab.FilePath) && tab.FilePath.StartsWith(oldFolderPath))
            {
                // Update file path to reflect new folder path
                tab.FilePath = tab.FilePath.Replace(oldFolderPath, newFolderPath);
                var newFileName = Path.GetFileName(tab.FilePath);
                tab.Header = tab.IsDirty ? $"{newFileName}*" : newFileName;
            }
        }
    }

    private void CreateNewFile(TreeViewItem parentItem, string folderPath)
    {
        try
        {
            // Generate a unique file name
            string newFileName = "NewFile.tsv";
            string newFilePath = Path.Combine(folderPath, newFileName);
            int counter = 1;

            while (File.Exists(newFilePath))
            {
                newFileName = $"NewFile{counter}.tsv";
                newFilePath = Path.Combine(folderPath, newFileName);
                counter++;
            }

            // Create the file
            File.WriteAllText(newFilePath, string.Empty);

            // Expand the parent node if not already expanded
            if (!parentItem.IsExpanded)
            {
                parentItem.IsExpanded = true;
            }

            // Refresh the tree node to show the new file
            RefreshTreeNode(parentItem);

            // Find the newly created file item and start rename
            var newFileItem = FindTreeItemByPath(parentItem, newFilePath);
            if (newFileItem != null)
            {
                newFileItem.IsSelected = true;
                BeginRenameTreeItem(newFileItem, false);
            }

            _viewModel?.StatusBarViewModel.ShowMessage($"Created: {newFileName}");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"ファイルの作成に失敗しました: {ex.Message}",
                "エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CreateNewFolder(TreeViewItem parentItem, string folderPath)
    {
        try
        {
            // Generate a unique folder name
            string newFolderName = "NewFolder";
            string newFolderPath = Path.Combine(folderPath, newFolderName);
            int counter = 1;

            while (Directory.Exists(newFolderPath))
            {
                newFolderName = $"NewFolder{counter}";
                newFolderPath = Path.Combine(folderPath, newFolderName);
                counter++;
            }

            // Create the folder
            Directory.CreateDirectory(newFolderPath);

            // Expand the parent node if not already expanded
            if (!parentItem.IsExpanded)
            {
                parentItem.IsExpanded = true;
            }

            // Refresh the tree node to show the new folder
            RefreshTreeNode(parentItem);

            // Find the newly created folder item and start rename
            var newFolderItem = FindTreeItemByPath(parentItem, newFolderPath);
            if (newFolderItem != null)
            {
                newFolderItem.IsSelected = true;
                BeginRenameTreeItem(newFolderItem, true);
            }

            _viewModel?.StatusBarViewModel.ShowMessage($"Created folder: {newFolderName}");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"フォルダの作成に失敗しました: {ex.Message}",
                "エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void RefreshTreeNode(TreeViewItem node)
    {
        var path = node.Tag as string;
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            return;

        // Save expansion state of child items
        var expandedPaths = new HashSet<string>();
        SaveExpansionState(node, expandedPaths);

        // Clear current items
        node.Items.Clear();

        // Repopulate the node
        PopulateTreeNode(node, path);

        // Restore expansion state
        RestoreExpansionState(node, expandedPaths);
    }

    private void SaveExpansionState(TreeViewItem node, HashSet<string> expandedPaths)
    {
        foreach (var item in node.Items)
        {
            if (item is TreeViewItem childItem)
            {
                var childPath = childItem.Tag as string;
                if (!string.IsNullOrEmpty(childPath) && childItem.IsExpanded)
                {
                    expandedPaths.Add(childPath);
                    SaveExpansionState(childItem, expandedPaths);
                }
            }
        }
    }

    private void RestoreExpansionState(TreeViewItem node, HashSet<string> expandedPaths)
    {
        foreach (var item in node.Items)
        {
            if (item is TreeViewItem childItem)
            {
                var childPath = childItem.Tag as string;
                if (!string.IsNullOrEmpty(childPath) && expandedPaths.Contains(childPath))
                {
                    childItem.IsExpanded = true;
                    RestoreExpansionState(childItem, expandedPaths);
                }
            }
        }
    }

    private TreeViewItem? FindTreeItemByPath(TreeViewItem parent, string path)
    {
        foreach (var item in parent.Items)
        {
            if (item is TreeViewItem treeItem)
            {
                if (treeItem.Tag is string itemPath && itemPath == path)
                {
                    return treeItem;
                }
            }
        }
        return null;
    }

    private void FolderTreeView_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Check if we clicked on the TreeView background (not on an item)
        var clickedElement = e.OriginalSource as DependencyObject;

        // Walk up the visual tree to see if we clicked on a TreeViewItem
        var treeViewItem = FindVisualParent<TreeViewItem>(clickedElement);

        if (treeViewItem == null && _viewModel?.SelectedFolderPath != null)
        {
            // Clicked on background - show context menu for root folder
            var contextMenu = new ContextMenu();

            var newFileMenuItem = new MenuItem { Header = "新しいファイル(_F)" };
            newFileMenuItem.Click += (s, args) =>
            {
                // Create file in root folder
                if (FolderTreeView.Items.Count > 0 && FolderTreeView.Items[0] is TreeViewItem rootItem)
                {
                    CreateNewFile(rootItem, _viewModel.SelectedFolderPath);
                }
            };
            contextMenu.Items.Add(newFileMenuItem);

            var newFolderMenuItem = new MenuItem { Header = "新しいフォルダ(_N)" };
            newFolderMenuItem.Click += (s, args) =>
            {
                // Create folder in root folder
                if (FolderTreeView.Items.Count > 0 && FolderTreeView.Items[0] is TreeViewItem rootItem)
                {
                    CreateNewFolder(rootItem, _viewModel.SelectedFolderPath);
                }
            };
            contextMenu.Items.Add(newFolderMenuItem);

            contextMenu.PlacementTarget = FolderTreeView;
            contextMenu.IsOpen = true;
            e.Handled = true;
        }
    }

    private void TreeViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TreeViewItem item)
        {
            _dragStartPoint = e.GetPosition(null);
            _draggedItem = item;
        }
    }

    private void TreeViewItem_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null)
        {
            System.Windows.Point currentPosition = e.GetPosition(null);
            System.Windows.Vector diff = _dragStartPoint - currentPosition;

            // Check if we've moved far enough to start a drag operation
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var itemPath = _draggedItem.Tag as string;
                if (!string.IsNullOrEmpty(itemPath))
                {
                    // Create drag data
                    var dragData = new System.Windows.DataObject("TreeViewItem", _draggedItem);
                    dragData.SetData(System.Windows.DataFormats.FileDrop, new[] { itemPath });

                    // Start drag-and-drop operation
                    DragDrop.DoDragDrop(_draggedItem, dragData, System.Windows.DragDropEffects.Move);

                    // Reset drag state
                    _draggedItem = null;
                }
            }
        }
    }

    private void TreeViewItem_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is TreeViewItem targetItem)
        {
            var targetPath = targetItem.Tag as string;

            // Only allow drop on folders
            if (!string.IsNullOrEmpty(targetPath) && Directory.Exists(targetPath))
            {
                // Check if we're dragging a TreeViewItem
                if (e.Data.GetDataPresent("TreeViewItem"))
                {
                    var draggedItem = e.Data.GetData("TreeViewItem") as TreeViewItem;
                    var draggedPath = draggedItem?.Tag as string;

                    // Don't allow dropping on itself or its descendants
                    if (!string.IsNullOrEmpty(draggedPath) && draggedPath != targetPath &&
                        !targetPath.StartsWith(draggedPath + Path.DirectorySeparatorChar))
                    {
                        e.Effects = System.Windows.DragDropEffects.Move;

                        // Visual feedback - highlight the drop target
                        targetItem.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 0, 120, 215));
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        e.Effects = System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void TreeViewItem_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is TreeViewItem item)
        {
            // Remove visual feedback
            item.Background = System.Windows.Media.Brushes.Transparent;
        }
    }

    private void TreeViewItem_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is TreeViewItem targetItem)
        {
            // Remove visual feedback
            targetItem.Background = System.Windows.Media.Brushes.Transparent;

            var targetPath = targetItem.Tag as string;

            if (!string.IsNullOrEmpty(targetPath) && Directory.Exists(targetPath))
            {
                if (e.Data.GetDataPresent("TreeViewItem"))
                {
                    var draggedItem = e.Data.GetData("TreeViewItem") as TreeViewItem;
                    var sourcePath = draggedItem?.Tag as string;

                    if (!string.IsNullOrEmpty(sourcePath) && sourcePath != targetPath)
                    {
                        MoveItemToFolder(sourcePath, targetPath, draggedItem, targetItem);
                    }
                }
            }
        }
    }

    private void MoveItemToFolder(string sourcePath, string targetFolderPath, TreeViewItem? sourceItem, TreeViewItem targetFolderItem)
    {
        try
        {
            bool isFile = File.Exists(sourcePath);
            bool isFolder = Directory.Exists(sourcePath);

            if (!isFile && !isFolder)
            {
                return;
            }

            string itemName = isFile ? Path.GetFileName(sourcePath) : new DirectoryInfo(sourcePath).Name;
            string newPath = Path.Combine(targetFolderPath, itemName);

            // Check if source and target are in the same folder
            string? sourceParentPath = isFile ? Path.GetDirectoryName(sourcePath) : Directory.GetParent(sourcePath)?.FullName;
            if (sourceParentPath != null && Path.GetFullPath(sourceParentPath) == Path.GetFullPath(targetFolderPath))
            {
                // Same folder - no need to move
                return;
            }

            // Check if target already exists
            if ((isFile && File.Exists(newPath)) || (isFolder && Directory.Exists(newPath)))
            {
                var result = System.Windows.MessageBox.Show(
                    $"'{itemName}' は既に移動先に存在します。上書きしますか？",
                    "確認",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result != MessageBoxResult.Yes)
                    return;

                // Delete existing target
                if (isFile)
                    File.Delete(newPath);
                else
                    Directory.Delete(newPath, true);
            }

            // Perform the move
            if (isFile)
            {
                File.Move(sourcePath, newPath);
            }
            else
            {
                Directory.Move(sourcePath, newPath);
            }

            // Update any open tabs that reference this file or files in this folder
            if (isFile)
            {
                UpdateOpenTabsForRename(sourcePath, newPath);
            }
            else
            {
                UpdateOpenTabsForFolderRename(sourcePath, newPath);
            }

            // Refresh only the affected folders to preserve tree expansion state
            // Find and refresh the source parent folder (sourceParentPath already calculated above)
            if (!string.IsNullOrEmpty(sourceParentPath))
            {
                var sourceParentItem = FindTreeViewItemByPathRecursive(FolderTreeView, sourceParentPath);
                if (sourceParentItem != null)
                {
                    RefreshTreeNode(sourceParentItem);
                }
            }

            // Expand and refresh the target folder
            if (!targetFolderItem.IsExpanded)
            {
                targetFolderItem.IsExpanded = true;
            }
            RefreshTreeNode(targetFolderItem);

            _viewModel?.StatusBarViewModel.ShowMessage($"Moved: {itemName} to {Path.GetFileName(targetFolderPath)}");
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"移動に失敗しました: {ex.Message}",
                "エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private TreeViewItem? FindTreeViewItemByPathRecursive(ItemsControl parent, string targetPath)
    {
        foreach (var item in parent.Items)
        {
            if (item is TreeViewItem treeItem)
            {
                var itemPath = treeItem.Tag as string;
                if (!string.IsNullOrEmpty(itemPath) && Path.GetFullPath(itemPath) == Path.GetFullPath(targetPath))
                {
                    return treeItem;
                }

                // Search recursively in children
                var result = FindTreeViewItemByPathRecursive(treeItem, targetPath);
                if (result != null)
                {
                    return result;
                }
            }
        }
        return null;
    }

    // Store event handlers for each DataGrid to allow proper cleanup
    private readonly Dictionary<DataGrid, (TabItemViewModel tab, PropertyChangedEventHandler vimStateHandler, PropertyChangedEventHandler documentHandler)> _dataGridHandlers
        = new Dictionary<DataGrid, (TabItemViewModel, PropertyChangedEventHandler, PropertyChangedEventHandler)>();

    private void TsvGrid_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        var dataGrid = sender as DataGrid;
        if (dataGrid == null)
            return;

        var newTab = e.NewValue as TabItemViewModel;
        var gridHashCode = dataGrid.GetHashCode();

        System.Diagnostics.Debug.WriteLine($"[DataContextChanged] Grid:{gridHashCode}, OldTab:{(e.OldValue as TabItemViewModel)?.Header}, NewTab:{newTab?.Header}");

        // Check if handlers are already registered for this DataGrid and TabItemViewModel combination
        if (_dataGridHandlers.TryGetValue(dataGrid, out var existingInfo))
        {
            // If the new tab is the same as the existing one, skip (no change needed)
            if (existingInfo.tab == newTab)
            {
                System.Diagnostics.Debug.WriteLine($"[DataContextChanged] Grid:{gridHashCode}, Same tab, skipping");
                return; // Already registered, skip
            }

            // Different tab, remove old handlers first
            System.Diagnostics.Debug.WriteLine($"[DataContextChanged] Grid:{gridHashCode}, Removing old handlers for {existingInfo.tab.Header}");
            existingInfo.tab.VimState.PropertyChanged -= existingInfo.vimStateHandler;
            existingInfo.tab.Document.PropertyChanged -= existingInfo.documentHandler;
            _dataGridHandlers.Remove(dataGrid);
        }

        // Add new event handlers only if new value is a TabItemViewModel
        if (newTab != null)
        {
            System.Diagnostics.Debug.WriteLine($"[DataContextChanged] Grid:{gridHashCode}, Adding new handlers for {newTab.Header}");

            // Create event handlers
            PropertyChangedEventHandler vimStateHandler = (s, evt) =>
            {
                if (evt.PropertyName == nameof(newTab.VimState.CursorPosition) &&
                    newTab == _viewModel?.SelectedTab)
                {
                    System.Diagnostics.Debug.WriteLine($"[VimState.CursorPosition] Tab:{newTab.Header}, Pos:({newTab.VimState.CursorPosition.Row},{newTab.VimState.CursorPosition.Column})");
                    UpdateDataGridSelection(dataGrid, newTab);
                }
                else if (evt.PropertyName == nameof(newTab.VimState.CurrentMode) &&
                         newTab == _viewModel?.SelectedTab)
                {
                    HandleModeChange(dataGrid, newTab);
                }
                else if (evt.PropertyName == nameof(newTab.VimState.CurrentSelection) &&
                         newTab == _viewModel?.SelectedTab &&
                         newTab.VimState.CurrentMode == VimEngine.VimMode.Visual)
                {
                    // Visual mode時にCurrentSelectionが変更された場合、選択範囲を再初期化
                    // （これはhjklでの移動時に発生する）
                    InitializeVisualSelection(newTab);
                }
            };

            PropertyChangedEventHandler documentHandler = (s, evt) =>
            {
                if (evt.PropertyName == nameof(TsvDocument.ColumnCount) &&
                    newTab == _viewModel?.SelectedTab)
                {
                    GenerateColumns(dataGrid, newTab);
                }
            };

            // Subscribe to events
            newTab.VimState.PropertyChanged += vimStateHandler;
            newTab.Document.PropertyChanged += documentHandler;

            // Store handlers and tab reference for cleanup
            _dataGridHandlers[dataGrid] = (newTab, vimStateHandler, documentHandler);
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Focus();

        // Set up clipboard monitoring
        var windowHelper = new WindowInteropHelper(this);
        _hwndSource = HwndSource.FromHwnd(windowHelper.Handle);
        if (_hwndSource != null)
        {
            _hwndSource.AddHook(WndProc);
            AddClipboardFormatListener(windowHelper.Handle);
        }

        // Restore session asynchronously on background thread
        if (_viewModel != null)
        {
            await _viewModel.RestoreSessionAsync();
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Handle clipboard update notification
        if (msg == WM_CLIPBOARDUPDATE)
        {
            OnClipboardChanged();
        }
        return IntPtr.Zero;
    }

    private void OnClipboardChanged()
    {
        // Debug: Check what's in the clipboard
        string? clipboardContent = null;
        try
        {
            if (System.Windows.Clipboard.ContainsText())
            {
                clipboardContent = System.Windows.Clipboard.GetText();
            }
        }
        catch { }

        string contentPreview = clipboardContent != null && clipboardContent.Length > 100
            ? clipboardContent.Substring(0, 100) + "..."
            : clipboardContent ?? "";

        System.Diagnostics.Debug.WriteLine($"[ClipboardMonitor] Clipboard changed. Content length: {clipboardContent?.Length ?? 0}");
        System.Diagnostics.Debug.WriteLine($"[ClipboardMonitor]   Content preview: [{contentPreview}]");

        // Check if clipboard was changed by an external application
        bool hasChanged = ClipboardHelper.HasClipboardChangedExternally();
        System.Diagnostics.Debug.WriteLine($"[ClipboardMonitor] HasClipboardChangedExternally: {hasChanged}");

        if (hasChanged)
        {
            System.Diagnostics.Debug.WriteLine($"[ClipboardMonitor] Clearing LastYank in all tabs");
            // Clear LastYank in ALL tabs' VimState (not just the selected tab)
            if (_viewModel?.Tabs != null)
            {
                foreach (var tab in _viewModel.Tabs)
                {
                    if (tab.VimState != null)
                    {
                        tab.VimState.LastYank = null;
                    }
                }
            }
        }
    }

    private void RowHeader_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var header = sender as System.Windows.Controls.Primitives.DataGridRowHeader;
        if (header == null || _viewModel == null)
            return;

        // Get row index from DataGridRow
        var row = FindVisualParent<DataGridRow>(header);
        if (row == null)
            return;

        int rowIndex = row.GetIndex();

        // Create context menu
        var contextMenu = new ContextMenu();

        var insertAboveMenuItem = new MenuItem { Header = "Insert Row Above" };
        insertAboveMenuItem.Click += (s, args) => _viewModel.InsertRowAboveCommand.Execute(rowIndex);
        contextMenu.Items.Add(insertAboveMenuItem);

        var insertBelowMenuItem = new MenuItem { Header = "Insert Row Below" };
        insertBelowMenuItem.Click += (s, args) => _viewModel.InsertRowBelowCommand.Execute(rowIndex);
        contextMenu.Items.Add(insertBelowMenuItem);

        header.ContextMenu = contextMenu;
        contextMenu.IsOpen = true;
    }

    private void ColumnHeader_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var header = sender as System.Windows.Controls.Primitives.DataGridColumnHeader;
        if (header == null || _viewModel == null)
            return;

        int columnIndex = header.Column.DisplayIndex;

        // Create context menu
        var contextMenu = new ContextMenu();

        var insertLeftMenuItem = new MenuItem { Header = "Insert Column Left" };
        insertLeftMenuItem.Click += (s, args) => _viewModel.InsertColumnLeftCommand.Execute(columnIndex);
        contextMenu.Items.Add(insertLeftMenuItem);

        var insertRightMenuItem = new MenuItem { Header = "Insert Column Right" };
        insertRightMenuItem.Click += (s, args) => _viewModel.InsertColumnRightCommand.Execute(columnIndex);
        contextMenu.Items.Add(insertRightMenuItem);

        header.ContextMenu = contextMenu;
        contextMenu.IsOpen = true;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Clean up clipboard monitoring
        if (_hwndSource != null)
        {
            var windowHelper = new WindowInteropHelper(this);
            RemoveClipboardFormatListener(windowHelper.Handle);
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }

        _viewModel?.SaveSession();
    }

    private void SelectCurrentFileInFolderTree()
    {
        if (_viewModel?.SelectedTab == null || string.IsNullOrEmpty(_viewModel.SelectedTab.FilePath))
            return;

        var filePath = _viewModel.SelectedTab.FilePath;

        // Check if file path starts with "Untitled" (unsaved file)
        if (filePath.StartsWith("Untitled"))
        {
            _viewModel.StatusBarViewModel.ShowMessage("Cannot select unsaved file in folder tree");
            return;
        }

        // Check if file exists
        if (!File.Exists(filePath))
        {
            _viewModel.StatusBarViewModel.ShowMessage("File does not exist");
            return;
        }

        // Check if SelectedFolderPath is set
        if (string.IsNullOrEmpty(_viewModel.SelectedFolderPath))
        {
            _viewModel.StatusBarViewModel.ShowMessage("No folder is currently open in the explorer");
            return;
        }

        // Check if file is within the selected folder
        var fullFilePath = Path.GetFullPath(filePath);
        var fullFolderPath = Path.GetFullPath(_viewModel.SelectedFolderPath);

        if (!fullFilePath.StartsWith(fullFolderPath, StringComparison.OrdinalIgnoreCase))
        {
            _viewModel.StatusBarViewModel.ShowMessage("File is not in the current folder tree");
            return;
        }

        try
        {
            // Get the list of parent directories from root to file
            var pathParts = new List<string>();
            var currentPath = Path.GetDirectoryName(fullFilePath);

            while (currentPath != null && currentPath.Length >= fullFolderPath.Length)
            {
                if (Path.GetFullPath(currentPath) == fullFolderPath)
                {
                    break;
                }
                pathParts.Insert(0, currentPath);
                currentPath = Path.GetDirectoryName(currentPath);
            }

            // Expand parent folders
            TreeViewItem? currentItem = null;
            if (FolderTreeView.Items.Count > 0 && FolderTreeView.Items[0] is TreeViewItem rootItem)
            {
                currentItem = rootItem;
                rootItem.IsExpanded = true;

                // Expand each parent folder
                foreach (var parentPath in pathParts)
                {
                    // Force lazy-load by triggering expansion
                    if (currentItem.Items.Count == 1 && currentItem.Items[0] is string)
                    {
                        currentItem.Items.Clear();
                        var itemPath = currentItem.Tag as string;
                        if (itemPath != null)
                        {
                            PopulateTreeNode(currentItem, itemPath);
                        }
                    }

                    // Find child item with matching path
                    TreeViewItem? childItem = null;
                    foreach (var item in currentItem.Items)
                    {
                        if (item is TreeViewItem treeItem && treeItem.Tag is string itemPath)
                        {
                            if (Path.GetFullPath(itemPath) == Path.GetFullPath(parentPath))
                            {
                                childItem = treeItem;
                                break;
                            }
                        }
                    }

                    if (childItem != null)
                    {
                        childItem.IsExpanded = true;
                        currentItem = childItem;
                    }
                    else
                    {
                        break;
                    }
                }

                // Force lazy-load the final parent folder
                if (currentItem.Items.Count == 1 && currentItem.Items[0] is string)
                {
                    currentItem.Items.Clear();
                    var itemPath = currentItem.Tag as string;
                    if (itemPath != null)
                    {
                        PopulateTreeNode(currentItem, itemPath);
                    }
                }

                // Find the file item
                TreeViewItem? fileItem = null;
                foreach (var item in currentItem.Items)
                {
                    if (item is TreeViewItem treeItem && treeItem.Tag is string itemPath)
                    {
                        if (Path.GetFullPath(itemPath) == fullFilePath)
                        {
                            fileItem = treeItem;
                            break;
                        }
                    }
                }

                if (fileItem != null)
                {
                    // Select and scroll to the item
                    fileItem.IsSelected = true;
                    fileItem.BringIntoView();
                    FolderTreeView.Focus();
                    _viewModel.StatusBarViewModel.ShowMessage($"Selected in folder tree: {Path.GetFileName(filePath)}");
                }
                else
                {
                    _viewModel.StatusBarViewModel.ShowMessage("File not found in folder tree");
                }
            }
        }
        catch (Exception ex)
        {
            _viewModel.StatusBarViewModel.ShowMessage($"Error selecting file: {ex.Message}");
        }
    }

    private void ApplyBulkEdit(TabItemViewModel tab)
    {
        if (tab.VimState.PendingBulkEditRange == null)
            return;

        var range = tab.VimState.PendingBulkEditRange;
        var document = tab.Document;
        var caretPosition = tab.VimState.CellEditCaretPosition;
        var originalValue = tab.VimState.OriginalCellValueForBulkEdit;

        // Get the value from the first cell in the selection range (the one that was edited)
        var editedCell = document.GetCell(new GridPosition(range.StartRow, range.StartColumn));
        if (editedCell == null)
        {
            tab.VimState.PendingBulkEditRange = null;
            tab.VimState.OriginalCellValueForBulkEdit = string.Empty;
            return;
        }

        var newValue = editedCell.Value;

        // Detect the inserted text by comparing original and new values
        string insertedText = string.Empty;
        if (caretPosition == VimEngine.CellEditCaretPosition.Start)
        {
            // Text was inserted at the start
            if (newValue.EndsWith(originalValue))
            {
                insertedText = newValue.Substring(0, newValue.Length - originalValue.Length);
            }
        }
        else // CellEditCaretPosition.End
        {
            // Text was inserted at the end
            if (newValue.StartsWith(originalValue))
            {
                insertedText = newValue.Substring(originalValue.Length);
            }
        }

        // Apply the inserted text to all cells in the selection range
        // Skip the first cell (range.StartRow, range.StartColumn) as it was already edited
        var cellUpdates = new Dictionary<GridPosition, string>();
        for (int r = 0; r < range.RowCount; r++)
        {
            for (int c = 0; c < range.ColumnCount; c++)
            {
                int docRow = range.StartRow + r;
                int docCol = range.StartColumn + c;

                // Skip the first cell that was already edited
                if (docRow == range.StartRow && docCol == range.StartColumn)
                    continue;

                if (docRow < document.RowCount && docCol < document.Rows[docRow].Cells.Count)
                {
                    var cell = document.Rows[docRow].Cells[docCol];
                    string updatedValue;

                    if (caretPosition == VimEngine.CellEditCaretPosition.Start)
                    {
                        // Insert at the start
                        updatedValue = insertedText + cell.Value;
                    }
                    else
                    {
                        // Insert at the end
                        updatedValue = cell.Value + insertedText;
                    }

                    cellUpdates[new GridPosition(docRow, docCol)] = updatedValue;
                }
            }
        }

        // Apply all updates using a command for undo support
        if (cellUpdates.Count > 0)
        {
            var positions = cellUpdates.Keys.ToList();
            var values = new Dictionary<GridPosition, string>(cellUpdates);

            // Create a modified BulkEditCellsCommand that applies different values to each cell
            var command = new Commands.BulkEditCellsWithValuesCommand(document, values);

            if (tab.VimState.CommandHistory != null)
            {
                tab.VimState.CommandHistory.Execute(command);
            }
            else
            {
                command.Execute();
            }
        }

        // Clear the pending bulk edit range
        tab.VimState.PendingBulkEditRange = null;
        tab.VimState.OriginalCellValueForBulkEdit = string.Empty;
    }
}