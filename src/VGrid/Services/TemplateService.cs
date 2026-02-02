using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using VGrid.Models;

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
    /// 利用可能なテンプレートファイル一覧を取得（サブフォルダ含む）
    /// </summary>
    public List<TemplateInfo> GetAvailableTemplates()
    {
        var templateDir = GetTemplateDirectory();

        if (!Directory.Exists(templateDir))
            return new List<TemplateInfo>();

        var result = new List<TemplateInfo>();
        GetTemplatesRecursively(templateDir, templateDir, result);

        return result.OrderBy(t => t.DisplayName).ToList();
    }

    /// <summary>
    /// テンプレートを再帰的に取得
    /// </summary>
    private void GetTemplatesRecursively(string rootDir, string currentDir, List<TemplateInfo> result)
    {
        // Get templates in current directory (TSV and CSV)
        var tsvFiles = Directory.GetFiles(currentDir, "*.tsv")
            .Concat(Directory.GetFiles(currentDir, "*.csv"))
            .ToArray();
        foreach (var path in tsvFiles)
        {
            var fileName = Path.GetFileName(path);
            var relativePath = Path.GetRelativePath(rootDir, path);
            var relativeDir = Path.GetDirectoryName(relativePath);

            // Display name includes folder path if in subdirectory
            string displayName;
            if (string.IsNullOrEmpty(relativeDir))
            {
                displayName = GetDisplayName(fileName);
            }
            else
            {
                displayName = $"{relativeDir}/{GetDisplayName(fileName)}";
            }

            result.Add(new TemplateInfo
            {
                FileName = relativePath,  // Use relative path for subdirectory templates
                DisplayName = displayName,
                FullPath = path
            });
        }

        // Recursively process subdirectories
        var subdirectories = Directory.GetDirectories(currentDir);
        foreach (var subdir in subdirectories)
        {
            GetTemplatesRecursively(rootDir, subdir, result);
        }
    }

    /// <summary>
    /// ファイル名から表示名を生成
    /// </summary>
    /// <param name="fileName">ファイル名 (例: "CustomerTemplate.tsv")</param>
    /// <returns>表示名 (例: "Customer Template.tsv")</returns>
    private string GetDisplayName(string fileName)
    {
        // 拡張子を保持
        var ext = Path.GetExtension(fileName);
        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

        // CamelCaseをスペース区切りに変換
        // 例: "CustomerTemplate" → "Customer Template"
        var withSpaces = Regex.Replace(nameWithoutExt, "([a-z])([A-Z])", "$1 $2");

        return withSpaces + ext;
    }

    /// <summary>
    /// テンプレートから新規ファイルを作成
    /// </summary>
    /// <param name="templateFileName">テンプレートファイル名（サブフォルダからの相対パス可）</param>
    /// <param name="targetDirectory">作成先ディレクトリ</param>
    /// <returns>作成されたファイルのフルパス</returns>
    public string CreateFileFromTemplate(string templateFileName, string targetDirectory)
    {
        var templateDir = GetTemplateDirectory();
        // パスを正規化して結合
        var templatePath = Path.GetFullPath(Path.Combine(templateDir, templateFileName));

        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"Template not found: {templateFileName}");

        // ターゲットディレクトリも正規化
        targetDirectory = Path.GetFullPath(targetDirectory);

        if (!Directory.Exists(targetDirectory))
            throw new DirectoryNotFoundException($"Target directory not found: {targetDirectory}");

        // ベースファイル名を生成
        // サブフォルダパスが含まれている場合も正しくファイル名のみを取得
        // 例: "日本語フォルダ\CustomerTemplate.tsv" → "CustomerTemplate"
        var templateFileNameOnly = Path.GetFileName(templateFileName);
        var baseFileName = Path.GetFileNameWithoutExtension(templateFileNameOnly);
        var extension = Path.GetExtension(templateFileNameOnly);

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
        if (!templateName.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) &&
            !templateName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            templateName = templateName + ".tsv";
        }

        var templateDir = GetTemplateDirectory();
        var templatePath = Path.Combine(templateDir, templateName);

        // 既存ファイルと重複チェック
        if (File.Exists(templatePath))
            throw new IOException($"A template with this name already exists: {templateName}");

        // 空のファイルを作成（ヘッダー行付き）
        var format = DelimiterStrategyFactory.DetectFromExtension(templatePath);
        var strategy = DelimiterStrategyFactory.Create(format);
        var initialContent = strategy.FormatLine(new[] { "Column1", "Column2", "Column3" });
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

    /// <summary>
    /// テンプレートファイルの名前を変更
    /// </summary>
    /// <param name="oldFileName">変更前のファイル名</param>
    /// <param name="newFileName">変更後のファイル名</param>
    public void RenameTemplate(string oldFileName, string newFileName)
    {
        // 新しいファイル名のバリデーション
        if (string.IsNullOrWhiteSpace(newFileName))
            throw new ArgumentException("Template name cannot be empty.", nameof(newFileName));

        // 無効な文字をチェック
        var invalidChars = Path.GetInvalidFileNameChars();
        if (newFileName.IndexOfAny(invalidChars) >= 0)
            throw new ArgumentException("Template name contains invalid characters.", nameof(newFileName));

        // 拡張子がない場合は.tsvを追加
        if (!newFileName.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) &&
            !newFileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            newFileName = newFileName + ".tsv";
        }

        var templateDir = GetTemplateDirectory();
        var oldTemplatePath = Path.Combine(templateDir, oldFileName);
        var newTemplatePath = Path.Combine(templateDir, newFileName);

        // 元のファイルが存在するかチェック
        if (!File.Exists(oldTemplatePath))
            throw new FileNotFoundException($"Template not found: {oldFileName}");

        // 新しいファイル名と同じファイルが既に存在するかチェック（大文字小文字を区別しない）
        if (!oldFileName.Equals(newFileName, StringComparison.OrdinalIgnoreCase) && File.Exists(newTemplatePath))
            throw new IOException($"A template with this name already exists: {newFileName}");

        // ファイル名変更を実行
        File.Move(oldTemplatePath, newTemplatePath);
    }

    /// <summary>
    /// 指定ディレクトリ内のテンプレートファイル一覧を取得
    /// </summary>
    /// <param name="directoryPath">ディレクトリのフルパス</param>
    /// <returns>テンプレート情報のリスト</returns>
    public List<TemplateInfo> GetTemplatesInDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return new List<TemplateInfo>();

        var tsvFiles = Directory.GetFiles(directoryPath, "*.tsv")
            .Concat(Directory.GetFiles(directoryPath, "*.csv"))
            .ToArray();

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
    /// 指定ディレクトリ内のサブディレクトリ一覧を取得
    /// </summary>
    /// <param name="directoryPath">ディレクトリのフルパス</param>
    /// <returns>サブディレクトリのフルパスリスト</returns>
    public List<string> GetSubdirectories(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return new List<string>();

        return Directory.GetDirectories(directoryPath)
            .OrderBy(d => Path.GetFileName(d))
            .ToList();
    }

    /// <summary>
    /// 指定ディレクトリ内に新しいテンプレートを作成
    /// </summary>
    /// <param name="directoryPath">作成先ディレクトリのフルパス</param>
    /// <param name="templateName">テンプレート名</param>
    /// <returns>作成されたテンプレートのフルパス</returns>
    public string CreateNewTemplateInDirectory(string directoryPath, string templateName)
    {
        // テンプレート名のバリデーション
        if (string.IsNullOrWhiteSpace(templateName))
            throw new ArgumentException("Template name cannot be empty.", nameof(templateName));

        // 無効な文字をチェック
        var invalidChars = Path.GetInvalidFileNameChars();
        if (templateName.IndexOfAny(invalidChars) >= 0)
            throw new ArgumentException("Template name contains invalid characters.", nameof(templateName));

        // ディレクトリがTemplateディレクトリ内かチェック
        if (!IsTemplateFolder(directoryPath) && directoryPath != GetTemplateDirectory())
            throw new ArgumentException("Directory is not within Template folder.", nameof(directoryPath));

        // 拡張子がない場合は.tsvを追加
        if (!templateName.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) &&
            !templateName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            templateName = templateName + ".tsv";
        }

        var templatePath = Path.Combine(directoryPath, templateName);

        // 既存ファイルと重複チェック
        if (File.Exists(templatePath))
            throw new IOException($"A template with this name already exists: {templateName}");

        // 空のファイルを作成（ヘッダー行付き）
        var format = DelimiterStrategyFactory.DetectFromExtension(templatePath);
        var strategy = DelimiterStrategyFactory.Create(format);
        var initialContent = strategy.FormatLine(new[] { "Column1", "Column2", "Column3" });
        File.WriteAllText(templatePath, initialContent);

        return templatePath;
    }

    /// <summary>
    /// 指定ディレクトリ内に新しいフォルダを作成
    /// </summary>
    /// <param name="parentDirectoryPath">親ディレクトリのフルパス</param>
    /// <param name="folderName">フォルダ名</param>
    /// <returns>作成されたフォルダのフルパス</returns>
    public string CreateTemplateFolder(string parentDirectoryPath, string folderName)
    {
        // フォルダ名のバリデーション
        if (string.IsNullOrWhiteSpace(folderName))
            throw new ArgumentException("Folder name cannot be empty.", nameof(folderName));

        // 無効な文字をチェック
        var invalidChars = Path.GetInvalidFileNameChars();
        if (folderName.IndexOfAny(invalidChars) >= 0)
            throw new ArgumentException("Folder name contains invalid characters.", nameof(folderName));

        // 親ディレクトリがTemplateディレクトリ内かチェック
        var templateDir = GetTemplateDirectory();
        if (!IsTemplateFolder(parentDirectoryPath) && parentDirectoryPath != templateDir)
            throw new ArgumentException("Parent directory is not within Template folder.", nameof(parentDirectoryPath));

        var folderPath = Path.Combine(parentDirectoryPath, folderName);

        // 既存フォルダと重複チェック
        if (Directory.Exists(folderPath))
            throw new IOException($"A folder with this name already exists: {folderName}");

        Directory.CreateDirectory(folderPath);

        return folderPath;
    }

    /// <summary>
    /// テンプレートフォルダを削除
    /// </summary>
    /// <param name="folderPath">削除するフォルダのフルパス</param>
    public void DeleteTemplateFolder(string folderPath)
    {
        // フォルダがTemplateディレクトリ内かチェック
        if (!IsTemplateFolder(folderPath))
            throw new ArgumentException("Folder is not within Template directory.", nameof(folderPath));

        // フォルダが存在する場合のみ削除
        if (Directory.Exists(folderPath))
        {
            Directory.Delete(folderPath, true);
        }
    }

    /// <summary>
    /// テンプレートフォルダの名前を変更
    /// </summary>
    /// <param name="oldFolderPath">変更前のフォルダパス</param>
    /// <param name="newFolderName">新しいフォルダ名</param>
    /// <returns>変更後のフォルダパス</returns>
    public string RenameTemplateFolder(string oldFolderPath, string newFolderName)
    {
        // 新しいフォルダ名のバリデーション
        if (string.IsNullOrWhiteSpace(newFolderName))
            throw new ArgumentException("Folder name cannot be empty.", nameof(newFolderName));

        // 無効な文字をチェック
        var invalidChars = Path.GetInvalidFileNameChars();
        if (newFolderName.IndexOfAny(invalidChars) >= 0)
            throw new ArgumentException("Folder name contains invalid characters.", nameof(newFolderName));

        // フォルダがTemplateディレクトリ内かチェック
        if (!IsTemplateFolder(oldFolderPath))
            throw new ArgumentException("Folder is not within Template directory.", nameof(oldFolderPath));

        // 元のフォルダが存在するかチェック
        if (!Directory.Exists(oldFolderPath))
            throw new DirectoryNotFoundException($"Folder not found: {oldFolderPath}");

        var parentDir = Path.GetDirectoryName(oldFolderPath);
        if (string.IsNullOrEmpty(parentDir))
            throw new ArgumentException("Invalid folder path.", nameof(oldFolderPath));

        var newFolderPath = Path.Combine(parentDir, newFolderName);

        // 新しいフォルダ名と同じフォルダが既に存在するかチェック
        if (!oldFolderPath.Equals(newFolderPath, StringComparison.OrdinalIgnoreCase) && Directory.Exists(newFolderPath))
            throw new IOException($"A folder with this name already exists: {newFolderName}");

        // フォルダ名変更を実行
        Directory.Move(oldFolderPath, newFolderPath);

        return newFolderPath;
    }

    /// <summary>
    /// 指定パスがTemplateディレクトリ内のフォルダかチェック
    /// </summary>
    /// <param name="folderPath">チェックするフォルダパス</param>
    /// <returns>Templateディレクトリ内のフォルダの場合true</returns>
    public bool IsTemplateFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath))
            return false;

        try
        {
            // 両方のパスを正規化
            var normalizedFolderPath = Path.GetFullPath(folderPath);
            var templateDir = Path.GetFullPath(GetTemplateDirectory());

            // フォルダパスがTemplateディレクトリで始まり、かつTemplateディレクトリ自体ではないか
            return normalizedFolderPath.StartsWith(templateDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                   && Directory.Exists(normalizedFolderPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// テンプレートセット（JSONファイル）一覧を取得
    /// </summary>
    public IEnumerable<TemplateSet> GetTemplateSets()
    {
        var templateDir = GetTemplateDirectory();
        if (!Directory.Exists(templateDir))
            return Enumerable.Empty<TemplateSet>();

        var result = new List<TemplateSet>();
        GetTemplateSetsRecursively(templateDir, result);
        return result;
    }

    /// <summary>
    /// テンプレートセットを再帰的に取得
    /// </summary>
    private void GetTemplateSetsRecursively(string currentDir, List<TemplateSet> result)
    {
        // Get JSON files in current directory
        var jsonFiles = Directory.GetFiles(currentDir, "*.json");
        foreach (var path in jsonFiles)
        {
            var set = LoadTemplateSet(path);
            if (set != null)
            {
                result.Add(set);
            }
        }

        // Recursively process subdirectories
        var subdirectories = Directory.GetDirectories(currentDir);
        foreach (var subdir in subdirectories)
        {
            GetTemplateSetsRecursively(subdir, result);
        }
    }

    /// <summary>
    /// 指定ディレクトリ内のテンプレートセット一覧を取得
    /// </summary>
    public List<TemplateSet> GetTemplateSetsInDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            return new List<TemplateSet>();

        var jsonFiles = Directory.GetFiles(directoryPath, "*.json");
        var result = new List<TemplateSet>();

        foreach (var path in jsonFiles)
        {
            var set = LoadTemplateSet(path);
            if (set != null)
            {
                result.Add(set);
            }
        }

        return result.OrderBy(s => s.Name).ToList();
    }

    /// <summary>
    /// テンプレートセットを読み込む
    /// </summary>
    public TemplateSet? LoadTemplateSet(string jsonPath)
    {
        if (!File.Exists(jsonPath))
            return null;

        try
        {
            var json = File.ReadAllText(jsonPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var set = JsonSerializer.Deserialize<TemplateSet>(json, options);
            if (set != null)
            {
                set.FilePath = jsonPath;
                // 名前が空の場合はファイル名を使用
                if (string.IsNullOrEmpty(set.Name))
                {
                    set.Name = Path.GetFileNameWithoutExtension(jsonPath);
                }
            }
            return set;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// テンプレートセットから複数ファイルを一括作成
    /// </summary>
    public List<string> CreateFilesFromSet(TemplateSet set, string targetDirectory, params string[] placeholders)
    {
        if (set == null || set.Templates == null || set.Templates.Count == 0)
            throw new ArgumentException("Template set is empty or invalid.");

        if (!Directory.Exists(targetDirectory))
            throw new DirectoryNotFoundException($"Target directory not found: {targetDirectory}");

        var createdFiles = new List<string>();
        var templateDir = GetTemplateDirectory();

        foreach (var item in set.Templates)
        {
            // テンプレートファイルのパスを解決（Templateフォルダからの相対パス）
            var templatePath = Path.GetFullPath(Path.Combine(templateDir, item.File));
            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException($"Template file not found: {item.File}");
            }

            // 出力ファイル名を生成（OutputNameが未設定の場合はテンプレートの相対パスをそのまま使用）
            var outputName = string.IsNullOrEmpty(item.OutputName)
                ? item.File
                : item.OutputName;
            var outputFileName = ApplyPlaceholders(outputName, placeholders);

            // パスに無効な文字が含まれていないかチェック（フォルダパスも許可）
            var invalidChars = Path.GetInvalidPathChars();
            if (outputFileName.IndexOfAny(invalidChars) >= 0)
            {
                throw new ArgumentException($"Output file name contains invalid characters: {outputFileName}");
            }

            var targetPath = Path.Combine(targetDirectory, outputFileName);

            // 出力先にフォルダパスが含まれている場合は、フォルダを作成
            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // ファイルをコピー
            File.Copy(templatePath, targetPath, overwrite: false);
            createdFiles.Add(targetPath);
        }

        return createdFiles;
    }

    /// <summary>
    /// テンプレートセットから作成されるファイル名をプレビュー
    /// </summary>
    public List<string> PreviewFileNames(TemplateSet set, params string[] placeholders)
    {
        if (set == null || set.Templates == null)
            return new List<string>();

        return set.Templates
            .Select(item =>
            {
                // OutputNameが未設定の場合はテンプレートの相対パスをそのまま使用
                var outputName = string.IsNullOrEmpty(item.OutputName)
                    ? item.File
                    : item.OutputName;
                return ApplyPlaceholders(outputName, placeholders);
            })
            .ToList();
    }

    /// <summary>
    /// プレースホルダー（{0}, {1}, ...）を値で置換
    /// </summary>
    private string ApplyPlaceholders(string template, string[] placeholders)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        try
        {
            // string.Format を使用してプレースホルダーを置換
            // placeholders が足りない場合は空文字列を補填
            var paddedPlaceholders = new string[10]; // 最大10個のプレースホルダーをサポート
            for (int i = 0; i < paddedPlaceholders.Length; i++)
            {
                paddedPlaceholders[i] = i < placeholders.Length ? placeholders[i] : string.Empty;
            }
            return string.Format(template, paddedPlaceholders);
        }
        catch
        {
            // フォーマットに失敗した場合は元のテンプレートを返す
            return template;
        }
    }

    /// <summary>
    /// テンプレートファイル名からプレースホルダー数を検出
    /// {{0}} のようなエスケープされたブレースは無視する
    /// </summary>
    /// <param name="templateFileName">テンプレートファイル名</param>
    /// <returns>プレースホルダーの数（0-indexed の最大値 + 1）</returns>
    public int DetectPlaceholderCount(string templateFileName)
    {
        if (string.IsNullOrEmpty(templateFileName))
            return 0;

        var maxPlaceholder = -1;
        // エスケープされていないプレースホルダーのみマッチ
        // {{0}} は {0} にエスケープされるため、{{ や }} の前後をチェック
        var regex = new Regex(@"(?<!\{)\{(\d+)\}(?!\})");

        var matches = regex.Matches(templateFileName);
        foreach (Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out int index))
            {
                maxPlaceholder = Math.Max(maxPlaceholder, index);
            }
        }

        return maxPlaceholder + 1; // 0-indexed なので +1
    }

    /// <summary>
    /// テンプレートファイル名にプレースホルダーを適用
    /// </summary>
    /// <param name="templateFileName">テンプレートファイル名</param>
    /// <param name="placeholders">プレースホルダー値の配列</param>
    /// <returns>プレースホルダーが適用されたファイル名</returns>
    public string ApplyPlaceholdersToFileName(string templateFileName, params string[] placeholders)
    {
        return ApplyPlaceholders(templateFileName, placeholders);
    }

    /// <summary>
    /// テンプレートセット内のプレースホルダー数を検出
    /// </summary>
    /// <param name="set">テンプレートセット</param>
    /// <returns>プレースホルダーの数（0-indexed の最大値 + 1）</returns>
    public int DetectPlaceholderCountInSet(TemplateSet set)
    {
        if (set == null || set.Templates == null || set.Templates.Count == 0)
            return 0;

        var maxPlaceholder = -1;
        var regex = new Regex(@"(?<!\{)\{(\d+)\}(?!\})");

        foreach (var template in set.Templates)
        {
            if (string.IsNullOrEmpty(template.OutputName))
            {
                var matches = regex.Matches(template.File);
                foreach (Match match in matches)
                {
                    if (int.TryParse(match.Groups[1].Value, out int index))
                    {
                        maxPlaceholder = Math.Max(maxPlaceholder, index);
                    }
                }
            }
            else
            {
                var matches = regex.Matches(template.OutputName);
                foreach (Match match in matches)
                {
                    if (int.TryParse(match.Groups[1].Value, out int index))
                    {
                        maxPlaceholder = Math.Max(maxPlaceholder, index);
                    }
                }
            }

        }

        return maxPlaceholder + 1; // 0-indexed なので +1
    }

    /// <summary>
    /// テンプレートから新規ファイルを作成（プレースホルダー対応版）
    /// </summary>
    /// <param name="templateFileName">テンプレートファイル名（サブフォルダからの相対パス可）</param>
    /// <param name="targetDirectory">作成先ディレクトリ</param>
    /// <param name="placeholders">プレースホルダー値の配列</param>
    /// <returns>作成されたファイルのフルパス</returns>
    public string CreateFileFromTemplateWithPlaceholders(string templateFileName, string targetDirectory, params string[] placeholders)
    {
        var templateDir = GetTemplateDirectory();
        // パスを正規化して結合
        var templatePath = Path.GetFullPath(Path.Combine(templateDir, templateFileName));

        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"Template not found: {templateFileName}");

        // ターゲットディレクトリも正規化
        targetDirectory = Path.GetFullPath(targetDirectory);

        if (!Directory.Exists(targetDirectory))
            throw new DirectoryNotFoundException($"Target directory not found: {targetDirectory}");

        // ベースファイル名を生成（プレースホルダー適用）
        var templateFileNameOnly = Path.GetFileName(templateFileName);
        var baseFileName = Path.GetFileNameWithoutExtension(templateFileNameOnly);
        var extension = Path.GetExtension(templateFileNameOnly);

        // プレースホルダーを適用
        var appliedBaseFileName = ApplyPlaceholders(baseFileName, placeholders);

        // ファイル名に使用できない文字をチェック
        var invalidChars = Path.GetInvalidFileNameChars();
        if (appliedBaseFileName.IndexOfAny(invalidChars) >= 0)
        {
            throw new ArgumentException($"Output file name contains invalid characters: {appliedBaseFileName}");
        }

        string newFileName = $"{appliedBaseFileName}{extension}";
        string newFilePath = Path.Combine(targetDirectory, newFileName);

        // ファイルが既に存在する場合はエラー
        if (File.Exists(newFilePath))
        {
            throw new IOException($"File already exists: {newFileName}");
        }

        // テンプレートファイルの内容をコピー
        File.Copy(templatePath, newFilePath);

        return newFilePath;
    }
}
