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
    /// Gets commit history for a folder or repository
    /// </summary>
    Task<List<GitCommit>> GetFolderHistoryAsync(string folderPath);

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

    /// <summary>
    /// Gets the current branch name
    /// </summary>
    Task<string?> GetCurrentBranchAsync(string repoRoot);

    /// <summary>
    /// Gets list of local branches
    /// </summary>
    Task<List<string>> GetLocalBranchesAsync(string repoRoot);

    /// <summary>
    /// Gets list of remote branches
    /// </summary>
    Task<List<string>> GetRemoteBranchesAsync(string repoRoot);

    /// <summary>
    /// Checks out a branch
    /// </summary>
    Task<(bool success, string message)> CheckoutBranchAsync(string repoRoot, string branchName);

    /// <summary>
    /// Creates a new branch
    /// </summary>
    Task<(bool success, string message)> CreateBranchAsync(string repoRoot, string branchName, bool checkout = false);

    /// <summary>
    /// Deletes a branch
    /// </summary>
    Task<(bool success, string message)> DeleteBranchAsync(string repoRoot, string branchName, bool force = false);

    /// <summary>
    /// Fetches from remote
    /// </summary>
    Task<(bool success, string message)> FetchAsync(string repoRoot, string? remoteName = null);

    /// <summary>
    /// Pulls from remote
    /// </summary>
    Task<(bool success, string message)> PullAsync(string repoRoot, string? remoteName = null, string? branchName = null);

    /// <summary>
    /// Pushes to remote
    /// </summary>
    Task<(bool success, string message)> PushAsync(string repoRoot, string? remoteName = null, string? branchName = null);

    /// <summary>
    /// Gets list of remotes
    /// </summary>
    Task<List<string>> GetRemotesAsync(string repoRoot);

    /// <summary>
    /// Gets tracking status (ahead/behind) for current branch
    /// </summary>
    Task<(int ahead, int behind)> GetTrackingStatusAsync(string repoRoot);
}
