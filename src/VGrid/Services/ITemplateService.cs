using VGrid.Models;

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

    /// <summary>
    /// テンプレートファイルの名前を変更
    /// </summary>
    /// <param name="oldFileName">変更前のファイル名</param>
    /// <param name="newFileName">変更後のファイル名</param>
    void RenameTemplate(string oldFileName, string newFileName);

    /// <summary>
    /// 指定ディレクトリ内のテンプレートファイル一覧を取得
    /// </summary>
    /// <param name="directoryPath">ディレクトリのフルパス</param>
    /// <returns>テンプレート情報のリスト</returns>
    List<TemplateInfo> GetTemplatesInDirectory(string directoryPath);

    /// <summary>
    /// 指定ディレクトリ内のサブディレクトリ一覧を取得
    /// </summary>
    /// <param name="directoryPath">ディレクトリのフルパス</param>
    /// <returns>サブディレクトリのフルパスリスト</returns>
    List<string> GetSubdirectories(string directoryPath);

    /// <summary>
    /// 指定ディレクトリ内に新しいテンプレートを作成
    /// </summary>
    /// <param name="directoryPath">作成先ディレクトリのフルパス</param>
    /// <param name="templateName">テンプレート名</param>
    /// <returns>作成されたテンプレートのフルパス</returns>
    string CreateNewTemplateInDirectory(string directoryPath, string templateName);

    /// <summary>
    /// 指定ディレクトリ内に新しいフォルダを作成
    /// </summary>
    /// <param name="parentDirectoryPath">親ディレクトリのフルパス</param>
    /// <param name="folderName">フォルダ名</param>
    /// <returns>作成されたフォルダのフルパス</returns>
    string CreateTemplateFolder(string parentDirectoryPath, string folderName);

    /// <summary>
    /// テンプレートフォルダを削除
    /// </summary>
    /// <param name="folderPath">削除するフォルダのフルパス</param>
    void DeleteTemplateFolder(string folderPath);

    /// <summary>
    /// テンプレートフォルダの名前を変更
    /// </summary>
    /// <param name="oldFolderPath">変更前のフォルダパス</param>
    /// <param name="newFolderName">新しいフォルダ名</param>
    /// <returns>変更後のフォルダパス</returns>
    string RenameTemplateFolder(string oldFolderPath, string newFolderName);

    /// <summary>
    /// 指定パスがTemplateディレクトリ内のフォルダかチェック
    /// </summary>
    /// <param name="folderPath">チェックするフォルダパス</param>
    /// <returns>Templateディレクトリ内のフォルダの場合true</returns>
    bool IsTemplateFolder(string folderPath);

    /// <summary>
    /// テンプレートセット（JSONファイル）一覧を取得
    /// </summary>
    /// <returns>テンプレートセットのリスト</returns>
    IEnumerable<TemplateSet> GetTemplateSets();

    /// <summary>
    /// 指定ディレクトリ内のテンプレートセット一覧を取得
    /// </summary>
    /// <param name="directoryPath">ディレクトリのフルパス</param>
    /// <returns>テンプレートセットのリスト</returns>
    List<TemplateSet> GetTemplateSetsInDirectory(string directoryPath);

    /// <summary>
    /// テンプレートセットを読み込む
    /// </summary>
    /// <param name="jsonPath">JSONファイルのパス</param>
    /// <returns>テンプレートセット（読み込み失敗時はnull）</returns>
    TemplateSet? LoadTemplateSet(string jsonPath);

    /// <summary>
    /// テンプレートセットから複数ファイルを一括作成
    /// </summary>
    /// <param name="set">テンプレートセット</param>
    /// <param name="targetDirectory">作成先ディレクトリ</param>
    /// <param name="placeholders">プレースホルダーの値（{0}, {1}, ...）</param>
    /// <returns>作成されたファイルのパスリスト</returns>
    List<string> CreateFilesFromSet(TemplateSet set, string targetDirectory, params string[] placeholders);

    /// <summary>
    /// テンプレートセットから作成されるファイル名をプレビュー
    /// </summary>
    /// <param name="set">テンプレートセット</param>
    /// <param name="placeholders">プレースホルダーの値</param>
    /// <returns>ファイル名のリスト</returns>
    List<string> PreviewFileNames(TemplateSet set, params string[] placeholders);

    /// <summary>
    /// テンプレートファイル名からプレースホルダー数を検出
    /// {{0}} のようなエスケープされたブレースは無視する
    /// </summary>
    /// <param name="templateFileName">テンプレートファイル名</param>
    /// <returns>プレースホルダーの数（0-indexed の最大値 + 1）</returns>
    int DetectPlaceholderCount(string templateFileName);

    /// <summary>
    /// テンプレートファイル名にプレースホルダーを適用
    /// </summary>
    /// <param name="templateFileName">テンプレートファイル名</param>
    /// <param name="placeholders">プレースホルダー値の配列</param>
    /// <returns>プレースホルダーが適用されたファイル名</returns>
    string ApplyPlaceholdersToFileName(string templateFileName, params string[] placeholders);

    /// <summary>
    /// テンプレートから新規ファイルを作成（プレースホルダー対応版）
    /// </summary>
    /// <param name="templateFileName">テンプレートファイル名（サブフォルダからの相対パス可）</param>
    /// <param name="targetDirectory">作成先ディレクトリ</param>
    /// <param name="placeholders">プレースホルダー値の配列</param>
    /// <returns>作成されたファイルのフルパス</returns>
    string CreateFileFromTemplateWithPlaceholders(string templateFileName, string targetDirectory, params string[] placeholders);

    /// <summary>
    /// テンプレートセット内のプレースホルダー数を検出
    /// </summary>
    /// <param name="set">テンプレートセット</param>
    /// <returns>プレースホルダーの数（0-indexed の最大値 + 1）</returns>
    int DetectPlaceholderCountInSet(TemplateSet set);
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
