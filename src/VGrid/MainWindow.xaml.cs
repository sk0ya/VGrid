using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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

    // Manager classes
    private FolderTreeManager? _folderTreeManager;
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
        _folderTreeManager = new FolderTreeManager(FolderTreeView, _viewModel);
        _dataGridManager = new DataGridManager(_viewModel);
        _selectionManager = new SelectionManager(_viewModel);
        _vimInputHandler = new VimInputHandler(_viewModel);

        // Subscribe to SelectedFolderPath and FilterText changes
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
        };

        // Set focus to the window and restore session asynchronously
        Loaded += MainWindow_Loaded;

        // Save session on window closing
        Closing += MainWindow_Closing;
    }

    // Folder Tree Event Handlers - Delegate to FolderTreeManager
    private void FolderTreeView_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) => _folderTreeManager?.FolderTreeView_KeyDown(sender, e);
    private void FolderTreeView_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) => _folderTreeManager?.FolderTreeView_MouseRightButtonUp(sender, e);

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
}
