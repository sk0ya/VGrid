using System.Text.Json.Serialization;

namespace VGrid.Models;

/// <summary>
/// テンプレートセット（複数のテンプレートを一括作成するための設定）
/// </summary>
public class TemplateSet
{
    /// <summary>
    /// セットの表示名
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// JSONファイルのパス
    /// </summary>
    [JsonIgnore]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// テンプレート一覧
    /// </summary>
    [JsonPropertyName("templates")]
    public List<TemplateSetItem> Templates { get; set; } = new();
}

/// <summary>
/// テンプレートセット内の個々のテンプレート定義
/// </summary>
public class TemplateSetItem
{
    /// <summary>
    /// 元となるテンプレートファイル（同じフォルダ内の相対パス）
    /// </summary>
    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;

    /// <summary>
    /// 出力ファイル名（{0}, {1} 等のプレースホルダー対応）
    /// 省略時はテンプレートファイル名をそのまま使用
    /// </summary>
    [JsonPropertyName("outputName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OutputName { get; set; }
}
