using System.IO;
using VGrid.Helpers;

namespace VGrid.Models;

/// <summary>
/// Represents a file with uncommitted changes in a Git repository
/// </summary>
public class UncommittedFile : ViewModelBase
{
    private bool _isSelected = true; // Default to selected

    /// <summary>
    /// Absolute path to the file
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// File name only (without directory path)
    /// </summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>
    /// Relative path from repository root
    /// </summary>
    public string RelativePath { get; init; } = string.Empty;

    /// <summary>
    /// Git status of the file
    /// </summary>
    public GitFileStatus Status { get; init; }

    /// <summary>
    /// Whether this file is selected for commit
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>
    /// Short status text for display (A, M, D, ?)
    /// </summary>
    public string StatusText => Status switch
    {
        GitFileStatus.Added => "A",
        GitFileStatus.Modified => "M",
        GitFileStatus.Deleted => "D",
        GitFileStatus.Untracked => "?",
        _ => "?"
    };
}

/// <summary>
/// Git file status types
/// </summary>
public enum GitFileStatus
{
    /// <summary>
    /// File is added to staging area
    /// </summary>
    Added,

    /// <summary>
    /// File is modified but not staged
    /// </summary>
    Modified,

    /// <summary>
    /// File is deleted
    /// </summary>
    Deleted,

    /// <summary>
    /// File is not tracked by Git
    /// </summary>
    Untracked
}
