using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using VGrid.Models;
using VGrid.Services;
using VGrid.ViewModels;
using VGrid.Views;

namespace VGrid.UI;

/// <summary>
/// Manages the folder tree view operations including file/folder creation, deletion, renaming, and drag-drop
/// </summary>
public class FolderTreeManager
{
    private readonly System.Windows.Controls.TreeView _treeView;
    private readonly MainViewModel _viewModel;
    private readonly ITemplateService _templateService;
    private TreeViewItem? _draggedItem;
    private System.Windows.Point _dragStartPoint;
    private bool _isDoubleClickHandlerRegistered;

    public FolderTreeManager(System.Windows.Controls.TreeView treeView, MainViewModel viewModel, ITemplateService templateService)
    {
        _treeView = treeView;
        _viewModel = viewModel;
        _templateService = templateService;
    }

    public void PopulateFolderTree()
    {
        _treeView.Items.Clear();

        if (string.IsNullOrEmpty(_viewModel.SelectedFolderPath))
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
            var newTsvFileMenuItem = new MenuItem { Header = "TSVファイル(_T)" };
            newTsvFileMenuItem.Click += NewTsvFileMenuItem_Click;
            newFileMenuItem.Items.Add(newTsvFileMenuItem);
            var newCsvFileMenuItem = new MenuItem { Header = "CSVファイル(_C)" };
            newCsvFileMenuItem.Click += NewCsvFileMenuItem_Click;
            newFileMenuItem.Items.Add(newCsvFileMenuItem);
            rootContextMenu.Items.Add(newFileMenuItem);

            AddTemplateMenuItems(rootContextMenu, rootItem, _viewModel.SelectedFolderPath);

            var newFolderMenuItem = new MenuItem { Header = "新しいフォルダ(_N)" };
            newFolderMenuItem.Click += NewFolderMenuItem_Click;
            rootContextMenu.Items.Add(newFolderMenuItem);

            rootContextMenu.Items.Add(new Separator());

            var openRootInExplorerMenuItem = new MenuItem { Header = "エクスプローラーで開く(_E)" };
            openRootInExplorerMenuItem.Click += OpenFolderInExplorerMenuItem_Click;
            rootContextMenu.Items.Add(openRootInExplorerMenuItem);

            rootItem.ContextMenu = rootContextMenu;

            PopulateTreeNode(rootItem, _viewModel.SelectedFolderPath);
            _treeView.Items.Add(rootItem);

            // Handle double-click on tree items (register only once)
            if (!_isDoubleClickHandlerRegistered)
            {
                _treeView.MouseDoubleClick += FolderTreeView_MouseDoubleClick;
                _isDoubleClickHandlerRegistered = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void PopulateTreeNode(TreeViewItem node, string path)
    {
        try
        {
            var filterText = _viewModel.FilterText ?? string.Empty;
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
                var newTsvFileMenuItem = new MenuItem { Header = "TSVファイル(_T)" };
                newTsvFileMenuItem.Click += NewTsvFileMenuItem_Click;
                newFileMenuItem.Items.Add(newTsvFileMenuItem);
                var newCsvFileMenuItem = new MenuItem { Header = "CSVファイル(_C)" };
                newCsvFileMenuItem.Click += NewCsvFileMenuItem_Click;
                newFileMenuItem.Items.Add(newCsvFileMenuItem);
                contextMenu.Items.Add(newFileMenuItem);

                AddTemplateMenuItems(contextMenu, dirItem, dir);

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

            // Add files (only TSV/CSV-related)
            var files = Directory.GetFiles(path, "*.tsv")
                .Concat(Directory.GetFiles(path, "*.csv"))
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
                .Concat(Directory.GetFiles(path, "*.csv"))
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
        // If no filter, return plain text with theme color
        if (string.IsNullOrWhiteSpace(filterText))
        {
            var simpleTextBlock = new TextBlock { Text = text };
            simpleTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "TreeViewForegroundBrush");
            return simpleTextBlock;
        }

        var textBlock = new TextBlock();
        textBlock.SetResourceReference(TextBlock.ForegroundProperty, "TreeViewForegroundBrush");
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
                    textBlock.Inlines.Add(new Run(text.Substring(currentIndex)));
                }
                break;
            }

            // Add text before match
            if (matchIndex > currentIndex)
            {
                textBlock.Inlines.Add(new Run(text.Substring(currentIndex, matchIndex - currentIndex)));
            }

            // Add highlighted match
            var matchRun = new Run(text.Substring(matchIndex, filterText.Length))
            {
                Background = new SolidColorBrush(Color.FromRgb(255, 255, 0)), // Yellow
                Foreground = new SolidColorBrush(Color.FromRgb(0, 0, 0)), // Black text for visibility
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
            Orientation = Orientation.Horizontal
        };

        // Add icon
        var icon = new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI Emoji"),
            FontSize = 14,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        if (isFolder)
        {
            icon.Text = "📁"; // Folder icon
            icon.Foreground = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Amber
        }
        else
        {
            // Determine file icon based on extension
            var ext = Path.GetExtension(text).ToLower();
            if (ext == ".tsv" || ext == ".csv" || ext == ".tab")
            {
                icon.Text = "📊"; // Chart icon for TSV files
                icon.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
            }
            else if (ext == ".txt")
            {
                icon.Text = "📄"; // Document icon for text files
                icon.Foreground = new SolidColorBrush(Color.FromRgb(96, 125, 139)); // Blue Grey
            }
            else
            {
                icon.Text = "📄"; // Default document icon
                icon.Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Grey
            }
        }

        stackPanel.Children.Add(icon);

        // Add text (with highlighting if filter is active)
        var textContent = CreateHighlightedHeader(text, filterText);
        if (textContent is TextBlock tb)
        {
            tb.VerticalAlignment = VerticalAlignment.Center;
            stackPanel.Children.Add(tb);
        }

        return stackPanel;
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
        if (_treeView.SelectedItem is TreeViewItem item)
        {
            var path = item.Tag as string;
            if (path != null && File.Exists(path))
            {
                await _viewModel.OpenFileAsync(path);
            }
        }
    }

