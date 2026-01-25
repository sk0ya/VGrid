using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VGrid.Models;
using VGrid.Services;

namespace VGrid.Views;

/// <summary>
/// テンプレートセットを新規作成・編集するダイアログ
/// </summary>
public partial class CreateTemplateSetDialog : Window
{
    private readonly ITemplateService _templateService;
    private readonly string _targetDirectory;
    private readonly List<SelectedTemplateItem> _selectedTemplates = new();
    private readonly bool _isEditMode;
    private readonly TemplateSet? _existingTemplateSet;

    /// <summary>
    /// 作成されたテンプレートセットファイルのパス
    /// </summary>
    public string? CreatedFilePath { get; private set; }

    /// <summary>
    /// 選択されたテンプレートの情報を保持するクラス
    /// </summary>
    private class SelectedTemplateItem
    {
        public TemplateInfo Template { get; set; } = null!;
        public TextBox OutputNameTextBox { get; set; } = null!;
        public Grid RowElement { get; set; } = null!;
        public TreeViewItem TreeViewItem { get; set; } = null!;
    }

    /// <summary>
    /// 新規作成モード用コンストラクタ
    /// </summary>
    public CreateTemplateSetDialog(ITemplateService templateService, string targetDirectory)
        : this(templateService, targetDirectory, null)
    {
    }

    /// <summary>
    /// 編集モード用コンストラクタ
    /// </summary>
    public CreateTemplateSetDialog(ITemplateService templateService, string targetDirectory, TemplateSet? existingTemplateSet)
    {
        InitializeComponent();

        _templateService = templateService;
        _targetDirectory = targetDirectory;
        _existingTemplateSet = existingTemplateSet;
        _isEditMode = existingTemplateSet != null;

        // テンプレートツリーを構築
        PopulateTemplateTree();

        if (_isEditMode && existingTemplateSet != null)
        {
            // 編集モード: 既存の値を設定
            Title = "テンプレートセットの編集";
            TitleBarText.Text = "テンプレートセットの編集";
            SetNameTextBox.Text = existingTemplateSet.Name;
            CreateButton.Content = "保存";

            // 既存のテンプレートを選択状態にする
            LoadExistingTemplates(existingTemplateSet);
        }
        else
        {
            // 新規作成モード
            SetNameTextBox.Text = "新規セット";
        }

        SetNameTextBox.SelectAll();
        SetNameTextBox.Focus();
    }

    /// <summary>
    /// 既存のテンプレートセットからテンプレートを読み込み
    /// </summary>
    private void LoadExistingTemplates(TemplateSet templateSet)
    {
        if (templateSet.Templates == null) return;

        var templateRootDir = _templateService.GetTemplateDirectoryPath();

        foreach (var item in templateSet.Templates)
        {
            // テンプレートファイルのフルパスを取得
            var templatePath = Path.Combine(templateRootDir, item.File);
            if (!File.Exists(templatePath)) continue;

            // ツリービューから該当するTreeViewItemを探す
            var treeViewItem = FindTreeViewItemByPath(TemplateTreeView, templatePath);
            if (treeViewItem?.Tag is TemplateInfo template)
            {
                AddTemplateToSelection(template, treeViewItem, item.OutputName);
            }
        }
    }

    /// <summary>
    /// パスに一致するTreeViewItemを再帰的に検索
    /// </summary>
    private TreeViewItem? FindTreeViewItemByPath(ItemsControl parent, string targetPath)
    {
        foreach (var item in parent.Items)
        {
            if (item is TreeViewItem treeViewItem)
            {
                if (treeViewItem.Tag is TemplateInfo template && template.FullPath == targetPath)
                {
                    return treeViewItem;
                }

                // 子アイテムを再帰検索
                var found = FindTreeViewItemByPath(treeViewItem, targetPath);
                if (found != null) return found;
            }
        }
        return null;
    }

    /// <summary>
    /// テンプレートツリーを構築
    /// </summary>
    private void PopulateTemplateTree()
    {
        TemplateTreeView.Items.Clear();
        var templateDir = _templateService.GetTemplateDirectoryPath();
        PopulateTreeNode(null, templateDir);
    }

