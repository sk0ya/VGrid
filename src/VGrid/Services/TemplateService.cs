using System.IO;
using System.Text.RegularExpressions;

namespace VGrid.Services;

/// <summary>
/// テンプレートファイル管理サービスの実装
/// </summary>
public class TemplateService : ITemplateService
{
    public TemplateService()
    {
        // 起動時にTemplateフォルダが存在しない場合は作成
        EnsureTemplateDirectoryExists();
    }

    /// <summary>
    /// Templateフォルダのパスを取得
    /// </summary>
    private string GetTemplateDirectory()
    {
        // 実行ファイルのディレクトリを取得
        var exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(exeDirectory, "Template");
    }

    /// <summary>
    /// Templateフォルダが存在しない場合は作成
    /// </summary>
    private void EnsureTemplateDirectoryExists()
    {
        var templateDir = GetTemplateDirectory();
        if (!Directory.Exists(templateDir))
        {
            Directory.CreateDirectory(templateDir);
        }
    }

    /// <summary>
    /// Templateフォルダが存在し、.tsvファイルがあるかチェック
    /// </summary>
    public bool HasTemplates()
    {
        var templates = GetAvailableTemplates();
        return templates.Count > 0;
    }

    /// <summary>
    /// 利用可能なテンプレートファイル一覧を取得
    /// </summary>
    public List<TemplateInfo> GetAvailableTemplates()
    {
        var templateDir = GetTemplateDirectory();

        if (!Directory.Exists(templateDir))
            return new List<TemplateInfo>();

        var tsvFiles = Directory.GetFiles(templateDir, "*.tsv");

        return tsvFiles.Select(path => new TemplateInfo
        {
            FileName = Path.GetFileName(path),
            DisplayName = GetDisplayName(Path.GetFileName(path)),
            FullPath = path
        })
        .OrderBy(t => t.DisplayName)
        .ToList();
    }

    /// <summary>
    /// ファイル名から表示名を生成
    /// </summary>
    /// <param name="fileName">ファイル名 (例: "CustomerTemplate.tsv")</param>
    /// <returns>表示名 (例: "Customer Template")</returns>
    private string GetDisplayName(string fileName)
    {
        // 拡張子を除去
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

        // CamelCaseをスペース区切りに変換
        // 例: "CustomerTemplate" → "Customer Template"
        var withSpaces = Regex.Replace(nameWithoutExt, "([a-z])([A-Z])", "$1 $2");

        return withSpaces;
    }

    /// <summary>
    /// テンプレートから新規ファイルを作成
    /// </summary>
    /// <param name="templateFileName">テンプレートファイル名</param>
    /// <param name="targetDirectory">作成先ディレクトリ</param>
    /// <returns>作成されたファイルのフルパス</returns>
    public string CreateFileFromTemplate(string templateFileName, string targetDirectory)
    {
        var templateDir = GetTemplateDirectory();
        var templatePath = Path.Combine(templateDir, templateFileName);

        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"Template not found: {templateFileName}");

        if (!Directory.Exists(targetDirectory))
            throw new DirectoryNotFoundException($"Target directory not found: {targetDirectory}");

        // ベースファイル名を生成（例: "CustomerTemplate.tsv" → "CustomerTemplate"）
        var baseFileName = Path.GetFileNameWithoutExtension(templateFileName);
        var extension = Path.GetExtension(templateFileName);

        // 一意なファイル名を生成（既存のCreateNewFileパターンを踏襲）
        string newFileName = $"{baseFileName}{extension}";
        string newFilePath = Path.Combine(targetDirectory, newFileName);
        int counter = 1;

        while (File.Exists(newFilePath))
        {
            newFileName = $"{baseFileName}{counter}{extension}";
            newFilePath = Path.Combine(targetDirectory, newFileName);
            counter++;
        }

        // テンプレートファイルの内容をコピー
        File.Copy(templatePath, newFilePath);

        return newFilePath;
    }

    /// <summary>
    /// 新しい空のテンプレートファイルを作成
    /// </summary>
    /// <param name="templateName">テンプレート名（拡張子なし可）</param>
    /// <returns>作成されたテンプレートのフルパス</returns>
    public string CreateNewTemplate(string templateName)
    {
        // テンプレート名のバリデーション
        if (string.IsNullOrWhiteSpace(templateName))
            throw new ArgumentException("Template name cannot be empty.", nameof(templateName));

        // 無効な文字をチェック
        var invalidChars = Path.GetInvalidFileNameChars();
        if (templateName.IndexOfAny(invalidChars) >= 0)
            throw new ArgumentException("Template name contains invalid characters.", nameof(templateName));

        // .tsv拡張子がない場合は追加
        if (!templateName.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase))
        {
            templateName = templateName + ".tsv";
        }

        var templateDir = GetTemplateDirectory();
        var templatePath = Path.Combine(templateDir, templateName);

        // 既存ファイルと重複チェック
        if (File.Exists(templatePath))
            throw new IOException($"A template with this name already exists: {templateName}");

        // 空のTSVファイルを作成（ヘッダー行付き）
        var initialContent = "Column1\tColumn2\tColumn3";
        File.WriteAllText(templatePath, initialContent);

        return templatePath;
    }

    /// <summary>
    /// テンプレートファイルを削除
    /// </summary>
    /// <param name="templateFileName">削除するテンプレートファイル名</param>
    public void DeleteTemplate(string templateFileName)
    {
        var templateDir = GetTemplateDirectory();
        var templatePath = Path.Combine(templateDir, templateFileName);

        // ファイルが存在する場合のみ削除
        if (File.Exists(templatePath))
        {
            File.Delete(templatePath);
        }
    }

    /// <summary>
    /// ファイルパスがTemplateディレクトリ内かチェック
    /// </summary>
    /// <param name="filePath">チェックするファイルパス</param>
    /// <returns>Templateディレクトリ内の場合true</returns>
    public bool IsTemplateFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        try
        {
            // 両方のパスを正規化
            var normalizedFilePath = Path.GetFullPath(filePath);
            var templateDir = Path.GetFullPath(GetTemplateDirectory());

            // ファイルパスがTemplateディレクトリで始まるか比較（大文字小文字を区別しない）
            return normalizedFilePath.StartsWith(templateDir, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Templateディレクトリのパスを取得
    /// </summary>
    /// <returns>Templateディレクトリのフルパス</returns>
    public string GetTemplateDirectoryPath()
    {
        return GetTemplateDirectory();
    }
}
