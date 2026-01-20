using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VGrid.Services;
using VGrid.ViewModels;

namespace VGrid.UI;

/// <summary>
/// Manages the template tree view operations including creation, deletion, renaming, and opening templates
/// Supports folder hierarchy within the Template directory
/// </summary>
public class TemplateTreeManager
{
    private readonly TreeView _treeView;
    private readonly MainViewModel _viewModel;
    private readonly ITemplateService _templateService;
    private bool _isDoubleClickHandlerRegistered;

    public TemplateTreeManager(TreeView treeView, MainViewModel viewModel, ITemplateService templateService)
    {
        _treeView = treeView;
        _viewModel = viewModel;
        _templateService = templateService;
    }

    /// <summary>
    /// テンプレート一覧をTreeViewに表示（フォルダ階層対応）
    /// </summary>
    public void PopulateTemplateTree()
    {
        _treeView.Items.Clear();

        try
        {
            var templateDir = _templateService.GetTemplateDirectoryPath();
            PopulateTreeNode(null, templateDir);

            // Handle double-click on tree items (register only once)
            if (!_isDoubleClickHandlerRegistered)
            {
                _treeView.MouseDoubleClick += TemplateTreeView_MouseDoubleClick;
                _isDoubleClickHandlerRegistered = true;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading templates: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// TreeViewノードを再帰的に構築
    /// </summary>
    private void PopulateTreeNode(TreeViewItem? parentItem, string directoryPath)
    {
        // Add subdirectories
        var subdirectories = _templateService.GetSubdirectories(directoryPath);
        foreach (var subdir in subdirectories)
        {
            var folderName = Path.GetFileName(subdir);
            var folderItem = new TreeViewItem
            {
                Header = CreateHeaderWithIcon(folderName, true),
                Tag = subdir
            };

            // Add dummy item for lazy loading
            folderItem.Items.Add("Loading...");
            folderItem.Expanded += TreeViewItem_Expanded;

            // Add context menu for folder
            folderItem.ContextMenu = CreateFolderContextMenu();

            if (parentItem == null)
                _treeView.Items.Add(folderItem);
            else
                parentItem.Items.Add(folderItem);
        }

        // Add templates
        var templates = _templateService.GetTemplatesInDirectory(directoryPath);
        foreach (var template in templates)
        {
            var templateItem = new TreeViewItem
            {
                Header = CreateHeaderWithIcon(template.DisplayName, false),
                Tag = template
            };

            // Add context menu for template
            templateItem.ContextMenu = CreateTemplateContextMenu();

            if (parentItem == null)
                _treeView.Items.Add(templateItem);
            else
                parentItem.Items.Add(templateItem);
        }
    }

    private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
    {
        var item = (TreeViewItem)sender;
        if (item.Items.Count == 1 && item.Items[0] is string)
        {
            item.Items.Clear();
            var path = item.Tag as string;
            if (path != null && Directory.Exists(path))
            {
                PopulateTreeNode(item, path);
            }
        }
    }

    private ContextMenu CreateTemplateContextMenu()
    {
        var contextMenu = new ContextMenu();

        var openMenuItem = new MenuItem { Header = "開く(_O)" };
        openMenuItem.Click += OpenTemplateMenuItem_Click;
        contextMenu.Items.Add(openMenuItem);

        contextMenu.Items.Add(new Separator());

        var renameMenuItem = new MenuItem { Header = "名前の変更(_R)" };
        renameMenuItem.Click += RenameTemplateMenuItem_Click;
        contextMenu.Items.Add(renameMenuItem);

        var deleteMenuItem = new MenuItem { Header = "削除(_D)" };
        deleteMenuItem.Click += DeleteTemplateMenuItem_Click;
        contextMenu.Items.Add(deleteMenuItem);

        contextMenu.Items.Add(new Separator());

        var openFolderMenuItem = new MenuItem { Header = "エクスプローラーで開く(_E)" };
        openFolderMenuItem.Click += OpenTemplateInExplorerMenuItem_Click;
        contextMenu.Items.Add(openFolderMenuItem);

        return contextMenu;
    }

    private ContextMenu CreateFolderContextMenu()
    {
        var contextMenu = new ContextMenu();

        var newTemplateMenuItem = new MenuItem { Header = "新規テンプレート(_T)" };
        newTemplateMenuItem.Click += NewTemplateInFolderMenuItem_Click;
        contextMenu.Items.Add(newTemplateMenuItem);

        var newFolderMenuItem = new MenuItem { Header = "新規フォルダ(_N)" };
        newFolderMenuItem.Click += NewFolderMenuItem_Click;
        contextMenu.Items.Add(newFolderMenuItem);

        contextMenu.Items.Add(new Separator());

        var renameMenuItem = new MenuItem { Header = "名前の変更(_R)" };
        renameMenuItem.Click += RenameFolderMenuItem_Click;
        contextMenu.Items.Add(renameMenuItem);

        var deleteMenuItem = new MenuItem { Header = "削除(_D)" };
        deleteMenuItem.Click += DeleteFolderMenuItem_Click;
        contextMenu.Items.Add(deleteMenuItem);

        contextMenu.Items.Add(new Separator());

        var openFolderMenuItem = new MenuItem { Header = "エクスプローラーで開く(_E)" };
        openFolderMenuItem.Click += OpenFolderInExplorerMenuItem_Click;
        contextMenu.Items.Add(openFolderMenuItem);

        return contextMenu;
    }

    private object CreateHeaderWithIcon(string text, bool isFolder)
    {
        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };

        var icon = new TextBlock
        {
            FontFamily = new FontFamily("Segoe UI Emoji"),
            FontSize = 14,
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        if (isFolder)
        {
            icon.Text = "\U0001F4C1"; // Folder icon
            icon.Foreground = new SolidColorBrush(Color.FromRgb(255, 193, 7)); // Amber
        }
        else
        {
            icon.Text = "\U0001F4CB"; // Clipboard icon for templates
            icon.Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
        }

        stackPanel.Children.Add(icon);

        var textBlock = new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center
        };
        stackPanel.Children.Add(textBlock);

        return stackPanel;
    }

    private async void TemplateTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Find the TreeViewItem that was double-clicked
        var clickedElement = e.OriginalSource as DependencyObject;
        var treeViewItem = FindVisualParent<TreeViewItem>(clickedElement);

        if (treeViewItem == null)
            return;

        if (treeViewItem.Tag is TemplateInfo template)
        {
            await _viewModel.OpenFileAsync(template.FullPath);
            e.Handled = true;
        }
        // For folders, don't handle - let TreeView's default behavior expand/collapse
    }

    public async void TemplateTreeView_KeyDown(object sender, KeyEventArgs e)
    {
        if (_treeView.SelectedItem is TreeViewItem item)
        {
            if (e.Key == Key.Enter)
            {
                if (item.Tag is TemplateInfo template)
                {
                    await _viewModel.OpenFileAsync(template.FullPath);
                    e.Handled = true;
                }
                else if (item.Tag is string folderPath && Directory.Exists(folderPath))
                {
                    item.IsExpanded = !item.IsExpanded;
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.F2)
            {
                if (item.Tag is TemplateInfo template)
                {
                    BeginRenameTemplate(item, template);
                    e.Handled = true;
                }
                else if (item.Tag is string folderPath && Directory.Exists(folderPath))
                {
                    BeginRenameFolder(item, folderPath);
                    e.Handled = true;
                }
            }
        }
    }

    // Template menu item handlers
    private async void OpenTemplateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextItem(sender) is { } item && item.Tag is TemplateInfo template)
        {
            await _viewModel.OpenFileAsync(template.FullPath);
        }
    }