    /// <summary>
    /// TreeViewノードを再帰的に構築
    /// </summary>
    private void PopulateTreeNode(TreeViewItem? parentItem, string directoryPath)
    {
        var templateRootDir = _templateService.GetTemplateDirectoryPath();

        // Add subdirectories
        var subdirectories = _templateService.GetSubdirectories(directoryPath);
        foreach (var subdir in subdirectories)
        {
            var folderName = Path.GetFileName(subdir);
            var folderItem = new TreeViewItem
            {
                Header = CreateHeaderWithIcon(folderName, isFolder: true),
                Tag = subdir,
                IsExpanded = true
            };

            if (parentItem == null)
                TemplateTreeView.Items.Add(folderItem);
            else
                parentItem.Items.Add(folderItem);

            // Recurse into subdirectory
            PopulateTreeNode(folderItem, subdir);
        }

        // Add templates
        var templates = _templateService.GetTemplatesInDirectory(directoryPath);
        foreach (var template in templates)
        {
            // FileNameをTemplateフォルダからの相対パスに修正
            template.FileName = Path.GetRelativePath(templateRootDir, template.FullPath);

            var templateItem = new TreeViewItem
            {
                Header = CreateHeaderWithIcon(template.DisplayName, isFolder: false),
                Tag = template
            };

            if (parentItem == null)
                TemplateTreeView.Items.Add(templateItem);
            else
                parentItem.Items.Add(templateItem);
        }
    }

    private object CreateHeaderWithIcon(string text, bool isFolder, bool isSelected = false)
    {
        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };

