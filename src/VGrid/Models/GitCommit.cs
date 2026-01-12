namespace VGrid.Models;

/// <summary>
/// Represents a git commit
/// </summary>
public class GitCommit
{
    public string Hash { get; init; } = string.Empty;

    public string ShortHash => Hash.Length > 7 ? Hash[..7] : Hash;

    public string AuthorName { get; init; } = string.Empty;

    public string AuthorEmail { get; init; } = string.Empty;

    public DateTime CommitDate { get; init; }

    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Display format for ListBox (Date, Message, Author, CommitID)
    /// </summary>
    public string DisplayText => $"{CommitDate:yyyy-MM-dd HH:mm} - {Message} - {AuthorName} - {ShortHash}";
}
