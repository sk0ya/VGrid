using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using VGrid.Commands;
using VGrid.Helpers;
using VGrid.Models;
using VGrid.VimEngine;

namespace VGrid.ViewModels;

public class FindReplaceViewModel : ViewModelBase
{
    private readonly TsvDocument _document;
    private readonly VimState _vimState;
    private readonly CommandHistory _commandHistory;

    private string _searchText = string.Empty;
    private string _replaceText = string.Empty;
    private bool _isCaseSensitive;
    private bool _useRegex;
    private bool _isVisible;
    private int _currentMatchIndex = -1;

    private List<GridPosition> _searchResults = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ExecuteSearch();
            }
        }
    }

    public string ReplaceText
    {
        get => _replaceText;
        set => SetProperty(ref _replaceText, value);
    }

    public bool IsCaseSensitive
    {
        get => _isCaseSensitive;
        set
        {
            if (SetProperty(ref _isCaseSensitive, value))
            {
                ExecuteSearch();
            }
        }
    }

    public bool UseRegex
    {
        get => _useRegex;
        set
        {
            if (SetProperty(ref _useRegex, value))
            {
                ExecuteSearch();
            }
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public int CurrentMatchIndex
    {
        get => _currentMatchIndex;
        private set
        {
            if (SetProperty(ref _currentMatchIndex, value))
            {
                UpdateMatchCountText();
                UpdateHighlighting();
            }
        }
    }

    public string MatchCountText
    {
        get
        {
            if (string.IsNullOrEmpty(SearchText))
                return "Enter search term";

            if (_searchResults.Count == 0)
                return "No results";

            if (CurrentMatchIndex >= 0 && CurrentMatchIndex < _searchResults.Count)
                return $"{CurrentMatchIndex + 1} of {_searchResults.Count}";

            return $"{_searchResults.Count} matches";
        }
    }

    public System.Windows.Input.ICommand FindNextCommand { get; }
    public System.Windows.Input.ICommand FindPreviousCommand { get; }
    public System.Windows.Input.ICommand ReplaceCommand { get; }
    public System.Windows.Input.ICommand ReplaceAllCommand { get; }
    public System.Windows.Input.ICommand CloseCommand { get; }

    public FindReplaceViewModel(TsvDocument document, VimState vimState, CommandHistory commandHistory)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _vimState = vimState ?? throw new ArgumentNullException(nameof(vimState));
        _commandHistory = commandHistory ?? throw new ArgumentNullException(nameof(commandHistory));

        FindNextCommand = new RelayCommand(_ => FindNext(), _ => _searchResults.Count > 0);
        FindPreviousCommand = new RelayCommand(_ => FindPrevious(), _ => _searchResults.Count > 0);
        ReplaceCommand = new RelayCommand(_ => Replace(), _ => CurrentMatchIndex >= 0 && CurrentMatchIndex < _searchResults.Count);
        ReplaceAllCommand = new RelayCommand(_ => ReplaceAll(), _ => _searchResults.Count > 0);
        CloseCommand = new RelayCommand(_ => Close());
    }

    public void Open()
    {
        // Clear Vim search highlighting
        _vimState.ClearSearch();

        bool wasVisible = IsVisible;

        // Show panel
        IsVisible = true;

        // If panel was already visible, manually trigger property change to refocus
        if (wasVisible)
        {
            OnPropertyChanged(nameof(IsVisible));
        }
    }

    public void Close()
    {
        // Hide panel
        IsVisible = false;

        // Clear highlighting
        ClearHighlighting();

        // Clear search results
        _searchResults.Clear();
        CurrentMatchIndex = -1;
    }

    private void ExecuteSearch()
    {
        if (string.IsNullOrEmpty(SearchText))
        {
            ClearHighlighting();
            _searchResults.Clear();
            CurrentMatchIndex = -1;
            OnPropertyChanged(nameof(MatchCountText));
            return;
        }

        // Perform search using enhanced TsvDocument.FindMatches
        _searchResults = _document.FindMatches(SearchText, UseRegex, IsCaseSensitive);

        // Update current match index
        CurrentMatchIndex = _searchResults.Count > 0 ? 0 : -1;

        // Update highlighting
        UpdateHighlighting();

        // Move cursor to first match - create new GridPosition to ensure change detection
        if (CurrentMatchIndex >= 0)
        {
            var newPosition = _searchResults[CurrentMatchIndex];
            _vimState.CursorPosition = new GridPosition(newPosition.Row, newPosition.Column);
        }

        // Update match count text
        OnPropertyChanged(nameof(MatchCountText));
    }

    private void FindNext()
    {
        if (_searchResults.Count == 0)
            return;

        // Wrap around to beginning
        if (CurrentMatchIndex >= _searchResults.Count - 1)
            CurrentMatchIndex = 0;
        else
            CurrentMatchIndex++;

        // Move cursor to match - create new GridPosition to ensure change detection
        var newPosition = _searchResults[CurrentMatchIndex];
        _vimState.CursorPosition = new GridPosition(newPosition.Row, newPosition.Column);
    }

    private void FindPrevious()
    {
        if (_searchResults.Count == 0)
            return;

        // Wrap around to end
        if (CurrentMatchIndex <= 0)
            CurrentMatchIndex = _searchResults.Count - 1;
        else
            CurrentMatchIndex--;

        // Move cursor to match - create new GridPosition to ensure change detection
        var newPosition = _searchResults[CurrentMatchIndex];
        _vimState.CursorPosition = new GridPosition(newPosition.Row, newPosition.Column);
    }

    private void Replace()
    {
        if (CurrentMatchIndex < 0 || CurrentMatchIndex >= _searchResults.Count)
            return;

        var position = _searchResults[CurrentMatchIndex];
        var cell = _document.GetCell(position);
        if (cell == null)
            return;

        string oldValue = cell.Value;
        string newValue;

        if (UseRegex)
        {
            // Regex replacement
            try
            {
                var options = IsCaseSensitive
                    ? RegexOptions.None
                    : RegexOptions.IgnoreCase;
                var regex = new Regex(SearchText, options);
                newValue = regex.Replace(oldValue, ReplaceText);
            }
            catch
            {
                // Invalid regex or replacement
                return;
            }
        }
        else
        {
            // Plain text replacement - replace only first occurrence
            var comparison = IsCaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            int index = oldValue.IndexOf(SearchText, comparison);
            if (index >= 0)
            {
                newValue = oldValue.Remove(index, SearchText.Length)
                                  .Insert(index, ReplaceText);
            }
            else
            {
                return;
            }
        }

        // Execute replace command with undo support
        var command = new FindReplaceCommand(_document, position, oldValue, newValue);
        _commandHistory.Execute(command);

        // Re-execute search to update results
        ExecuteSearch();

        // Move to next match (or wrap to beginning)
        if (_searchResults.Count > 0)
        {
            if (CurrentMatchIndex < _searchResults.Count)
            {
                // Stay at same position if there's a match there
                // Otherwise move to next
                _vimState.CursorPosition = _searchResults[CurrentMatchIndex];
            }
            else if (CurrentMatchIndex >= _searchResults.Count)
            {
                CurrentMatchIndex = 0;
                _vimState.CursorPosition = _searchResults[CurrentMatchIndex];
            }
        }
    }

    private void ReplaceAll()
    {
        if (_searchResults.Count == 0)
            return;

        // Confirm with user
        var result = System.Windows.MessageBox.Show(
            $"Replace all {_searchResults.Count} occurrences?",
            "Confirm Replace All",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes)
            return;

        // Build replacement dictionary
        var replacements = new Dictionary<GridPosition, (string oldValue, string newValue)>();

        foreach (var position in _searchResults)
        {
            var cell = _document.GetCell(position);
            if (cell == null)
                continue;

            string oldValue = cell.Value;
            string newValue;

            if (UseRegex)
            {
                try
                {
                    var options = IsCaseSensitive
                        ? RegexOptions.None
                        : RegexOptions.IgnoreCase;
                    var regex = new Regex(SearchText, options);
                    newValue = regex.Replace(oldValue, ReplaceText);
                }
                catch
                {
                    continue;
                }
            }
            else
            {
                // Plain text replacement - replace all occurrences in the cell
                var comparison = IsCaseSensitive
                    ? StringComparison.Ordinal
                    : StringComparison.OrdinalIgnoreCase;

                // Use Replace with StringComparison (requires specific logic for case-insensitive)
                if (IsCaseSensitive)
                {
                    newValue = oldValue.Replace(SearchText, ReplaceText);
                }
                else
                {
                    // Case-insensitive replace
                    newValue = Regex.Replace(oldValue, Regex.Escape(SearchText), ReplaceText, RegexOptions.IgnoreCase);
                }
            }

            replacements[position] = (oldValue, newValue);
        }

        // Execute bulk replace command
        var command = new BulkFindReplaceCommand(_document, replacements);
        _commandHistory.Execute(command);

        // Re-execute search
        ExecuteSearch();
    }

    private void ClearHighlighting()
    {
        // Clear IsSearchMatch on all cells
        foreach (var row in _document.Rows)
        {
            foreach (var cell in row.Cells)
            {
                cell.IsSearchMatch = false;
            }
        }
    }

    private void UpdateHighlighting()
    {
        // First clear all highlighting
        ClearHighlighting();

        // Highlight all matches
        foreach (var position in _searchResults)
        {
            var cell = _document.GetCell(position);
            if (cell != null)
            {
                cell.IsSearchMatch = true;
            }
        }
    }

    private void UpdateMatchCountText()
    {
        OnPropertyChanged(nameof(MatchCountText));
    }
}
