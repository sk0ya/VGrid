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

    private const int WM_CLIPBOARDUPDATE = 0x031D;

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

        // Set focus to the window and restore session asynchronously
        Loaded += MainWindow_Loaded;

        // Save session on window closing
        Closing += MainWindow_Closing;
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
    private void ColumnHeader_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) => _selectionManager?.ColumnHeader_PreviewMouseLeftButtonDown(sender, e);
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

        // Clean up clipboard monitoring
        if (_hwndSource != null)
        {
            var windowHelper = new WindowInteropHelper(this);
            RemoveClipboardFormatListener(windowHelper.Handle);
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }
    }

    // Clipboard Monitoring
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
}
