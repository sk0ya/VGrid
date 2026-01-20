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
    /// Gets the current branch name
    /// </summary>
    public async Task<string?> GetCurrentBranchAsync(string repoRoot)
    {
        try
        {
            var (exitCode, output, _) = await RunGitCommandAsync("rev-parse --abbrev-ref HEAD", repoRoot);
            if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                return output.Trim();
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets list of local branches
    /// </summary>
    public async Task<List<string>> GetLocalBranchesAsync(string repoRoot)
    {
        var branches = new List<string>();
        try
        {
            var (exitCode, output, _) = await RunGitCommandAsync("branch --format=%(refname:short)", repoRoot);
            if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                branches.AddRange(lines.Select(l => l.Trim()));
            }
        }
        catch
        {
            // Return empty list on error
        }
        return branches;
    }

    /// <summary>
    /// Gets list of remote branches
    /// </summary>
    public async Task<List<string>> GetRemoteBranchesAsync(string repoRoot)
    {
        var branches = new List<string>();
        try
        {
            var (exitCode, output, _) = await RunGitCommandAsync("branch -r --format=%(refname:short)", repoRoot);
            if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var branch = line.Trim();
                    // Skip HEAD pointer
                    if (!branch.Contains("HEAD"))
                    {
                        branches.Add(branch);
                    }
                }
            }
        }
        catch
        {
            // Return empty list on error
        }
        return branches;
    }

    /// <summary>
    /// Checks out a branch
    /// </summary>
    public async Task<(bool success, string message)> CheckoutBranchAsync(string repoRoot, string branchName)
    {
        try
        {
            var (exitCode, output, error) = await RunGitCommandAsync($"checkout \"{branchName}\"", repoRoot);
            if (exitCode == 0)
            {
                return (true, $"Switched to branch '{branchName}'");
            }
            return (false, error.Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Creates a new branch
    /// </summary>
    public async Task<(bool success, string message)> CreateBranchAsync(string repoRoot, string branchName, bool checkout = false)
    {
        try
        {
            var command = checkout ? $"checkout -b \"{branchName}\"" : $"branch \"{branchName}\"";
            var (exitCode, output, error) = await RunGitCommandAsync(command, repoRoot);
            if (exitCode == 0)
            {
                var msg = checkout
                    ? $"Switched to a new branch '{branchName}'"
                    : $"Created branch '{branchName}'";
                return (true, msg);
            }
            return (false, error.Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Deletes a branch
    /// </summary>
    public async Task<(bool success, string message)> DeleteBranchAsync(string repoRoot, string branchName, bool force = false)
    {
        try
        {
            var flag = force ? "-D" : "-d";
            var (exitCode, output, error) = await RunGitCommandAsync($"branch {flag} \"{branchName}\"", repoRoot);
            if (exitCode == 0)
            {
                return (true, $"Deleted branch '{branchName}'");
            }
            return (false, error.Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Fetches from remote
    /// </summary>
    public async Task<(bool success, string message)> FetchAsync(string repoRoot, string? remoteName = null)
    {
        try
        {
            var command = string.IsNullOrEmpty(remoteName) ? "fetch --all --prune" : $"fetch \"{remoteName}\" --prune";
            var (exitCode, output, error) = await RunGitCommandAsync(command, repoRoot);
            if (exitCode == 0)
            {
                return (true, "Fetch completed successfully");
            }
            return (false, error.Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Pulls from remote
    /// </summary>
    public async Task<(bool success, string message)> PullAsync(string repoRoot, string? remoteName = null, string? branchName = null)
    {
        try
        {
            string command;
            if (!string.IsNullOrEmpty(remoteName) && !string.IsNullOrEmpty(branchName))
            {
                command = $"pull \"{remoteName}\" \"{branchName}\"";
            }
            else if (!string.IsNullOrEmpty(remoteName))
            {
                command = $"pull \"{remoteName}\"";
            }
            else
            {
                command = "pull";
            }

            var (exitCode, output, error) = await RunGitCommandAsync(command, repoRoot);
            if (exitCode == 0)
            {
                if (output.Contains("Already up to date"))
                {
                    return (true, "Already up to date");
                }
                return (true, "Pull completed successfully");
            }
            return (false, error.Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Pushes to remote
    /// </summary>
    public async Task<(bool success, string message)> PushAsync(string repoRoot, string? remoteName = null, string? branchName = null)
    {
        try
        {
            string command;
            if (!string.IsNullOrEmpty(remoteName) && !string.IsNullOrEmpty(branchName))
            {
                command = $"push \"{remoteName}\" \"{branchName}\"";
            }
            else if (!string.IsNullOrEmpty(remoteName))
            {
                command = $"push \"{remoteName}\"";
            }
            else
            {
                command = "push";
            }

            var (exitCode, output, error) = await RunGitCommandAsync(command, repoRoot);
            if (exitCode == 0)
            {
                if (output.Contains("Everything up-to-date") || error.Contains("Everything up-to-date"))
                {
                    return (true, "Everything up-to-date");
                }
                return (true, "Push completed successfully");
            }
            return (false, error.Trim());
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    /// <summary>
    /// Gets list of remotes
    /// </summary>
    public async Task<List<string>> GetRemotesAsync(string repoRoot)
    {
        var remotes = new List<string>();
        try
        {
            var (exitCode, output, _) = await RunGitCommandAsync("remote", repoRoot);
            if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                remotes.AddRange(lines.Select(l => l.Trim()));
            }
        }
        catch
        {
            // Return empty list on error
        }
        return remotes;
    }

    /// <summary>
    /// Gets tracking status (ahead/behind) for current branch
    /// </summary>
    public async Task<(int ahead, int behind)> GetTrackingStatusAsync(string repoRoot)
    {
        try
        {
            var (exitCode, output, _) = await RunGitCommandAsync("rev-list --left-right --count HEAD...@{upstream}", repoRoot);
            if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var parts = output.Trim().Split('\t');
                if (parts.Length >= 2)
                {
                    int.TryParse(parts[0], out int ahead);
                    int.TryParse(parts[1], out int behind);
                    return (ahead, behind);
                }
            }
        }
        catch
        {
            // Return 0,0 if no upstream or error
        }
        return (0, 0);
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
