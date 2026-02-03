using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using VGrid.UI;
using VGrid.ViewModels;
using VGrid.VimEngine;

namespace VGrid;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;
    private HwndSource? _hwndSource;
    private GridLength _savedSidebarWidth = new GridLength(250);

    // Manager classes
    private FolderTreeManager? _folderTreeManager;
    private TemplateTreeManager? _templateTreeManager;
    private DataGridManager? _dataGridManager;
    private SelectionManager? _selectionManager;
    private VimInputHandler? _vimInputHandler;

    // Win32 API for clipboard monitoring
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    // Win32 API for proper window maximization with taskbar
    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private const uint MONITOR_DEFAULTTONEAREST = 2;
    private const int WM_GETMINMAXINFO = 0x0024;
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private const int WM_MOUSEHWHEEL = 0x020E;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // Initialize manager classes
        _folderTreeManager = new FolderTreeManager(FolderTreeView, _viewModel, _viewModel.TemplateService);
        _templateTreeManager = new TemplateTreeManager(TemplateTreeView, _viewModel, _viewModel.TemplateService);
        _dataGridManager = new DataGridManager(_viewModel);
        _selectionManager = new SelectionManager(_viewModel);
        _vimInputHandler = new VimInputHandler(_viewModel);

        // Initialize template tree
        _templateTreeManager.PopulateTemplateTree();

        // Subscribe to SelectedFolderPath, FilterText, and IsSidebarOpen changes
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.SelectedFolderPath))
            {
                _folderTreeManager.PopulateFolderTree();
            }
            else if (e.PropertyName == nameof(_viewModel.FilterText))
            {
                _folderTreeManager.PopulateFolderTree();
            }
            else if (e.PropertyName == nameof(_viewModel.IsSidebarOpen))
            {
                UpdateSidebarWidth();
            }
        };

        // Subscribe to ScrollToCenterRequested event from MainViewModel
        _viewModel.OnScrollToCenterRequested += OnScrollToCenterRequested;

        // Phase 2 optimization: Subscribe to TabClosed event for cleanup
        _viewModel.TabClosed += OnTabClosed;

        // Subscribe to MaxColumnWidthChanged event for realtime column width updates
        _viewModel.MaxColumnWidthChanged += OnMaxColumnWidthChanged;

        // Set focus to the window and restore session asynchronously
        Loaded += MainWindow_Loaded;

        // Set up WndProc hook early to catch WM_GETMINMAXINFO before first maximize
        SourceInitialized += MainWindow_SourceInitialized;

        // Save session on window closing
        Closing += MainWindow_Closing;
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

    // Folder Tree Event Handlers - Delegate to FolderTreeManager
    private void FolderTreeView_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) => _folderTreeManager?.FolderTreeView_KeyDown(sender, e);
    private void FolderTreeView_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) => _folderTreeManager?.FolderTreeView_MouseRightButtonUp(sender, e);

    // Template Tree Event Handlers - Delegate to TemplateTreeManager
    private void TemplateTreeView_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) => _templateTreeManager?.TemplateTreeView_KeyDown(sender, e);
    private void TemplateTreeView_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) => _templateTreeManager?.TemplateTreeView_MouseRightButtonUp(sender, e);

    // Template Toolbar Button Handlers
    private void NewTemplateButton_Click(object sender, RoutedEventArgs e) => _templateTreeManager?.CreateNewTemplate();
    private void NewTemplateFolderButton_Click(object sender, RoutedEventArgs e) => _templateTreeManager?.CreateNewFolder();
    private void RefreshTemplateButton_Click(object sender, RoutedEventArgs e) => _templateTreeManager?.PopulateTemplateTree();

    // Activity Bar Button Handlers
    private void ActivityBarButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radioButton && radioButton.Tag is string viewName)
        {
            if (Enum.TryParse<Models.SidebarView>(viewName, out var view))
            {
                _viewModel?.SelectSidebarView(view);
            }
        }
    }

    // Sidebar width management
    private void UpdateSidebarWidth()
    {
        if (_viewModel == null) return;

        if (_viewModel.IsSidebarOpen)
        {
            // Restore sidebar width
            SidebarColumn.Width = _savedSidebarWidth;
            SplitterColumn.Width = new GridLength(5);
        }
        else
        {
            // Save current width and collapse
            if (SidebarColumn.Width.Value > 0)
            {
                _savedSidebarWidth = SidebarColumn.Width;
            }
            SidebarColumn.Width = new GridLength(0);
            SplitterColumn.Width = new GridLength(0);
        }
    }

    // DataGrid Event Handlers - Delegate to DataGridManager
    private void TsvGrid_Loaded(object sender, RoutedEventArgs e) => _dataGridManager?.TsvGrid_Loaded(sender, e);
    private void TsvGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e) => _dataGridManager?.TsvGrid_CellEditEnding(sender, e);
    private void TsvGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e) => _dataGridManager?.TsvGrid_BeginningEdit(sender, e);
    private void TsvGrid_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e) => _dataGridManager?.TsvGrid_OnDataContextChanged(sender, e);

    // Selection Event Handlers - Delegate to SelectionManager
    private void RowHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _selectionManager?.RowHeader_PreviewMouseLeftButtonDown(sender, e);
    private void RowHeader_PreviewMouseMove(object sender, MouseEventArgs e) => _selectionManager?.RowHeader_PreviewMouseMove(sender, e);
    private void RowHeader_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => _selectionManager?.RowHeader_PreviewMouseLeftButtonUp(sender, e);
    private void ColumnHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _selectionManager?.ColumnHeader_PreviewMouseLeftButtonDown(sender, e);
    private void ColumnHeader_PreviewMouseMove(object sender, MouseEventArgs e) => _selectionManager?.ColumnHeader_PreviewMouseMove(sender, e);
    private void ColumnHeader_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => _selectionManager?.ColumnHeader_PreviewMouseLeftButtonUp(sender, e);
    private void RowHeader_MouseRightButtonUp(object sender, MouseButtonEventArgs e) => _selectionManager?.RowHeader_MouseRightButtonUp(sender, e);
    private void ColumnHeader_MouseRightButtonUp(object sender, MouseButtonEventArgs e) => _selectionManager?.ColumnHeader_MouseRightButtonUp(sender, e);

    // Tab Header Event Handler - Close tab on middle mouse button click
    private void TabHeader_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.MiddleButton == MouseButtonState.Pressed)
        {
            if (sender is FrameworkElement element && element.DataContext is TabItemViewModel tab)
            {
                _viewModel?.CloseTabCommand.Execute(tab);
                e.Handled = true;
            }
        }
    }

    // Keyboard Input Handler - Delegate to VimInputHandler
    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Handle Ctrl+Shift+E for Select in Folder Tree (special case that needs FolderTreeManager)
        if (e.Key == Key.E && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            _folderTreeManager?.SelectCurrentFileInFolderTree();
            e.Handled = true;
            return;
        }

        _vimInputHandler?.Window_PreviewKeyDown(sender, e);
    }

    // Window Lifecycle Methods
    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        // Set up WndProc hook early to catch WM_GETMINMAXINFO before first maximize
        var windowHelper = new WindowInteropHelper(this);
        _hwndSource = HwndSource.FromHwnd(windowHelper.Handle);
        if (_hwndSource != null)
        {
            _hwndSource.AddHook(WndProc);
            AddClipboardFormatListener(windowHelper.Handle);
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Focus();

        // Restore session asynchronously on background thread
        if (_viewModel != null)
        {
            await _viewModel.RestoreSessionAsync();

            // Check for command line folder argument
            if (System.Windows.Application.Current is App app &&
                !string.IsNullOrEmpty(app.StartupFolderPath))
            {
                _viewModel.OpenFolderByPath(app.StartupFolderPath);
            }
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Check for unsaved changes BEFORE cleanup
        if (_viewModel != null)
        {
            bool canClose = _viewModel.ConfirmCloseApplication();

            if (!canClose)
            {
                // User cancelled, prevent window from closing
                e.Cancel = true;
                return;
            }

            // User confirmed close, save session
            _viewModel.SaveSession();
        }

        // Clean up folder tree file watcher
        _folderTreeManager?.Dispose();

        // Clean up clipboard monitoring
        if (_hwndSource != null)
        {
            var windowHelper = new WindowInteropHelper(this);
            RemoveClipboardFormatListener(windowHelper.Handle);
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }
    }

    // Clipboard Monitoring, Horizontal Scroll, and Window Maximization
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Handle window maximization to respect taskbar
        if (msg == WM_GETMINMAXINFO)
        {
            WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }
        // Handle clipboard update notification
        else if (msg == WM_CLIPBOARDUPDATE)
        {
            OnClipboardChanged();
        }
        // Handle horizontal mouse wheel (tilt wheel)
        else if (msg == WM_MOUSEHWHEEL)
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

    private void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

        // Get the monitor that the window is on
        IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

        if (monitor != IntPtr.Zero)
        {
            var monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            GetMonitorInfo(monitor, ref monitorInfo);

            // rcWork is the work area (excludes taskbar)
            // rcMonitor is the full monitor area
            RECT rcWorkArea = monitorInfo.rcWork;
            RECT rcMonitorArea = monitorInfo.rcMonitor;

            // Get DPI scaling factor for this window
            // This is important for Remote Desktop and multi-DPI environments
            double dpiScale = 1.0;
            var source = PresentationSource.FromVisual(this);
            if (source?.CompositionTarget != null)
            {
                dpiScale = source.CompositionTarget.TransformToDevice.M11;
            }

            // Set the maximized position relative to the monitor
            // Apply DPI scaling to get correct position
            mmi.ptMaxPosition.X = Math.Abs(rcWorkArea.Left - rcMonitorArea.Left);
            mmi.ptMaxPosition.Y = Math.Abs(rcWorkArea.Top - rcMonitorArea.Top);

            // Set the maximized size to the work area size
            // For Remote Desktop, we need to ensure we don't exceed the work area
            int workWidth = Math.Abs(rcWorkArea.Right - rcWorkArea.Left);
            int workHeight = Math.Abs(rcWorkArea.Bottom - rcWorkArea.Top);

            // Apply a small margin to ensure the window doesn't overlap the taskbar
            // This helps with Remote Desktop where DPI detection may be inaccurate
            mmi.ptMaxSize.X = workWidth;
            mmi.ptMaxSize.Y = workHeight;

            // Also set the max track size to prevent resizing beyond work area
            mmi.ptMaxTrackSize.X = workWidth;
            mmi.ptMaxTrackSize.Y = workHeight;
        }

        Marshal.StructureToPtr(mmi, lParam, true);
    }

    private void OnClipboardChanged()
    {
        // Check if clipboard was changed by an external application
        bool hasChanged = ClipboardHelper.HasClipboardChangedExternally();

        if (hasChanged)
        {
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

    private void OnScrollToCenterRequested(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[MainWindow] OnScrollToCenterRequested called");
        if (_viewModel?.SelectedTab == null || _dataGridManager == null)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] ViewModel or DataGridManager is null");
            return;
        }

        // Use DataGridManager's lookup to find the DataGrid for this tab
        _dataGridManager.ScrollToCenterForTab(_viewModel.SelectedTab);
    }

    /// <summary>
    /// Phase 2 optimization: Clean up cached handlers when a tab is closed
    /// </summary>
    private void OnTabClosed(object? sender, TabItemViewModel tab)
    {
        _dataGridManager?.CleanupTab(tab);
    }

    /// <summary>
    /// Recalculate column widths when MaxColumnWidth setting is changed
    /// </summary>
    private void OnMaxColumnWidthChanged(object? sender, EventArgs e)
    {
        _dataGridManager?.RecalculateAllColumnWidths();
    }

    /// <summary>
    /// Show diff when double-clicking a file in Git Changes list
    /// </summary>
    private void GitChangesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GitChangesListBox.SelectedItem is Models.UncommittedFile file)
        {
            _viewModel?.GitChangesViewModel.ShowDiffCommand.Execute(file);
        }
    }

    private DataGrid? FindDataGridForTab(TabItemViewModel tab)
    {
        System.Diagnostics.Debug.WriteLine("[MainWindow] FindDataGridForTab called");

        // Find the TabControl
        var tabControl = FindVisualChild<TabControl>(this);
        if (tabControl == null)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] TabControl not found");
            return null;
        }

        System.Diagnostics.Debug.WriteLine($"[MainWindow] TabControl found, SelectedItem={tabControl.SelectedItem?.GetType().Name}");

        // For TabControl with ItemsSource, we need to find the ContentPresenter differently
        // First, ensure the selected item is the current tab
        if (tabControl.SelectedItem != tab)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Selected item is not the current tab");
            return null;
        }

        // Update the layout to ensure containers are generated
        tabControl.UpdateLayout();

        // Find the ContentPresenter for the selected content
        var contentPresenter = FindVisualChild<ContentPresenter>(tabControl);
        if (contentPresenter == null)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] ContentPresenter not found");
            return null;
        }

        System.Diagnostics.Debug.WriteLine("[MainWindow] ContentPresenter found");

        // Find the DataGrid within the ContentPresenter
        var dataGrid = FindVisualChild<DataGrid>(contentPresenter);
        if (dataGrid == null)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] DataGrid not found in ContentPresenter");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] DataGrid found: Items.Count={dataGrid.Items.Count}");
        }

        return dataGrid;
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

    private T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent)
                return parent;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    // Context Menu Event Handlers
    private void ContextMenu_Cut_Click(object sender, RoutedEventArgs e)
    {
        // Copy first, then delete
        ContextMenu_Copy_Click(sender, e);
        ContextMenu_Delete_Click(sender, e);
    }

    private void ContextMenu_Copy_Click(object sender, RoutedEventArgs e)
    {
        var tab = _viewModel?.SelectedTab;
        if (tab == null) return;

        var state = tab.VimState;
        var document = tab.GridViewModel.Document;

        // Check if there's a visual selection
        if (state.CurrentSelection != null)
        {
            var selection = state.CurrentSelection;
            int rows = selection.RowCount;
            int cols = selection.ColumnCount;
            string[,] values = new string[rows, cols];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int docRow = selection.StartRow + r;
                    int docCol = selection.StartColumn + c;

                    if (docRow < document.RowCount && docCol < document.Rows[docRow].Cells.Count)
                    {
                        values[r, c] = document.Rows[docRow].Cells[docCol].Value;
                    }
                    else
                    {
                        values[r, c] = string.Empty;
                    }
                }
            }

            state.LastYank = new VimEngine.YankedContent
            {
                Values = values,
                SourceType = selection.Type,
                Rows = rows,
                Columns = cols
            };
        }
        else
        {
            // Copy single cell at cursor
            if (state.CursorPosition.Row >= document.RowCount) return;

            var cell = document.GetCell(state.CursorPosition);
            if (cell == null) return;

            string[,] values = new string[1, 1];
            values[0, 0] = cell.Value;

            state.LastYank = new VimEngine.YankedContent
            {
                Values = values,
                SourceType = VimEngine.VisualType.Character,
                Rows = 1,
                Columns = 1
            };
        }

        VimEngine.ClipboardHelper.CopyToClipboard(state.LastYank);
        state.OnYankPerformed();
    }

    private void ContextMenu_Paste_Click(object sender, RoutedEventArgs e)
    {
        var tab = _viewModel?.SelectedTab;
        if (tab == null) return;

        var state = tab.VimState;
        var document = tab.GridViewModel.Document;

        var yank = state.LastYank ?? VimEngine.ClipboardHelper.ReadFromClipboard();
        if (yank == null) return;

        var startPos = state.CursorPosition;

        var command = new Commands.PasteCommand(document, startPos, yank, pasteBefore: false);
        if (state.CommandHistory != null)
        {
            state.CommandHistory.Execute(command);
        }
        else
        {
            command.Execute();
        }

        if (command.AffectedColumns.Any())
        {
            state.OnColumnWidthUpdateRequested(command.AffectedColumns);
        }
    }

    private void ContextMenu_Delete_Click(object sender, RoutedEventArgs e)
    {
        var tab = _viewModel?.SelectedTab;
        if (tab == null) return;

        var state = tab.VimState;
        var document = tab.GridViewModel.Document;

        // Check if there's a visual selection
        if (state.CurrentSelection != null)
        {
            var selection = state.CurrentSelection;

            var command = new Commands.DeleteSelectionCommand(document, selection);
            if (state.CommandHistory != null)
            {
                state.CommandHistory.Execute(command);
            }
            else
            {
                command.Execute();
            }

            // Exit visual mode after delete
            state.SwitchMode(VimEngine.VimMode.Normal);
        }
        else
        {
            // Delete single cell at cursor
            if (state.CursorPosition.Row >= document.RowCount) return;

            var command = new Commands.EditCellCommand(document, state.CursorPosition, string.Empty);
            if (state.CommandHistory != null)
            {
                state.CommandHistory.Execute(command);
            }
            else
            {
                command.Execute();
            }
        }
    }
}
