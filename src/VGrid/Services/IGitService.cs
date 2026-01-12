using VGrid.Models;

namespace VGrid.Services;

/// <summary>
/// Service for Git operations using git CLI
/// </summary>
public interface IGitService
{
    /// <summary>
    /// Checks if git is available in PATH
    /// </summary>
    Task<bool> IsGitAvailableAsync();

    /// <summary>
    /// Checks if the file is in a git repository
    /// </summary>
    Task<bool> IsInGitRepositoryAsync(string filePath);

    /// <summary>
    /// Gets the git repository root for a given file
    /// </summary>
    Task<string?> GetRepositoryRootAsync(string filePath);

    /// <summary>
    /// Gets commit history for a specific file
    /// </summary>
    Task<List<GitCommit>> GetFileHistoryAsync(string filePath);

    /// <summary>
    /// Gets file content at a specific commit
    /// </summary>
    Task<string> GetFileAtCommitAsync(string filePath, string commitHash);

    /// <summary>
    /// Gets file content at a specific commit using relative path
    /// </summary>
    Task<string> GetFileAtCommitAsync(string repoRoot, string relativeFilePath, string commitHash);

    /// <summary>
    /// Gets the parent commit hash for a given commit
    /// </summary>
    Task<string?> GetParentCommitAsync(string commitHash, string repoRoot);

    /// <summary>
    /// Gets list of changed files between two commits
    /// </summary>
    Task<List<string>> GetChangedFilesAsync(string repoRoot, string? commit1Hash, string? commit2Hash);

    /// <summary>
    /// Gets uncommitted files in a repository (modified, added, untracked)
    /// </summary>
    Task<List<(string filePath, GitFileStatus status)>> GetUncommittedFilesAsync(string repoPath);

    /// <summary>
    /// Stages specific files for commit
    /// </summary>
    Task<bool> StageFilesAsync(string repoPath, IEnumerable<string> filePaths);

    /// <summary>
    /// Creates a commit with staged files
    /// </summary>
    Task<bool> CommitAsync(string repoPath, string message);
}