    private void RenameTemplateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextItem(sender) is { } item && item.Tag is TemplateInfo template)
        {
            BeginRenameTemplate(item, template);
        }
    }

    private void DeleteTemplateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextItem(sender) is { } item && item.Tag is TemplateInfo template)
        {
            var result = MessageBox.Show(
                $"テンプレート '{template.DisplayName}' を削除してもよろしいですか？",
                "テンプレートの削除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Close the template if it's open in a tab
                    var openTab = _viewModel.Tabs.FirstOrDefault(t => t.FilePath == template.FullPath);
                    if (openTab != null)
                    {
                        _viewModel.CloseTab(openTab);
                    }

                    File.Delete(template.FullPath);

                    // Remove from tree
                    if (item.Parent is TreeViewItem parentItem)
                        parentItem.Items.Remove(item);
                    else
                        _treeView.Items.Remove(item);

                    _viewModel.RefreshTemplatesCommand.Execute(null);
                    _viewModel.StatusBarViewModel.ShowMessage($"テンプレート '{template.DisplayName}' を削除しました。");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"削除に失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void OpenTemplateInExplorerMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextItem(sender) is { } item && item.Tag is TemplateInfo template)
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{template.FullPath}\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"エクスプローラーを開けませんでした: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // Folder menu item handlers
    private void NewTemplateInFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextItem(sender) is { } item && item.Tag is string folderPath)
        {
            CreateNewTemplateInFolder(item, folderPath);
        }
    }

    private void NewFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextItem(sender) is { } item && item.Tag is string parentFolderPath)
        {
            CreateNewFolder(item, parentFolderPath);
        }
    }

    private void RenameFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextItem(sender) is { } item && item.Tag is string folderPath)
        {
            BeginRenameFolder(item, folderPath);
        }
    }

    private void DeleteFolderMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextItem(sender) is { } item && item.Tag is string folderPath)
        {
            var folderName = Path.GetFileName(folderPath);
            var result = MessageBox.Show(
                $"フォルダ '{folderName}' とその中身を削除してもよろしいですか？\n\nこの操作は元に戻せません。",
                "フォルダの削除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // Close any templates from this folder that are open in tabs
                    var tabsToClose = _viewModel.Tabs
                        .Where(t => !string.IsNullOrEmpty(t.FilePath) &&
                               t.FilePath.StartsWith(folderPath + Path.DirectorySeparatorChar))
                        .ToList();

                    foreach (var tab in tabsToClose)
                    {
                        _viewModel.CloseTab(tab);
                    }

                    _templateService.DeleteTemplateFolder(folderPath);

                    // Remove from tree
                    if (item.Parent is TreeViewItem parentItem)
                        parentItem.Items.Remove(item);
                    else
                        _treeView.Items.Remove(item);

                    _viewModel.RefreshTemplatesCommand.Execute(null);
                    _viewModel.StatusBarViewModel.ShowMessage($"フォルダ '{folderName}' を削除しました。");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"削除に失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    private void OpenFolderInExplorerMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetContextItem(sender) is { } item && item.Tag is string folderPath)
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", $"\"{folderPath}\"");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"エクスプローラーを開けませんでした: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    // Rename methods
    private void BeginRenameTemplate(TreeViewItem item, TemplateInfo template)
    {
        var itemName = template.FileName;
        var itemNameWithoutExt = Path.GetFileNameWithoutExtension(itemName);
        bool isProcessed = false;

        var textBox = new TextBox
        {
            Text = itemName,
            Margin = new Thickness(0),
            Padding = new Thickness(2),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Colors.CornflowerBlue),
            Focusable = true
        };
        // テーマ対応のため、リソースから色を取得
        textBox.SetResourceReference(TextBox.BackgroundProperty, "TextBoxBackgroundBrush");
        textBox.SetResourceReference(TextBox.ForegroundProperty, "TextBoxForegroundBrush");

        textBox.KeyDown += (s, e) =>
        {
            if (isProcessed) return;

            if (e.Key == Key.Enter)
            {
                isProcessed = true;
                var newName = textBox.Text.Trim();
                if (!string.IsNullOrEmpty(newName) && newName != itemName)
                {
                    RenameTemplate(item, template, newName);
                }
                else
                {
                    item.Header = CreateHeaderWithIcon(template.DisplayName, false);
                }
                _treeView.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                isProcessed = true;
                item.Header = CreateHeaderWithIcon(template.DisplayName, false);
                _treeView.Focus();
                e.Handled = true;
            }
        };

        textBox.LostFocus += (s, e) =>
        {
            if (isProcessed) return;
            isProcessed = true;
            var newName = textBox.Text.Trim();
            if (!string.IsNullOrEmpty(newName) && newName != itemName)
            {
                RenameTemplate(item, template, newName);
            }
            else
            {
                item.Header = CreateHeaderWithIcon(template.DisplayName, false);
            }
        };

        item.Header = textBox;

        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            textBox.Focus();
            Keyboard.Focus(textBox);
            textBox.Select(0, itemNameWithoutExt.Length);
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void RenameTemplate(TreeViewItem item, TemplateInfo template, string newFileName)
    {
        try
        {
            if (!newFileName.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase))
            {
                newFileName = newFileName + ".tsv";
            }

            var directory = Path.GetDirectoryName(template.FullPath);
            if (string.IsNullOrEmpty(directory)) return;

            var newFilePath = Path.Combine(directory, newFileName);

            if (File.Exists(newFilePath) && !template.FullPath.Equals(newFilePath, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show($"'{newFileName}' は既に存在します。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                item.Header = CreateHeaderWithIcon(template.DisplayName, false);
                return;
            }

            File.Move(template.FullPath, newFilePath);

            // Update open tabs
            var openTab = _viewModel.Tabs.FirstOrDefault(t => t.FilePath == template.FullPath);
            if (openTab != null)
            {
                openTab.FilePath = newFilePath;
                openTab.Header = openTab.IsDirty ? $"{newFileName}*" : newFileName;
            }

            // Update item
            var newDisplayName = Path.GetFileNameWithoutExtension(newFileName);
            template.FileName = newFileName;
            template.FullPath = newFilePath;
            template.DisplayName = newDisplayName;
            item.Header = CreateHeaderWithIcon(newDisplayName, false);

            _viewModel.RefreshTemplatesCommand.Execute(null);
            _viewModel.StatusBarViewModel.ShowMessage($"Renamed: {newFileName}");
            _treeView.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"名前変更に失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            item.Header = CreateHeaderWithIcon(template.DisplayName, false);
            _treeView.Focus();
        }
    }

    private void BeginRenameFolder(TreeViewItem item, string folderPath)
    {
        var folderName = Path.GetFileName(folderPath);
        bool isProcessed = false;

        var textBox = new TextBox
        {
            Text = folderName,
            Margin = new Thickness(0),
            Padding = new Thickness(2),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Colors.CornflowerBlue),
            Focusable = true
        };
        // テーマ対応のため、リソースから色を取得
        textBox.SetResourceReference(TextBox.BackgroundProperty, "TextBoxBackgroundBrush");
        textBox.SetResourceReference(TextBox.ForegroundProperty, "TextBoxForegroundBrush");

        textBox.KeyDown += (s, e) =>
        {
            if (isProcessed) return;

            if (e.Key == Key.Enter)
            {
                isProcessed = true;
                var newName = textBox.Text.Trim();
                if (!string.IsNullOrEmpty(newName) && newName != folderName)
                {
                    RenameFolder(item, folderPath, newName);
                }
                else
                {
                    item.Header = CreateHeaderWithIcon(folderName, true);
                }
                _treeView.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                isProcessed = true;
                item.Header = CreateHeaderWithIcon(folderName, true);
                _treeView.Focus();
                e.Handled = true;
            }
        };

        textBox.LostFocus += (s, e) =>
        {
            if (isProcessed) return;
            isProcessed = true;
            var newName = textBox.Text.Trim();
            if (!string.IsNullOrEmpty(newName) && newName != folderName)
            {
                RenameFolder(item, folderPath, newName);
            }
            else
            {
                item.Header = CreateHeaderWithIcon(folderName, true);
            }
        };

        item.Header = textBox;

        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            textBox.Focus();
            Keyboard.Focus(textBox);
            textBox.SelectAll();
        }), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void RenameFolder(TreeViewItem item, string oldFolderPath, string newFolderName)
    {
        try
        {
            var newFolderPath = _templateService.RenameTemplateFolder(oldFolderPath, newFolderName);

            // Update open tabs that reference files in this folder
            foreach (var tab in _viewModel.Tabs)
            {
                if (!string.IsNullOrEmpty(tab.FilePath) && tab.FilePath.StartsWith(oldFolderPath))
                {
                    tab.FilePath = tab.FilePath.Replace(oldFolderPath, newFolderPath);
                }
            }

            item.Tag = newFolderPath;
            item.Header = CreateHeaderWithIcon(newFolderName, true);

            _viewModel.RefreshTemplatesCommand.Execute(null);
            _viewModel.StatusBarViewModel.ShowMessage($"Renamed folder: {newFolderName}");
            _treeView.Focus();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"名前変更に失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            item.Header = CreateHeaderWithIcon(Path.GetFileName(oldFolderPath), true);
            _treeView.Focus();
        }
    }

    // Create methods
    private void CreateNewTemplateInFolder(TreeViewItem parentItem, string folderPath)
    {
        try
        {
            string baseName = "NewTemplate";
            string templateName = baseName + ".tsv";
            int counter = 1;

            var existingTemplates = _templateService.GetTemplatesInDirectory(folderPath);
            while (existingTemplates.Any(t => t.FileName.Equals(templateName, StringComparison.OrdinalIgnoreCase)))
            {
                templateName = $"{baseName}{counter}.tsv";
                counter++;
            }

            var newTemplatePath = _templateService.CreateNewTemplateInDirectory(folderPath, templateName);

            // Expand parent and refresh
            if (!parentItem.IsExpanded)
            {
                parentItem.IsExpanded = true;
            }
            else
            {
                RefreshTreeNode(parentItem, folderPath);
            }

            // Find and select the new item
            var newItem = FindTreeItemByPath(parentItem, newTemplatePath);
            if (newItem != null)
            {
                newItem.IsSelected = true;
                var newTemplate = newItem.Tag as TemplateInfo;
                if (newTemplate != null)
                {
                    BeginRenameTemplate(newItem, newTemplate);
                }
            }

            _viewModel.RefreshTemplatesCommand.Execute(null);
            _viewModel.StatusBarViewModel.ShowMessage($"Created: {templateName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"テンプレートの作成に失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CreateNewFolder(TreeViewItem parentItem, string parentFolderPath)
    {
        try
        {
            string baseName = "NewFolder";
            string folderName = baseName;
            int counter = 1;

            var existingFolders = _templateService.GetSubdirectories(parentFolderPath);
            while (existingFolders.Any(f => Path.GetFileName(f).Equals(folderName, StringComparison.OrdinalIgnoreCase)))
            {
                folderName = $"{baseName}{counter}";
                counter++;
            }

            var newFolderPath = _templateService.CreateTemplateFolder(parentFolderPath, folderName);

            // Expand parent and refresh
            if (!parentItem.IsExpanded)
            {
                parentItem.IsExpanded = true;
            }
            else
            {
                RefreshTreeNode(parentItem, parentFolderPath);
            }

            // Find and select the new item
            var newItem = FindTreeItemByPath(parentItem, newFolderPath);
            if (newItem != null)
            {
                newItem.IsSelected = true;
                BeginRenameFolder(newItem, newFolderPath);
            }

            _viewModel.StatusBarViewModel.ShowMessage($"Created folder: {folderName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"フォルダの作成に失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// ツールバーから呼び出される新規テンプレート作成（ルートに作成）
    /// </summary>
    public void CreateNewTemplate()
    {
        try
        {
            var templateDir = _templateService.GetTemplateDirectoryPath();

            string baseName = "NewTemplate";
            string templateName = baseName + ".tsv";
            int counter = 1;

            var existingTemplates = _templateService.GetTemplatesInDirectory(templateDir);
            while (existingTemplates.Any(t => t.FileName.Equals(templateName, StringComparison.OrdinalIgnoreCase)))
            {
                templateName = $"{baseName}{counter}.tsv";
                counter++;
            }

            var newTemplatePath = _templateService.CreateNewTemplateInDirectory(templateDir, templateName);

            // Refresh tree
            PopulateTemplateTree();

            // Find and select the new item
            foreach (var item in _treeView.Items)
            {
                if (item is TreeViewItem treeItem && treeItem.Tag is TemplateInfo template)
                {
                    if (template.FullPath == newTemplatePath)
                    {
                        treeItem.IsSelected = true;
                        BeginRenameTemplate(treeItem, template);
                        break;
                    }
                }
            }

            _viewModel.RefreshTemplatesCommand.Execute(null);
            _viewModel.StatusBarViewModel.ShowMessage($"Created: {templateName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"テンプレートの作成に失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// ツールバーから呼び出される新規フォルダ作成（ルートに作成）
    /// </summary>
    public void CreateNewFolder()
    {
        try
        {
            var templateDir = _templateService.GetTemplateDirectoryPath();

            string baseName = "NewFolder";
            string folderName = baseName;
            int counter = 1;

            var existingFolders = _templateService.GetSubdirectories(templateDir);
            while (existingFolders.Any(f => Path.GetFileName(f).Equals(folderName, StringComparison.OrdinalIgnoreCase)))
            {
                folderName = $"{baseName}{counter}";
                counter++;
            }

            var newFolderPath = _templateService.CreateTemplateFolder(templateDir, folderName);

            // Refresh tree
            PopulateTemplateTree();

            // Find and select the new item
            foreach (var item in _treeView.Items)
            {
                if (item is TreeViewItem treeItem && treeItem.Tag is string folderPath)
                {
                    if (folderPath == newFolderPath)
                    {
                        treeItem.IsSelected = true;
                        BeginRenameFolder(treeItem, newFolderPath);
                        break;
                    }
                }
            }

            _viewModel.StatusBarViewModel.ShowMessage($"Created folder: {folderName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"フォルダの作成に失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void TemplateTreeView_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var clickedElement = e.OriginalSource as DependencyObject;
        var treeViewItem = FindVisualParent<TreeViewItem>(clickedElement);

        if (treeViewItem == null)
        {
            // Clicked on background - show context menu for creating in root
            var contextMenu = new ContextMenu();

            var newTemplateMenuItem = new MenuItem { Header = "新規テンプレート(_T)" };
            newTemplateMenuItem.Click += (s, args) => CreateNewTemplate();
            contextMenu.Items.Add(newTemplateMenuItem);

            var newFolderMenuItem = new MenuItem { Header = "新規フォルダ(_N)" };
            newFolderMenuItem.Click += (s, args) => CreateNewFolder();
            contextMenu.Items.Add(newFolderMenuItem);

            contextMenu.Items.Add(new Separator());

            var openFolderMenuItem = new MenuItem { Header = "Templateフォルダを開く(_E)" };
            openFolderMenuItem.Click += (s, args) =>
            {
                try
                {
                    var templateDir = _templateService.GetTemplateDirectoryPath();
                    System.Diagnostics.Process.Start("explorer.exe", $"\"{templateDir}\"");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"フォルダを開けませんでした: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            contextMenu.Items.Add(openFolderMenuItem);

            contextMenu.PlacementTarget = _treeView;
            contextMenu.IsOpen = true;
            e.Handled = true;
        }
    }

    // Helper methods
    private TreeViewItem? GetContextItem(object sender)
    {
        if (sender is MenuItem menuItem &&
            menuItem.Parent is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is TreeViewItem item)
        {
            return item;
        }
        return null;
    }

    private void RefreshTreeNode(TreeViewItem node, string directoryPath)
    {
        node.Items.Clear();
        PopulateTreeNode(node, directoryPath);
    }

    private TreeViewItem? FindTreeItemByPath(TreeViewItem parent, string targetPath)
    {
        foreach (var item in parent.Items)
        {
            if (item is TreeViewItem treeItem)
            {
                if (treeItem.Tag is TemplateInfo template && template.FullPath == targetPath)
                    return treeItem;
                if (treeItem.Tag is string folderPath && folderPath == targetPath)
                    return treeItem;
            }
        }
        return null;
    }

    private T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            child = VisualTreeHelper.GetParent(child);
            if (child is T parent)
                return parent;
        }
        return null;
    }
}
