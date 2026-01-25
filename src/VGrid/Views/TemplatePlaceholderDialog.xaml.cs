using System.Windows;
using System.Windows.Controls;
using VGrid.Services;

namespace VGrid.Views;

/// <summary>
/// テンプレートからファイル作成時のプレースホルダー入力ダイアログ
/// </summary>
public partial class TemplatePlaceholderDialog : Window
{
    private readonly ITemplateService _templateService;
    private readonly string _templateFileName;
    private readonly string _targetDirectory;
    private readonly int _placeholderCount;
    private readonly List<TextBox> _placeholderTextBoxes = new();

    /// <summary>
    /// 作成されたファイルのパス
    /// </summary>
    public string? CreatedFilePath { get; private set; }

    public TemplatePlaceholderDialog(
        ITemplateService templateService,
        string templateFileName,
        string templateDisplayName,
        string targetDirectory,
        int placeholderCount)
    {
        InitializeComponent();

        _templateService = templateService;
        _templateFileName = templateFileName;
        _targetDirectory = targetDirectory;
        _placeholderCount = placeholderCount;

        // テンプレート名を表示
        TemplateNameText.Text = templateDisplayName;

        // プレースホルダー入力欄を生成
        CreatePlaceholderInputs(placeholderCount);

        // 初期プレビューを更新
        UpdatePreview();
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

        // 最初のテキストボックスにフォーカス
        if (_placeholderTextBoxes.Count > 0)
        {
            Loaded += (s, e) => _placeholderTextBoxes[0].Focus();
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
        var previewFileName = _templateService.ApplyPlaceholdersToFileName(
            System.IO.Path.GetFileName(_templateFileName),
            placeholders);
        PreviewText.Text = previewFileName;

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

            // ファイルを作成
            CreatedFilePath = _templateService.CreateFileFromTemplateWithPlaceholders(
                _templateFileName,
                _targetDirectory,
                placeholders);

            DialogResult = true;
            Close();
        }
        catch (System.IO.IOException ex)
        {
            MessageBox.Show(
                $"ファイルの作成に失敗しました:\n{ex.Message}",
                "エラー",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
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
