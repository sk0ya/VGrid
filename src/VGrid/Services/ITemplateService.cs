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
