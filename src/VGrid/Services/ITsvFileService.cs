using VGrid.Models;

namespace VGrid.Services;

/// <summary>
/// Service for loading and saving TSV files
/// </summary>
public interface ITsvFileService
{
    /// <summary>
    /// Loads a TSV document from the specified file path
    /// </summary>
    Task<TsvDocument> LoadAsync(string filePath);

    /// <summary>
    /// Saves the TSV document to the specified file path
    /// </summary>
    Task SaveAsync(TsvDocument document, string filePath);

    /// <summary>
    /// Validates whether the file at the specified path is a valid TSV file
    /// </summary>
    bool ValidateTsv(string filePath);
}