        // チェックマーク（選択済みの場合）
        if (isSelected)
        {
            var checkMark = new TextBlock
            {
                Text = "✓ ",
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(76, 175, 80)), // Green
                VerticalAlignment = VerticalAlignment.Center
            };
            stackPanel.Children.Add(checkMark);
        }

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
        textBlock.SetResourceReference(ForegroundProperty, "ListBoxForegroundBrush");
        stackPanel.Children.Add(textBlock);

        return stackPanel;
    }

    private void TemplateTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Find the TreeViewItem that was double-clicked
        var clickedElement = e.OriginalSource as DependencyObject;
        var treeViewItem = FindVisualParent<TreeViewItem>(clickedElement);

        if (treeViewItem?.Tag is TemplateInfo template)
        {
            // 既に選択済みの場合は解除
            var existingItem = _selectedTemplates.FirstOrDefault(s => s.Template.FullPath == template.FullPath);
            if (existingItem != null)
            {
                RemoveTemplateFromSelection(existingItem);
            }
            else
            {
                AddTemplateToSelection(template, treeViewItem);
            }
            e.Handled = true;
        }
    }

    private void RemoveTemplateFromSelection(SelectedTemplateItem item)
    {
        _selectedTemplates.Remove(item);
        SelectedTemplatesPanel.Children.Remove(item.RowElement);
        // ツリーのヘッダーを元に戻す
        item.TreeViewItem.Header = CreateHeaderWithIcon(item.Template.DisplayName, isFolder: false, isSelected: false);
        UpdateCreateButtonState();
    }

    private void AddTemplateToSelection(TemplateInfo template, TreeViewItem treeViewItem)
    {
        AddTemplateToSelection(template, treeViewItem, null);
    }

    private void AddTemplateToSelection(TemplateInfo template, TreeViewItem treeViewItem, string? initialOutputName)
    {
        // Check if already added
        if (_selectedTemplates.Any(s => s.Template.FullPath == template.FullPath))
        {
            return;
        }

        // ツリーのヘッダーを選択済みに更新
        treeViewItem.Header = CreateHeaderWithIcon(template.DisplayName, isFolder: false, isSelected: true);

        // Create row for selected template
        var grid = new Grid
        {
            Margin = new Thickness(0, 2, 0, 2)
        };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });

        // Template file name
        var fileNameText = new TextBlock
        {
            Text = template.FileName,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = template.FileName
        };
        fileNameText.SetResourceReference(ForegroundProperty, "ListBoxForegroundBrush");
        Grid.SetColumn(fileNameText, 0);
        grid.Children.Add(fileNameText);

        // Arrow
        var arrowText = new TextBlock
        {
            Text = "→",
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        arrowText.SetResourceReference(ForegroundProperty, "ListBoxForegroundBrush");
        Grid.SetColumn(arrowText, 1);
        grid.Children.Add(arrowText);

        // Output name text box
        var outputTextBox = new TextBox
        {
            Text = initialOutputName ?? "",
            Padding = new Thickness(4, 2, 4, 2),
            VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "出力ファイル名（空欄の場合は元のパスを使用）"
        };
        outputTextBox.SetResourceReference(BackgroundProperty, "TextBoxBackgroundBrush");
        outputTextBox.SetResourceReference(ForegroundProperty, "TextBoxForegroundBrush");
        outputTextBox.SetResourceReference(BorderBrushProperty, "ButtonBorderBrush");
        Grid.SetColumn(outputTextBox, 2);
        grid.Children.Add(outputTextBox);

        // Remove button
        var removeButton = new Button
        {
            Content = "×",
            Width = 20,
            Height = 20,
            Padding = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Cursor = Cursors.Hand,
            ToolTip = "削除"
        };
        removeButton.SetResourceReference(BackgroundProperty, "ButtonBackgroundBrush");
        removeButton.SetResourceReference(ForegroundProperty, "ButtonForegroundBrush");
        removeButton.SetResourceReference(BorderBrushProperty, "ButtonBorderBrush");
        Grid.SetColumn(removeButton, 3);
        grid.Children.Add(removeButton);

        var selectedItem = new SelectedTemplateItem
        {
            Template = template,
            OutputNameTextBox = outputTextBox,
            RowElement = grid,
            TreeViewItem = treeViewItem
        };

        removeButton.Click += (s, e) => RemoveTemplateFromSelection(selectedItem);

        _selectedTemplates.Add(selectedItem);
        SelectedTemplatesPanel.Children.Add(grid);
        UpdateCreateButtonState();
    }

    private void SetNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateCreateButtonState();
    }

    private void UpdateCreateButtonState()
    {
        var hasName = !string.IsNullOrWhiteSpace(SetNameTextBox.Text);
        var hasSelectedTemplates = _selectedTemplates.Count > 0;
        CreateButton.IsEnabled = hasName && hasSelectedTemplates;
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var setName = SetNameTextBox.Text.Trim();
            string filePath;

            if (_isEditMode && _existingTemplateSet != null && !string.IsNullOrEmpty(_existingTemplateSet.FilePath))
            {
                // 編集モード: 既存ファイルを上書き
                filePath = _existingTemplateSet.FilePath;
            }
            else
            {
                // 新規作成モード: 新しいファイル名を生成
                var invalidChars = Path.GetInvalidFileNameChars();
                var safeFileName = string.Join("", setName.Select(c => invalidChars.Contains(c) ? '_' : c));
                var fileName = safeFileName + ".json";
                filePath = Path.Combine(_targetDirectory, fileName);

                // 既存ファイルのチェック
                int counter = 1;
                while (File.Exists(filePath))
                {
                    fileName = $"{safeFileName}{counter}.json";
                    filePath = Path.Combine(_targetDirectory, fileName);
                    counter++;
                }
            }

            // テンプレートセットを作成
            var templateSet = new TemplateSet
            {
                Name = setName,
                Templates = _selectedTemplates.Select(item =>
                {
                    var outputName = item.OutputNameTextBox.Text.Trim();
                    return new TemplateSetItem
                    {
                        File = item.Template.FileName,
                        OutputName = string.IsNullOrEmpty(outputName) ? null : outputName
                    };
                }).ToList()
            };

            // JSONファイルを作成/更新
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(templateSet, options);
            File.WriteAllText(filePath, json);

            CreatedFilePath = filePath;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            var actionName = _isEditMode ? "更新" : "作成";
            MessageBox.Show($"テンプレートセットの{actionName}に失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
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