    public async void FolderTreeView_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_treeView.SelectedItem is TreeViewItem item)
        {
            var itemPath = item.Tag as string;
            if (!string.IsNullOrEmpty(itemPath))
            {
                if (e.Key == Key.Enter)
                {
                    if (File.Exists(itemPath))
                    {
                        // Open file
                        await _viewModel.OpenFileAsync(itemPath);
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

    private TreeViewItem? GetTreeViewItemFromSubMenuItem(MenuItem menuItem)
    {
        // Sub-menu item: MenuItem -> parent MenuItem -> ContextMenu -> TreeViewItem
        if (menuItem.Parent is MenuItem parentMenuItem &&
            parentMenuItem.Parent is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is TreeViewItem item)
        {
            return item;
        }
        return null;
    }

    private void NewTsvFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            var item = GetTreeViewItemFromSubMenuItem(menuItem);
            if (item != null)
            {
                var folderPath = item.Tag as string;
                if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                {
                    CreateNewFile(item, folderPath, ".tsv");
                }
            }
        }
    }

    private void NewCsvFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem)
        {
            var item = GetTreeViewItemFromSubMenuItem(menuItem);
            if (item != null)
            {
                var folderPath = item.Tag as string;
                if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
                {
                    CreateNewFile(item, folderPath, ".csv");
                }
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
                var result = MessageBox.Show(
                    $"ファイル '{fileName}' を削除してもよろしいですか？",
                    "ファイルの削除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Close the file if it's open in a tab
                        var openTab = _viewModel.Tabs.FirstOrDefault(t => t.FilePath == filePath);
                        if (openTab != null)
                        {
                            _viewModel.CloseTab(openTab);
                        }

                        // Delete the file
                        File.Delete(filePath);

                        // Remove from tree view
                        if (item.Parent is ItemsControl parent)
                        {
                            parent.Items.Remove(item);
                        }

                        _viewModel.StatusBarViewModel.ShowMessage($"ファイル '{fileName}' を削除しました。");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
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
                var result = MessageBox.Show(
                    $"フォルダ '{folderName}' とその中身を削除してもよろしいですか？\n\nこの操作は元に戻せません。",
                    "フォルダの削除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        // Close any files from this folder that are open in tabs
                        if (_viewModel.Tabs != null)
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

                        _viewModel.StatusBarViewModel.ShowMessage($"フォルダ '{folderName}' を削除しました。");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(
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
                    MessageBox.Show(
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
                    MessageBox.Show(
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
        var textBox = new TextBox
        {
            Text = itemName,
            Margin = new Thickness(0),
            Padding = new Thickness(2),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Colors.CornflowerBlue),
            Tag = itemPath, // Store original path
            Focusable = true
        };
        // テーマ対応のため、リソースから色を取得
        textBox.SetResourceReference(TextBox.BackgroundProperty, "TextBoxBackgroundBrush");
        textBox.SetResourceReference(TextBox.ForegroundProperty, "TextBoxForegroundBrush");

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
                    var filterText = _viewModel.FilterText ?? string.Empty;
                    item.Header = CreateHeaderWithIcon(itemName, filterText, isFolder);
                }

                // Return focus to TreeView
                _treeView.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                isProcessed = true;
                // Cancel rename - restore original header with highlighting
                var filterText = _viewModel.FilterText ?? string.Empty;
                item.Header = CreateHeaderWithIcon(itemName, filterText, isFolder);
                // Return focus to TreeView
                _treeView.Focus();
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
                var filterText = _viewModel.FilterText ?? string.Empty;
                item.Header = CreateHeaderWithIcon(itemName, filterText, isFolder);
            }
        };

        // Replace header with TextBox
        item.Header = textBox;

        // Focus the TextBox and select item name without extension
        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
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
                MessageBox.Show(
                    $"ファイル '{newFileName}' は既に存在します。",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                var filterText = _viewModel.FilterText ?? string.Empty;
                item.Header = CreateHeaderWithIcon(Path.GetFileName(oldFilePath), filterText, false);
                return;
            }

            // If extension changed, convert file content to new delimiter format
            var oldExt = Path.GetExtension(oldFilePath).ToLowerInvariant();
            var newExt = Path.GetExtension(newFilePath).ToLowerInvariant();
            if (oldExt != newExt && File.Exists(oldFilePath))
            {
                var oldStrategy = DelimiterStrategyFactory.Create(
                    DelimiterStrategyFactory.DetectFromExtension(oldFilePath));
                var newStrategy = DelimiterStrategyFactory.Create(
                    DelimiterStrategyFactory.DetectFromExtension(newFilePath));
                var content = File.ReadAllText(oldFilePath);
                var rows = oldStrategy.ParseContent(content);
                var lines = rows.Select(r => newStrategy.FormatLine(r));
                File.WriteAllLines(oldFilePath, lines);
            }

            // Rename the file
            File.Move(oldFilePath, newFilePath);

            // Update TreeViewItem with highlighting
            var currentFilterText = _viewModel.FilterText ?? string.Empty;
            item.Header = CreateHeaderWithIcon(newFileName, currentFilterText, false);
            item.Tag = newFilePath;

            // Update any open tabs that reference this file
            UpdateOpenTabsForRename(oldFilePath, newFilePath);

            _viewModel.StatusBarViewModel.ShowMessage($"Renamed: {newFileName}");

            // Return focus to TreeView
            _treeView.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"ファイル名の変更に失敗しました: {ex.Message}",
                "エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            var filterText = _viewModel.FilterText ?? string.Empty;
            item.Header = CreateHeaderWithIcon(Path.GetFileName(oldFilePath), filterText, false);

            // Return focus to TreeView
            _treeView.Focus();
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
                MessageBox.Show(
                    $"フォルダ '{newFolderName}' は既に存在します。",
                    "エラー",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                var filterText = _viewModel.FilterText ?? string.Empty;
                item.Header = CreateHeaderWithIcon(new DirectoryInfo(oldFolderPath).Name, filterText, true);
                return;
            }

            // Rename the folder
            Directory.Move(oldFolderPath, newFolderPath);

            // Update TreeViewItem with highlighting
            var currentFilterText = _viewModel.FilterText ?? string.Empty;
            item.Header = CreateHeaderWithIcon(newFolderName, currentFilterText, true);
            item.Tag = newFolderPath;

            // Update any open tabs that reference files in this folder
            UpdateOpenTabsForFolderRename(oldFolderPath, newFolderPath);

            _viewModel.StatusBarViewModel.ShowMessage($"Renamed folder: {newFolderName}");

            // Return focus to TreeView
            _treeView.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"フォルダ名の変更に失敗しました: {ex.Message}",
                "エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            var filterText = _viewModel.FilterText ?? string.Empty;
            item.Header = CreateHeaderWithIcon(new DirectoryInfo(oldFolderPath).Name, filterText, true);

            // Return focus to TreeView
            _treeView.Focus();
        }
    }

    private void UpdateOpenTabsForRename(string oldFilePath, string newFilePath)
    {
        foreach (var tab in _viewModel.Tabs)
        {
            if (tab.FilePath == oldFilePath)
            {
                tab.FilePath = newFilePath;
                tab.Document.DelimiterFormat = DelimiterStrategyFactory.DetectFromExtension(newFilePath);
                var newFileName = Path.GetFileName(newFilePath);
                tab.Header = tab.IsDirty ? $"{newFileName}*" : newFileName;
            }
        }
    }

    private void UpdateOpenTabsForFolderRename(string oldFolderPath, string newFolderPath)
    {
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

    private void CreateNewFile(TreeViewItem parentItem, string folderPath, string extension = ".tsv")
    {
        try
        {
            // Generate a unique file name
            string newFileName = $"NewFile{extension}";
            string newFilePath = Path.Combine(folderPath, newFileName);
            int counter = 1;

            while (File.Exists(newFilePath))
            {
                newFileName = $"NewFile{counter}{extension}";
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

            _viewModel.StatusBarViewModel.ShowMessage($"Created: {newFileName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
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

            _viewModel.StatusBarViewModel.ShowMessage($"Created folder: {newFolderName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
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

    public void FolderTreeView_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Check if we clicked on the TreeView background (not on an item)
        var clickedElement = e.OriginalSource as DependencyObject;

        // Walk up the visual tree to see if we clicked on a TreeViewItem
        var treeViewItem = FindVisualParent<TreeViewItem>(clickedElement);

        if (treeViewItem == null && _viewModel.SelectedFolderPath != null)
        {
            // Clicked on background - show context menu for root folder
            var contextMenu = new ContextMenu();

            var newFileMenuItem = new MenuItem { Header = "新しいファイル(_F)" };
            var newTsvFileMenuItem = new MenuItem { Header = "TSVファイル(_T)" };
            newTsvFileMenuItem.Click += (s, args) =>
            {
                if (_treeView.Items.Count > 0 && _treeView.Items[0] is TreeViewItem rootItem)
                {
                    CreateNewFile(rootItem, _viewModel.SelectedFolderPath, ".tsv");
                }
            };
            newFileMenuItem.Items.Add(newTsvFileMenuItem);
            var newCsvFileMenuItem = new MenuItem { Header = "CSVファイル(_C)" };
            newCsvFileMenuItem.Click += (s, args) =>
            {
                if (_treeView.Items.Count > 0 && _treeView.Items[0] is TreeViewItem rootItem)
                {
                    CreateNewFile(rootItem, _viewModel.SelectedFolderPath, ".csv");
                }
            };
            newFileMenuItem.Items.Add(newCsvFileMenuItem);
            contextMenu.Items.Add(newFileMenuItem);

            if (_treeView.Items.Count > 0 && _treeView.Items[0] is TreeViewItem rootItemForTemplate)
            {
                AddTemplateMenuItems(contextMenu, rootItemForTemplate, _viewModel.SelectedFolderPath);
            }

            var newFolderMenuItem = new MenuItem { Header = "新しいフォルダ(_N)" };
            newFolderMenuItem.Click += (s, args) =>
            {
                // Create folder in root folder
                if (_treeView.Items.Count > 0 && _treeView.Items[0] is TreeViewItem rootItem)
                {
                    CreateNewFolder(rootItem, _viewModel.SelectedFolderPath);
                }
            };
            contextMenu.Items.Add(newFolderMenuItem);

            contextMenu.PlacementTarget = _treeView;
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
            Point currentPosition = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPosition;

            // Check if we've moved far enough to start a drag operation
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                var itemPath = _draggedItem.Tag as string;
                if (!string.IsNullOrEmpty(itemPath))
                {
                    // Create drag data
                    var dragData = new DataObject("TreeViewItem", _draggedItem);
                    dragData.SetData(DataFormats.FileDrop, new[] { itemPath });

                    // Start drag-and-drop operation
                    DragDrop.DoDragDrop(_draggedItem, dragData, DragDropEffects.Move);

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
                        e.Effects = DragDropEffects.Move;

                        // Visual feedback - highlight the drop target
                        targetItem.Background = new SolidColorBrush(Color.FromArgb(50, 0, 120, 215));
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void TreeViewItem_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is TreeViewItem item)
        {
            // Remove visual feedback
            item.Background = Brushes.Transparent;
        }
    }

    private void TreeViewItem_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is TreeViewItem targetItem)
        {
            // Remove visual feedback
            targetItem.Background = Brushes.Transparent;

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
                var result = MessageBox.Show(
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
                var sourceParentItem = FindTreeViewItemByPathRecursive(_treeView, sourceParentPath);
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

            _viewModel.StatusBarViewModel.ShowMessage($"Moved: {itemName} to {Path.GetFileName(targetFolderPath)}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
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

    public void SelectCurrentFileInFolderTree()
    {
        if (_viewModel.SelectedTab == null || string.IsNullOrEmpty(_viewModel.SelectedTab.FilePath))
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
            if (_treeView.Items.Count > 0 && _treeView.Items[0] is TreeViewItem rootItem)
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
                    _treeView.Focus();
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

    /// <summary>
    /// コンテキストメニューに「テンプレートから新規作成」サブメニューを追加
    /// サブメニューを開いた時に動的にテンプレート一覧を取得する
    /// </summary>
    private void AddTemplateMenuItems(ContextMenu contextMenu, TreeViewItem parentItem, string folderPath)
    {
        // サブメニュー作成
        var templateMenuItem = new MenuItem
        {
            Header = "テンプレートから新規作成(_T)"
        };

        // サブメニューを開いた時に動的にテンプレート一覧を取得
        templateMenuItem.SubmenuOpened += (s, e) =>
        {
            templateMenuItem.Items.Clear();

            var templates = _templateService.GetAvailableTemplates();

            if (templates.Count == 0)
            {
                var noTemplateItem = new MenuItem
                {
                    Header = "(テンプレートがありません)",
                    IsEnabled = false
                };
                templateMenuItem.Items.Add(noTemplateItem);
                return;
            }

            // 各テンプレートをサブアイテムとして追加
            foreach (var template in templates)
            {
                var templateItem = new MenuItem
                {
                    Header = template.DisplayName,
                    Tag = template
                };

                // クリック時に parentItem.Tag から最新のパスを取得
                // （フォルダ名変更後も正しいパスを使用するため）
                templateItem.Click += (sender, args) =>
                    CreateFileFromTemplateMenuItem_Click(sender, args, parentItem);

                templateMenuItem.Items.Add(templateItem);
            }
        };

        // ダミーアイテムを追加（サブメニュー矢印を表示するため）
        templateMenuItem.Items.Add(new MenuItem { Header = "Loading..." });

        // 「新しいファイル」の次に挿入（index 1）
        contextMenu.Items.Insert(1, templateMenuItem);

        // テンプレートセットからの一括作成メニュー
        var templateSetMenuItem = new MenuItem
        {
            Header = "テンプレートセットから一括作成(_S)"
        };

        // サブメニューを開いた時に動的にテンプレートセット一覧を取得
        templateSetMenuItem.SubmenuOpened += (s, e) =>
        {
            templateSetMenuItem.Items.Clear();

            var templateSets = _templateService.GetTemplateSets().ToList();

            if (templateSets.Count == 0)
            {
                var noSetItem = new MenuItem
                {
                    Header = "(テンプレートセットがありません)",
                    IsEnabled = false
                };
                templateSetMenuItem.Items.Add(noSetItem);
                return;
            }

            // 各テンプレートセットをサブアイテムとして追加
            foreach (var templateSet in templateSets)
            {
                var setItem = new MenuItem
                {
                    Header = templateSet.Name,
                    Tag = templateSet
                };

                setItem.Click += CreateFilesFromTemplateSetMenuItem_Click;

                templateSetMenuItem.Items.Add(setItem);
            }
        };

        // ダミーアイテムを追加（サブメニュー矢印を表示するため）
        templateSetMenuItem.Items.Add(new MenuItem { Header = "Loading..." });

        // テンプレートメニューの次に挿入（index 2）
        contextMenu.Items.Insert(2, templateSetMenuItem);
    }

    private void CreateFilesFromTemplateSetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is TemplateSet templateSet)
        {
            // メニュー階層をたどってContextMenuを取得し、PlacementTargetからTreeViewItemを取得
            var targetItem = GetTreeViewItemFromMenuItem(menuItem);
            if (targetItem == null)
            {
                MessageBox.Show("出力先フォルダが見つかりません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var targetDirectory = targetItem.Tag as string;
            if (string.IsNullOrEmpty(targetDirectory) || !Directory.Exists(targetDirectory))
            {
                MessageBox.Show("出力先フォルダが見つかりません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // プレースホルダーの数を検出
            var placeholderCount = _templateService.DetectPlaceholderCountInSet(templateSet);

            if (placeholderCount > 0)
            {
                // プレースホルダーがある場合はダイアログを表示
                var dialog = new TemplateSetDialog(templateSet, _templateService, targetDirectory)
                {
                    Owner = Window.GetWindow(_treeView)
                };

                if (dialog.ShowDialog() == true && dialog.CreatedFiles.Count > 0)
                {
                    // ツリーをリフレッシュ
                    RefreshTreeNode(targetItem);
                }
            }
            else
            {
                // プレースホルダーがない場合は直接ファイルを作成
                try
                {
                    var createdFiles = _templateService.CreateFilesFromSet(templateSet, targetDirectory);

                    if (createdFiles.Count > 0)
                    {
                        // ツリーをリフレッシュ
                        RefreshTreeNode(targetItem);
                        _viewModel.StatusBarViewModel.ShowMessage(
                            $"Created {createdFiles.Count} files from template set: {templateSet.Name}");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"ファイルの作成に失敗しました:\n{ex.Message}",
                        "エラー",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
    }

    /// <summary>
    /// MenuItemからContextMenuのPlacementTarget（TreeViewItem）を取得
    /// </summary>
    private TreeViewItem? GetTreeViewItemFromMenuItem(MenuItem menuItem)
    {
        DependencyObject? current = menuItem;
        while (current != null)
        {
            if (current is ContextMenu contextMenu)
            {
                return contextMenu.PlacementTarget as TreeViewItem;
            }
            current = LogicalTreeHelper.GetParent(current);
        }
        return null;
    }

    /// <summary>
    /// テンプレートからファイルを作成
    /// </summary>
    private void CreateFileFromTemplateMenuItem_Click(
        object sender,
        RoutedEventArgs e,
        TreeViewItem parentItem)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not TemplateInfo template)
            return;

        // parentItem.Tag から最新のフォルダパスを取得
        // （フォルダ名変更後も正しいパスを使用するため）
        var folderPath = parentItem.Tag as string;
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            MessageBox.Show(
                "フォルダが見つかりません。ツリーを更新してください。",
                "エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        try
        {
            // テンプレートファイル名にプレースホルダーが含まれているかチェック
            var placeholderCount = _templateService.DetectPlaceholderCount(template.FileName);

            string newFilePath;

            if (placeholderCount > 0)
            {
                // プレースホルダーがある場合はダイアログを表示
                var dialog = new TemplatePlaceholderDialog(
                    _templateService,
                    template.FileName,
                    template.DisplayName,
                    folderPath,
                    placeholderCount)
                {
                    Owner = Window.GetWindow(_treeView)
                };

                if (dialog.ShowDialog() != true || string.IsNullOrEmpty(dialog.CreatedFilePath))
                {
                    return; // キャンセルされた
                }

                newFilePath = dialog.CreatedFilePath;
            }
            else
            {
                // プレースホルダーがない場合は従来通りの処理
                newFilePath = _templateService.CreateFileFromTemplate(
                    template.FileName,
                    folderPath
                );
            }

            var newFileName = Path.GetFileName(newFilePath);

            // 親ノードを展開
            if (!parentItem.IsExpanded)
            {
                parentItem.IsExpanded = true;
            }

            // TreeViewを更新
            RefreshTreeNode(parentItem);

            // 新しく作成されたファイルアイテムを探して選択
            var newFileItem = FindTreeItemByPath(parentItem, newFilePath);
            if (newFileItem != null)
            {
                newFileItem.IsSelected = true;
                // プレースホルダーがなかった場合のみ名前変更モードに入る
                if (placeholderCount == 0)
                {
                    BeginRenameTreeItem(newFileItem, false);
                }
            }

            _viewModel.StatusBarViewModel.ShowMessage(
                $"Created from template: {newFileName}"
            );
        }
        catch (FileNotFoundException ex)
        {
            MessageBox.Show(
                $"テンプレートファイルが見つかりません: {ex.Message}",
                "エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"テンプレートからのファイル作成に失敗しました: {ex.Message}",
                "エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
