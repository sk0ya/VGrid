using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using VGrid.Helpers;
using VGrid.Models;
using VGrid.Services;
using static VGrid.Services.DiffAlgorithm;

namespace VGrid.ViewModels;

/// <summary>
/// ViewModel for the diff viewer window
/// </summary>
public class DiffViewerViewModel : ViewModelBase
{
    private readonly IGitService _gitService;
    private readonly string _repoRoot;
    private readonly string? _commit1Hash;
    private readonly string? _commit2Hash;
    private readonly string? _initialSelectedFile;

    private string? _selectedFile;

    public DiffViewerViewModel(
        string repoRoot,
        string? commit1Hash,
        string? commit2Hash,
        IGitService gitService,
        string? initialSelectedFile = null)
    {
        _repoRoot = repoRoot;
        _commit1Hash = commit1Hash;
        _commit2Hash = commit2Hash;
        _gitService = gitService;
        _initialSelectedFile = initialSelectedFile;

        ChangedFiles = new ObservableCollection<string>();
        LeftRows = new ObservableCollection<DiffRow>();
        RightRows = new ObservableCollection<DiffRow>();

        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));

        // Load changed files on initialization
        _ = LoadChangedFilesAsync();
    }

    public ObservableCollection<string> ChangedFiles { get; }
    public ObservableCollection<DiffRow> LeftRows { get; }
    public ObservableCollection<DiffRow> RightRows { get; }

    public string? SelectedFile
    {
        get => _selectedFile;
        set
        {
            if (SetProperty(ref _selectedFile, value) && value != null)
            {
                _ = LoadFileDiffAsync(value);
            }
        }
    }

    public string DiffTitle
    {
        get
        {
            if (_commit1Hash == null && _commit2Hash == null)
                return "No commits selected";
            else if (_commit1Hash == null)
                return $"Working Directory vs {GetShortCommitHash(_commit2Hash)}";
            else if (_commit2Hash == null)
                return $"{GetShortCommitHash(_commit1Hash)} vs Working Directory";
            else
                return $"{GetShortCommitHash(_commit1Hash)} vs {GetShortCommitHash(_commit2Hash)}";
        }
    }

    public string LeftHeader
    {
        get
        {
            if (_commit1Hash == null)
                return "Working Directory";
            else
                return GetShortCommitHash(_commit1Hash);
        }
    }

    public string RightHeader
    {
        get
        {
            if (_commit2Hash == null)
                return "Working Directory";
            else
                return GetShortCommitHash(_commit2Hash);
        }
    }

    /// <summary>
    /// Gets short version of commit hash or the full string if it's too short (like "HEAD")
    /// </summary>
    private string GetShortCommitHash(string? hash)
    {
        if (string.IsNullOrEmpty(hash))
            return string.Empty;

        // If it's a special ref like "HEAD" or short already, return as-is
        if (hash.Length <= 7)
            return hash;

        // Otherwise return first 7 characters
        return hash[..7];
    }

    public RelayCommand CloseCommand { get; }

    public event EventHandler? CloseRequested;

    private async Task LoadChangedFilesAsync()
    {
        var files = await _gitService.GetChangedFilesAsync(_repoRoot, _commit1Hash, _commit2Hash);
        ChangedFiles.Clear();
        foreach (var file in files)
        {
            ChangedFiles.Add(file);
        }

        // Determine which file to select
        string? fileToSelect = null;

        // First priority: use initial selected file if specified and exists in changed files
        if (!string.IsNullOrEmpty(_initialSelectedFile))
        {
            // Try exact match first
            fileToSelect = ChangedFiles.FirstOrDefault(f =>
                f.Equals(_initialSelectedFile, StringComparison.OrdinalIgnoreCase));

            // If not found, try normalizing path separators
            if (fileToSelect == null)
            {
                var normalizedInitial = _initialSelectedFile.Replace('\\', '/');
                fileToSelect = ChangedFiles.FirstOrDefault(f =>
                    f.Replace('\\', '/').Equals(normalizedInitial, StringComparison.OrdinalIgnoreCase));
            }
        }

        // If no initial file specified or not found, auto-select first TSV file
        if (fileToSelect == null)
        {
            fileToSelect = ChangedFiles.FirstOrDefault(f =>
                f.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
                f.EndsWith(".tab", StringComparison.OrdinalIgnoreCase));
        }

        // Set the selected file
        if (fileToSelect != null)
        {
            SelectedFile = fileToSelect;
        }
    }

    private async Task LoadFileDiffAsync(string relativeFilePath)
    {
        var fullPath = Path.Combine(_repoRoot, relativeFilePath);

        string leftContent, rightContent;

        if (_commit1Hash == null)
        {
            // Working directory vs commit2
            leftContent = File.Exists(fullPath) ? await File.ReadAllTextAsync(fullPath) : string.Empty;
            rightContent = await _gitService.GetFileAtCommitAsync(_repoRoot, relativeFilePath, _commit2Hash!);
        }
        else if (_commit2Hash == null)
        {
            // Commit1 vs working directory
            leftContent = await _gitService.GetFileAtCommitAsync(_repoRoot, relativeFilePath, _commit1Hash);
            rightContent = File.Exists(fullPath) ? await File.ReadAllTextAsync(fullPath) : string.Empty;
        }
        else
        {
            // Commit1 vs commit2
            leftContent = await _gitService.GetFileAtCommitAsync(_repoRoot, relativeFilePath, _commit1Hash);
            rightContent = await _gitService.GetFileAtCommitAsync(_repoRoot, relativeFilePath, _commit2Hash);
        }

        ComputeAndDisplayDiff(leftContent, rightContent);
    }

    private void ComputeAndDisplayDiff(string leftContent, string rightContent)
    {
        // Parse TSV content - keep empty lines
        var leftLines = leftContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var rightLines = rightContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        // Remove trailing empty line if present (from split)
        if (leftLines.Length > 0 && string.IsNullOrEmpty(leftLines[^1]))
            leftLines = leftLines[..^1];
        if (rightLines.Length > 0 && string.IsNullOrEmpty(rightLines[^1]))
            rightLines = rightLines[..^1];

        // Compute line-level diff using proper algorithm
        var diffLines = DiffAlgorithm.ComputeDiff(leftLines, rightLines);

        // Calculate maximum number of columns needed
        int maxCols = 1;
        foreach (var line in leftLines)
        {
            var cells = line.Split('\t');
            maxCols = Math.Max(maxCols, cells.Length);
        }
        foreach (var line in rightLines)
        {
            var cells = line.Split('\t');
            maxCols = Math.Max(maxCols, cells.Length);
        }

        // Build diff rows for display
        LeftRows.Clear();
        RightRows.Clear();

        foreach (var diffLine in diffLines)
        {
            DiffRow leftRow, rightRow;

            switch (diffLine.Type)
            {
                case DiffOperationType.Unchanged:
                    // Both sides show the same content
                    leftRow = CreateDiffRow(diffLine.LeftLineNumber, diffLine.LeftContent!, maxCols, DiffStatus.Unchanged);
                    rightRow = CreateDiffRow(diffLine.RightLineNumber, diffLine.RightContent!, maxCols, DiffStatus.Unchanged);
                    break;

                case DiffOperationType.Deleted:
                    // Left side shows deleted line, right side is empty
                    leftRow = CreateDiffRow(diffLine.LeftLineNumber, diffLine.LeftContent!, maxCols, DiffStatus.Deleted);
                    rightRow = CreateDiffRow(null, string.Empty, maxCols, DiffStatus.Deleted);
                    break;

                case DiffOperationType.Added:
                    // Right side shows added line, left side is empty
                    leftRow = CreateDiffRow(null, string.Empty, maxCols, DiffStatus.Added);
                    rightRow = CreateDiffRow(diffLine.RightLineNumber, diffLine.RightContent!, maxCols, DiffStatus.Added);
                    break;

                case DiffOperationType.Modified:
                    // Both sides shown with cell-level diff highlighting
                    leftRow = new DiffRow(diffLine.LeftLineNumber, diffLine.RightLineNumber, maxCols, DiffStatus.Modified);
                    rightRow = new DiffRow(diffLine.LeftLineNumber, diffLine.RightLineNumber, maxCols, DiffStatus.Modified);

                    var leftCells = diffLine.LeftContent!.Split('\t');
                    var rightCells = diffLine.RightContent!.Split('\t');

                    // Compare cells individually
                    for (int j = 0; j < maxCols; j++)
                    {
                        var leftValue = j < leftCells.Length ? leftCells[j] : string.Empty;
                        var rightValue = j < rightCells.Length ? rightCells[j] : string.Empty;

                        leftRow.Cells[j].Value = leftValue;
                        rightRow.Cells[j].Value = rightValue;

                        // Mark cell status
                        if (leftValue != rightValue)
                        {
                            leftRow.Cells[j].Status = DiffStatus.Modified;
                            rightRow.Cells[j].Status = DiffStatus.Modified;
                        }
                        else
                        {
                            leftRow.Cells[j].Status = DiffStatus.Unchanged;
                            rightRow.Cells[j].Status = DiffStatus.Unchanged;
                        }
                    }
                    break;

                default:
                    continue;
            }

            LeftRows.Add(leftRow);
            RightRows.Add(rightRow);
        }
    }

    /// <summary>
    /// Creates a DiffRow from a line of TSV content
    /// </summary>
    private DiffRow CreateDiffRow(int? lineNumber, string content, int columnCount, DiffStatus status)
    {
        var row = new DiffRow(lineNumber, lineNumber, columnCount, status);
        var cells = content.Split('\t');

        for (int i = 0; i < columnCount; i++)
        {
            row.Cells[i].Value = i < cells.Length ? cells[i] : string.Empty;
            row.Cells[i].Status = status;
        }

        return row;
    }
}
