using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using VGrid.Models;

namespace VGrid.Services;

/// <summary>
/// Service for Git operations using git CLI
/// </summary>
public class GitService : IGitService
{
    /// <summary>
    /// Checks if git is available in PATH
    /// </summary>
    public async Task<bool> IsGitAvailableAsync()
    {
        try
        {
            var (exitCode, _, _) = await RunGitCommandAsync("--version", Directory.GetCurrentDirectory());
            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if the file is in a git repository
    /// </summary>
    public async Task<bool> IsInGitRepositoryAsync(string filePath)
    {
        try
        {
            // Handle both file paths and directory paths
            var directory = Directory.Exists(filePath) ? filePath : Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(directory))
                return false;

            var (exitCode, output, _) = await RunGitCommandAsync("rev-parse --is-inside-work-tree", directory);
            return exitCode == 0 && output.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the git repository root for a given file
    /// </summary>
    public async Task<string?> GetRepositoryRootAsync(string filePath)
    {
        try
        {
            // Handle both file paths and directory paths
            var directory = Directory.Exists(filePath) ? filePath : Path.GetDirectoryName(filePath);
            if (string.IsNullOrEmpty(directory))
                return null;

            var (exitCode, output, _) = await RunGitCommandAsync("rev-parse --show-toplevel", directory);
            if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                // Convert Unix path to Windows path if needed
                var root = output.Trim().Replace('/', Path.DirectorySeparatorChar);
                return root;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets commit history for a specific file
    /// </summary>
    public async Task<List<GitCommit>> GetFileHistoryAsync(string filePath)
    {
        var commits = new List<GitCommit>();

        try
        {
            var repoRoot = await GetRepositoryRootAsync(filePath);
            if (string.IsNullOrEmpty(repoRoot))
            {
                System.Diagnostics.Debug.WriteLine($"GitService: Could not get repository root for {filePath}");
                return commits;
            }

            // Get relative path from repo root
            var relativePath = Path.GetRelativePath(repoRoot, filePath).Replace('\\', '/');

            // Format: Hash|AuthorName|AuthorEmail|Date|Subject
            var format = "%H|%an|%ae|%ad|%s";
            var arguments = $"log --follow --format=\"{format}\" --date=iso -- \"{relativePath}\"";

            var (exitCode, output, error) = await RunGitCommandAsync(arguments, repoRoot);
            if (exitCode != 0)
            {
                System.Diagnostics.Debug.WriteLine($"GitService: git log command failed with exit code {exitCode}");
                System.Diagnostics.Debug.WriteLine($"GitService: Error: {error}");
                return commits;
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                System.Diagnostics.Debug.WriteLine($"GitService: No git history found for {relativePath}");
                return commits;
            }

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length >= 5)
                {
                    var commit = new GitCommit
                    {
                        Hash = parts[0],
                        AuthorName = parts[1],
                        AuthorEmail = parts[2],
                        CommitDate = TryParseGitDate(parts[3]),
                        Message = string.Join("|", parts.Skip(4)) // Message might contain '|'
                    };
                    commits.Add(commit);
                }
            }

            System.Diagnostics.Debug.WriteLine($"GitService: Found {commits.Count} commits for {relativePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GitService: Exception in GetFileHistoryAsync: {ex.Message}");
        }

        return commits;
    }

    /// <summary>
    /// Gets commit history for a folder or repository
    /// </summary>
    public async Task<List<GitCommit>> GetFolderHistoryAsync(string folderPath)
    {
        var commits = new List<GitCommit>();

        try
        {
            // Handle both file paths and directory paths
            var directory = Directory.Exists(folderPath) ? folderPath : Path.GetDirectoryName(folderPath);
            if (string.IsNullOrEmpty(directory))
            {
                System.Diagnostics.Debug.WriteLine($"GitService: Invalid folder path: {folderPath}");
                return commits;
            }

            var repoRoot = await GetRepositoryRootAsync(directory);
            if (string.IsNullOrEmpty(repoRoot))
            {
                System.Diagnostics.Debug.WriteLine($"GitService: Could not get repository root for {folderPath}");
                return commits;
            }

            // Get relative path from repo root
            var relativePath = Path.GetRelativePath(repoRoot, directory).Replace('\\', '/');

            // Format: Hash|AuthorName|AuthorEmail|Date|Subject
            var format = "%H|%an|%ae|%ad|%s";

            // If relative path is ".", get all commits in repository
            // Otherwise, get commits that affected the specific folder
            string arguments;
            if (relativePath == ".")
            {
                arguments = $"log --format=\"{format}\" --date=iso";
            }
            else
            {
                arguments = $"log --format=\"{format}\" --date=iso -- \"{relativePath}\"";
            }

            var (exitCode, output, error) = await RunGitCommandAsync(arguments, repoRoot);
            if (exitCode != 0)
            {
                System.Diagnostics.Debug.WriteLine($"GitService: git log command failed with exit code {exitCode}");
                System.Diagnostics.Debug.WriteLine($"GitService: Error: {error}");
                return commits;
            }

            if (string.IsNullOrWhiteSpace(output))
            {
                System.Diagnostics.Debug.WriteLine($"GitService: No git history found for {relativePath}");
                return commits;
            }

            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('|');
                if (parts.Length >= 5)
                {
                    var commit = new GitCommit
                    {
                        Hash = parts[0],
                        AuthorName = parts[1],
                        AuthorEmail = parts[2],
                        CommitDate = TryParseGitDate(parts[3]),
                        Message = string.Join("|", parts.Skip(4)) // Message might contain '|'
                    };
                    commits.Add(commit);
                }
            }

            System.Diagnostics.Debug.WriteLine($"GitService: Found {commits.Count} commits for folder {relativePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"GitService: Exception in GetFolderHistoryAsync: {ex.Message}");
        }

        return commits;
    }

    /// <summary>
    /// Gets file content at a specific commit
    /// </summary>
    public async Task<string> GetFileAtCommitAsync(string filePath, string commitHash)
    {
        try
        {
            var repoRoot = await GetRepositoryRootAsync(filePath);
            if (string.IsNullOrEmpty(repoRoot))
                return string.Empty;

            // Get relative path from repo root
            var relativePath = Path.GetRelativePath(repoRoot, filePath).Replace('\\', '/');

            var arguments = $"show {commitHash}:\"{relativePath}\"";
            var (exitCode, output, _) = await RunGitCommandAsync(arguments, repoRoot);

            if (exitCode == 0)
                return output;

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets file content at a specific commit using relative path
    /// </summary>
    public async Task<string> GetFileAtCommitAsync(string repoRoot, string relativeFilePath, string commitHash)
    {
        try
        {
            // Use relative path directly (already in Unix format from git diff)
            var relativePath = relativeFilePath.Replace('\\', '/');

            var arguments = $"show {commitHash}:\"{relativePath}\"";
            var (exitCode, output, _) = await RunGitCommandAsync(arguments, repoRoot);

            if (exitCode == 0)
                return output;

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Gets the parent commit hash for a given commit
    /// </summary>
    public async Task<string?> GetParentCommitAsync(string commitHash, string repoRoot)
    {
        try
        {
            var arguments = $"rev-parse {commitHash}^";
            var (exitCode, output, _) = await RunGitCommandAsync(arguments, repoRoot);

            if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
                return output.Trim();

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets list of changed files between two commits or between commit and working directory
    /// </summary>
    public async Task<List<string>> GetChangedFilesAsync(string repoRoot, string? commit1Hash, string? commit2Hash)
    {
        var files = new HashSet<string>();

        try
        {
            string arguments;
            if (commit1Hash == null && commit2Hash == null)
            {
                // No commits specified - return empty list
                return files.ToList();
            }
            else if (commit1Hash == null)
            {
                // Compare working directory with commit2Hash
                arguments = $"diff --name-only -z {commit2Hash}";
            }
            else if (commit2Hash == null)
            {
                // Compare commit1Hash with working directory
                arguments = $"diff --name-only -z {commit1Hash}";
            }
            else
            {
                // Compare two commits
                arguments = $"diff --name-only -z {commit1Hash} {commit2Hash}";
            }

            var (exitCode, output, _) = await RunGitCommandAsync(arguments, repoRoot);
            if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                // With -z option, files are separated by NUL character
                var lines = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    files.Add(line);
                }
            }

            // If comparing with working directory, also include added/untracked files from git status
            if (commit2Hash == null)
            {
                var statusArgs = "status --porcelain -z --untracked-files=all";
                var (statusExitCode, statusOutput, _) = await RunGitCommandAsync(statusArgs, repoRoot);

                if (statusExitCode == 0 && !string.IsNullOrWhiteSpace(statusOutput))
                {
                    // With -z option, entries are separated by NUL character
                    // Format: "XY filename\0" where X is staged status, Y is unstaged status
                    var entries = statusOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var entry in entries)
                    {
                        if (entry.Length < 3)
                            continue;

                        var statusCode = entry.Substring(0, 2);
                        // Include untracked (??) and added (A ) files
                        if (statusCode == "??" || statusCode.Contains('A'))
                        {
                            var filePath = entry.Substring(3);
                            files.Add(filePath);
                        }
                    }
                }
            }
        }
        catch
        {
            // Return empty list on error
        }

        return files.ToList();
    }

    /// <summary>
    /// Runs a git command and returns the exit code, output, and error
    /// </summary>
    private async Task<(int exitCode, string output, string error)> RunGitCommandAsync(
        string arguments,
        string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (process.ExitCode, output, error);
    }

    /// <summary>
    /// Gets uncommitted files in a repository (modified, added, untracked)
    /// </summary>
    public async Task<List<(string filePath, GitFileStatus status)>> GetUncommittedFilesAsync(string repoPath)
    {
        var files = new List<(string filePath, GitFileStatus status)>();

        try
        {
            // Ensure repoPath is a directory
            var directory = Directory.Exists(repoPath) ? repoPath : Path.GetDirectoryName(repoPath);
            if (string.IsNullOrEmpty(directory))
                return files;

            // Check if in git repository
            var isInRepo = await IsInGitRepositoryAsync(directory);
            if (!isInRepo)
                return files;

            // Get repository root
            var repoRoot = await GetRepositoryRootAsync(directory);
            if (string.IsNullOrEmpty(repoRoot))
                return files;

            // Run git status --porcelain to get uncommitted files
            var arguments = "status --porcelain -z --untracked-files=all";
            var (exitCode, output, _) = await RunGitCommandAsync(arguments, repoRoot);

            if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
                return files;

            // Parse output
            // With -z option, entries are separated by NUL character
            // Format: "XY filename\0" where X is staged status, Y is unstaged status
            // M = modified, A = added, D = deleted, ?? = untracked, etc.
            var entries = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                if (entry.Length < 3)
                    continue;

                var statusCode = entry.Substring(0, 2);
                var filePath = entry.Substring(3);

                // Convert to absolute path
                var absolutePath = Path.Combine(repoRoot, filePath.Replace('/', Path.DirectorySeparatorChar));

                // Determine status
                GitFileStatus status;
                if (statusCode == "??")
                {
                    status = GitFileStatus.Untracked;
                }
                else if (statusCode.Contains('M'))
                {
                    status = GitFileStatus.Modified;
                }
                else if (statusCode.Contains('A'))
                {
                    status = GitFileStatus.Added;
                }
                else if (statusCode.Contains('D'))
                {
                    status = GitFileStatus.Deleted;
                }
                else
                {
                    status = GitFileStatus.Modified; // Default
                }

                files.Add((absolutePath, status));
            }
        }
        catch
        {
            // Return empty list on error
        }

        return files;
    }

    /// <summary>
    /// Stages specific files for commit
    /// </summary>
    public async Task<bool> StageFilesAsync(string repoPath, IEnumerable<string> filePaths)
    {
        try
        {
            // Ensure repoPath is a directory
            var directory = Directory.Exists(repoPath) ? repoPath : Path.GetDirectoryName(repoPath);
            if (string.IsNullOrEmpty(directory))
                return false;

            // Get repository root
            var repoRoot = await GetRepositoryRootAsync(directory);
            if (string.IsNullOrEmpty(repoRoot))
                return false;

            // Convert absolute paths to relative paths from repo root
            var relativePaths = filePaths
                .Select(f => Path.GetRelativePath(repoRoot, f).Replace('\\', '/'))
                .ToList();

            if (!relativePaths.Any())
                return false;

            // Run git add for each file
            foreach (var relativePath in relativePaths)
            {
                var arguments = $"add \"{relativePath}\"";
                var (exitCode, _, _) = await RunGitCommandAsync(arguments, repoRoot);
                if (exitCode != 0)
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a commit with staged files
    /// </summary>
    public async Task<bool> CommitAsync(string repoPath, string message)
    {
        try
        {
            // Ensure repoPath is a directory
            var directory = Directory.Exists(repoPath) ? repoPath : Path.GetDirectoryName(repoPath);
            if (string.IsNullOrEmpty(directory))
                return false;

            // Get repository root
            var repoRoot = await GetRepositoryRootAsync(directory);
            if (string.IsNullOrEmpty(repoRoot))
                return false;

            // Escape quotes in commit message
            var escapedMessage = message.Replace("\"", "\\\"");

            // Run git commit
            var arguments = $"commit -m \"{escapedMessage}\"";
            var (exitCode, _, _) = await RunGitCommandAsync(arguments, repoRoot);

            return exitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tries to parse a git date string in ISO format
    /// </summary>
    private DateTime TryParseGitDate(string dateString)
    {
        // Git ISO format: 2024-01-15 10:30:45 +0900
        // Try multiple formats
        var formats = new[]
        {
            "yyyy-MM-dd HH:mm:ss zzz",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd"
        };

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(dateString.Trim(), format, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var result))
            {
                return result;
            }
        }

        // Fallback: try general parsing
        if (DateTime.TryParse(dateString, out var fallbackResult))
        {
            return fallbackResult;
        }

        return DateTime.MinValue;
    }
}
