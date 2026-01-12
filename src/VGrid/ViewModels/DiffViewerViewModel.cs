using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using VGrid.Helpers;
using VGrid.Models;
using VGrid.Services;

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

    private string? _selectedFile;

    public DiffViewerViewModel(
        string repoRoot,
        string? commit1Hash,
        string? commit2Hash,
        IGitService gitService)
    {
        _repoRoot = repoRoot;
        _commit1Hash = commit1Hash;
        _commit2Hash = commit2Hash;
        _gitService = gitService;

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
                return $"Working Directory vs {_commit2Hash?[..7]}";
            else if (_commit2Hash == null)
                return $"{_commit1Hash[..7]} vs Working Directory";
            else
                return $"{_commit1Hash[..7]} vs {_commit2Hash[..7]}";
        }
    }

    public string LeftHeader
    {
        get
        {
            if (_commit1Hash == null)
                return "Working Directory";
            else
                return _commit1Hash[..7];
        }
    }

    public string RightHeader
    {
        get
        {
            if (_commit2Hash == null)
                return "Working Directory";
            else
                return _commit2Hash[..7];
        }
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

        // Auto-select first TSV file if exists
        var firstTsv = files.FirstOrDefault(f =>
            f.EndsWith(".tsv", StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
            f.EndsWith(".tab", StringComparison.OrdinalIgnoreCase));

        if (firstTsv != null)
        {
            SelectedFile = firstTsv;
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

        int maxRows = Math.Max(leftLines.Length, rightLines.Length);
        int maxCols = 0;

        var leftRowData = new List<string[]>();
        var rightRowData = new List<string[]>();

        for (int i = 0; i < maxRows; i++)
        {
            var leftCells = i < leftLines.Length ? leftLines[i].Split('\t') : Array.Empty<string>();
            var rightCells = i < rightLines.Length ? rightLines[i].Split('\t') : Array.Empty<string>();

            leftRowData.Add(leftCells);
            rightRowData.Add(rightCells);

            maxCols = Math.Max(maxCols, Math.Max(leftCells.Length, rightCells.Length));
        }

        // Ensure at least 1 column
        if (maxCols == 0)
            maxCols = 1;

        // Build diff rows for horizontal layout (left-right DataGrids)
        LeftRows.Clear();
        RightRows.Clear();

        for (int i = 0; i < maxRows; i++)
        {
            var leftCells = leftRowData[i];
            var rightCells = rightRowData[i];

            // Row index is 1-based for display
            var leftRow = new DiffRow(i + 1, maxCols);
            var rightRow = new DiffRow(i + 1, maxCols);

            // Fill cells and detect changes
            for (int j = 0; j < maxCols; j++)
            {
                var leftValue = j < leftCells.Length ? leftCells[j] : string.Empty;
                var rightValue = j < rightCells.Length ? rightCells[j] : string.Empty;

                leftRow.Cells[j].Value = leftValue;
                rightRow.Cells[j].Value = rightValue;

                // Determine cell status
                if (leftValue != rightValue)
                {
                    if (string.IsNullOrEmpty(leftValue))
                    {
                        leftRow.Cells[j].Status = DiffStatus.Added;
                        rightRow.Cells[j].Status = DiffStatus.Added;
                    }
                    else if (string.IsNullOrEmpty(rightValue))
                    {
                        leftRow.Cells[j].Status = DiffStatus.Deleted;
                        rightRow.Cells[j].Status = DiffStatus.Deleted;
                    }
                    else
                    {
                        leftRow.Cells[j].Status = DiffStatus.Modified;
                        rightRow.Cells[j].Status = DiffStatus.Modified;
                    }
                }
            }

            LeftRows.Add(leftRow);
            RightRows.Add(rightRow);
        }
    }
}
