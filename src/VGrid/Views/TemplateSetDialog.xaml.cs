using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using VGrid.Models;
using VGrid.Services;

namespace VGrid.Views;

/// <summary>
/// テンプレートセットから一括ファイル作成を行うダイアログ
/// </summary>
public partial class TemplateSetDialog : Window
{
    private readonly TemplateSet _templateSet;
    private readonly ITemplateService _templateService;
    private readonly string _targetDirectory;
    private readonly List<TextBox> _placeholderTextBoxes = new();
    private readonly int _placeholderCount;

    /// <summary>
    /// 作成されたファイルのパスリスト
    /// </summary>
    public List<string> CreatedFiles { get; private set; } = new();

    public TemplateSetDialog(TemplateSet templateSet, ITemplateService templateService, string? targetDirectory = null)
    {
        InitializeComponent();

        _templateSet = templateSet;
        _templateService = templateService;
        _targetDirectory = targetDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        // セット名を表示
        SetNameText.Text = templateSet.Name;

        // プレースホルダーの数を検出
        _placeholderCount = DetectPlaceholderCount(templateSet);

        // プレースホルダー入力欄を生成
        CreatePlaceholderInputs(_placeholderCount);

        // 初期プレビューを更新
        UpdatePreview();
    }

    /// <summary>
    /// テンプレートセット内のプレースホルダー数を検出
    /// </summary>
    private int DetectPlaceholderCount(TemplateSet set)
    {
        var maxPlaceholder = -1;
        var regex = new Regex(@"\{(\d+)\}");

        foreach (var template in set.Templates)
        {
            if (string.IsNullOrEmpty(template.OutputName))
                continue;

            var matches = regex.Matches(template.OutputName);
            foreach (Match match in matches)
            {
                if (int.TryParse(match.Groups[1].Value, out int index))
                {
                    maxPlaceholder = Math.Max(maxPlaceholder, index);
                }
            }
        }

        return maxPlaceholder + 1; // 0-indexed なので +1
    }

    /// <summary>
    /// プレースホルダー入力欄を生成
    /// </summary>
    private void CreatePlaceholderInputs(int count)
    {
        _placeholderTextBoxes.Clear();
        PlaceholderInputsPanel.Children.Clear();

        if (count == 0)
        {
            return;
        }

        var label = new TextBlock
        {
            Text = "プレースホルダー入力",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        };
        label.SetResourceReference(ForegroundProperty, "TextBoxForegroundBrush");
        PlaceholderInputsPanel.Children.Add(label);

        for (int i = 0; i < count; i++)
        {
            var grid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 8)
            };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelText = new TextBlock
            {
                Text = $"{{{i}}}:",
                VerticalAlignment = VerticalAlignment.Center
            };
            labelText.SetResourceReference(ForegroundProperty, "TextBoxForegroundBrush");
            Grid.SetColumn(labelText, 0);

            var textBox = new TextBox
            {
                Padding = new Thickness(8, 6, 8, 6),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            textBox.SetResourceReference(BackgroundProperty, "TextBoxBackgroundBrush");
            textBox.SetResourceReference(ForegroundProperty, "TextBoxForegroundBrush");
            textBox.SetResourceReference(BorderBrushProperty, "ButtonBorderBrush");
            textBox.TextChanged += PlaceholderTextBox_TextChanged;
            Grid.SetColumn(textBox, 1);

            grid.Children.Add(labelText);
            grid.Children.Add(textBox);
            PlaceholderInputsPanel.Children.Add(grid);

            _placeholderTextBoxes.Add(textBox);
        }
    }

    /// <summary>
    /// プレースホルダーの値が変更されたときにプレビューを更新
    /// </summary>
    private void PlaceholderTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdatePreview();
    }

    /// <summary>
    /// プレビューを更新
    /// </summary>
    private void UpdatePreview()
    {
        var placeholders = _placeholderTextBoxes.Select(tb => tb.Text).ToArray();
        var preview = _templateService.PreviewFileNames(_templateSet, placeholders);
        TemplatesListBox.ItemsSource = preview;

        // 作成ボタンの有効/無効を更新
        UpdateCreateButtonState();
    }

    /// <summary>
    /// 作成ボタンの有効/無効を更新
    /// </summary>
    private void UpdateCreateButtonState()
    {
        // プレースホルダーがすべて入力されているかチェック
        var allPlaceholdersFilled = _placeholderTextBoxes.All(tb => !string.IsNullOrWhiteSpace(tb.Text));
        CreateButton.IsEnabled = _placeholderCount == 0 || allPlaceholdersFilled;
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var placeholders = _placeholderTextBoxes.Select(tb => tb.Text).ToArray();

            // ファイル名の重複チェック
            var previewFileNames = _templateService.PreviewFileNames(_templateSet, placeholders);
            foreach (var fileName in previewFileNames)
            {
                var targetPath = Path.Combine(_targetDirectory, fileName);
                if (File.Exists(targetPath))
                {
                    var result = MessageBox.Show(
                        $"ファイル '{fileName}' は既に存在します。上書きしますか？",
                        "確認",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Cancel)
                    {
                        return;
                    }
                    else if (result == MessageBoxResult.No)
                    {
                        MessageBox.Show("作成をキャンセルしました。", "キャンセル", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }
                    // Yes の場合は上書きを許可するため、既存ファイルを削除
                    File.Delete(targetPath);
                }
            }

            CreatedFiles = _templateService.CreateFilesFromSet(_templateSet, _targetDirectory, placeholders);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"ファイルの作成に失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
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
}
