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
            var directory = Path.GetDirectoryName(filePath);
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
            var directory = Path.GetDirectoryName(filePath);
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
                return commits;

            // Get relative path from repo root
            var relativePath = Path.GetRelativePath(repoRoot, filePath).Replace('\\', '/');

            // Format: Hash|AuthorName|AuthorEmail|Date|Subject
            var format = "%H|%an|%ae|%ad|%s";
            var arguments = $"log --follow --format=\"{format}\" --date=iso -- \"{relativePath}\"";

            var (exitCode, output, _) = await RunGitCommandAsync(arguments, repoRoot);
            if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
                return commits;

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
        }
        catch
        {
            // Return empty list on error
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
        var files = new List<string>();

        try
        {
            string arguments;
            if (commit1Hash == null && commit2Hash == null)
            {
                // No commits specified - return empty list
                return files;
            }
            else if (commit1Hash == null)
            {
                // Compare working directory with commit2Hash
                arguments = $"diff --name-only {commit2Hash}";
            }
            else if (commit2Hash == null)
            {
                // Compare commit1Hash with working directory
                arguments = $"diff --name-only {commit1Hash}";
            }
            else
            {
                // Compare two commits
                arguments = $"diff --name-only {commit1Hash} {commit2Hash}";
            }

            var (exitCode, output, _) = await RunGitCommandAsync(arguments, repoRoot);
            if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
            {
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                files.AddRange(lines.Select(l => l.Trim()));
            }
        }
        catch
        {
            // Return empty list on error
        }

        return files;
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
