namespace VGrid.Services;

/// <summary>
/// テンプレートファイル管理サービスのインターフェース
/// </summary>
public interface ITemplateService
{
    /// <summary>
    /// Templateフォルダが存在し、.tsvファイルがあるかチェック
    /// </summary>
    bool HasTemplates();

    /// <summary>
    /// 利用可能なテンプレートファイル一覧を取得
    /// </summary>
    List<TemplateInfo> GetAvailableTemplates();

    /// <summary>
    /// テンプレートから新規ファイルを作成
    /// </summary>
    /// <param name="templateFileName">テンプレートファイル名</param>
    /// <param name="targetDirectory">作成先ディレクトリ</param>
    /// <returns>作成されたファイルのフルパス</returns>
    string CreateFileFromTemplate(string templateFileName, string targetDirectory);

    /// <summary>
    /// 新しい空のテンプレートファイルを作成
    /// </summary>
    /// <param name="templateName">テンプレート名（拡張子なし可）</param>
    /// <returns>作成されたテンプレートのフルパス</returns>
    string CreateNewTemplate(string templateName);

    /// <summary>
    /// テンプレートファイルを削除
    /// </summary>
    /// <param name="templateFileName">削除するテンプレートファイル名</param>
    void DeleteTemplate(string templateFileName);

    /// <summary>
    /// ファイルパスがTemplateディレクトリ内かチェック
    /// </summary>
    /// <param name="filePath">チェックするファイルパス</param>
    /// <returns>Templateディレクトリ内の場合true</returns>
    bool IsTemplateFile(string filePath);

    /// <summary>
    /// Templateディレクトリのパスを取得
    /// </summary>
    /// <returns>Templateディレクトリのフルパス</returns>
    string GetTemplateDirectoryPath();
}

/// <summary>
/// テンプレート情報
/// </summary>
public class TemplateInfo
{
    public string FileName { get; set; } = string.Empty;      // "CustomerTemplate.tsv"
    public string DisplayName { get; set; } = string.Empty;   // "Customer Template"
    public string FullPath { get; set; } = string.Empty;
}
