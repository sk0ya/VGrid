using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using VGrid.Commands;
using VGrid.Models;
using VGrid.VimEngine.Actions;
using VGrid.VimEngine.KeyBinding;

namespace VGrid.VimEngine;

/// <summary>
/// Specifies the type of command being entered in Command mode
/// </summary>
public enum CommandType
{
    /// <summary>
    /// Search command (triggered by '/')
    /// </summary>
    Search,

    /// <summary>
    /// Ex-command (triggered by ':')
    /// </summary>
    ExCommand
}

/// <summary>
/// Specifies where the caret should be positioned when entering insert mode
/// </summary>
public enum CellEditCaretPosition
{
    /// <summary>
    /// Position caret at the start of the cell content
    /// </summary>
    Start,

    /// <summary>
    /// Position caret at the end of the cell content
    /// </summary>
    End
}

/// <summary>
/// Central state manager for Vim behavior
/// </summary>
public class VimState : INotifyPropertyChanged
{
    private VimMode _currentMode = VimMode.Normal;
    private IVimMode? _modeHandler;
    private GridPosition _cursorPosition = new(0, 0);
    private int? _countPrefix;
    private SelectionRange? _currentSelection;
    private YankedContent? _lastYank;
    private readonly Dictionary<char, string> _registers = new();
    private readonly KeySequence _pendingKeys = new();
    private readonly HashSet<int> _selectedRows = new();
    private readonly HashSet<int> _selectedColumns = new();
    private int? _lastSelectedRowIndex;
    private int? _lastSelectedColumnIndex;
    private string _searchPattern = string.Empty;
    private List<GridPosition> _searchResults = new();
    private int _currentMatchIndex = -1;
    private bool _isSearchActive = false;
    private CellEditCaretPosition _cellEditCaretPosition = CellEditCaretPosition.End;
    private CommandType _commandType = CommandType.Search;
    private string _errorMessage = string.Empty;
    private SelectionRange? _pendingBulkEditRange;
    private string _originalCellValueForBulkEdit = string.Empty;
    private LastChange? _lastChange;
    private ChangeType _pendingInsertType = ChangeType.None;
    private GridPosition? _insertModeStartPosition;
    private string _insertModeOriginalValue = string.Empty;

    // Mode handlers
    private readonly Dictionary<VimMode, IVimMode> _modeHandlers = new();

    public VimState()
    {
        // Initialize mode handlers
        _modeHandlers[VimMode.Normal] = new NormalMode();
        _modeHandlers[VimMode.Insert] = new InsertMode();
        _modeHandlers[VimMode.Visual] = new VisualMode();
        _modeHandlers[VimMode.Command] = new CommandMode();

        // Set initial mode
        _modeHandler = _modeHandlers[VimMode.Normal];
    }

    /// <summary>
    /// The current Vim mode
    /// </summary>
    public VimMode CurrentMode
    {
        get => _currentMode;
        private set
        {
            if (_currentMode != value)
            {
                _currentMode = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// The current cursor position
    /// </summary>
    public GridPosition CursorPosition
    {
        get => _cursorPosition;
        set
        {
            if (_cursorPosition != value)
            {
                _cursorPosition = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// The count prefix for commands (e.g., "3" in "3j")
    /// </summary>
    public int? CountPrefix
    {
        get => _countPrefix;
        set
        {
            if (_countPrefix != value)
            {
                _countPrefix = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// The current visual mode selection range
    /// </summary>
    public SelectionRange? CurrentSelection
    {
        get => _currentSelection;
        set
        {
            if (_currentSelection != value)
            {
                _currentSelection = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// The last yanked content
    /// </summary>
    public YankedContent? LastYank
    {
        get => _lastYank;
        set
        {
            if (_lastYank != value)
            {
                _lastYank = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Set of selected row indices for header-based multi-selection
    /// </summary>
    public IReadOnlySet<int> SelectedRows => _selectedRows;

    /// <summary>
    /// Set of selected column indices for header-based multi-selection
    /// </summary>
    public IReadOnlySet<int> SelectedColumns => _selectedColumns;

    /// <summary>
    /// Toggles the selection state of a row
    /// </summary>
    public void ToggleRowSelection(int rowIndex)
    {
        if (_selectedRows.Contains(rowIndex))
            _selectedRows.Remove(rowIndex);
        else
        {
            _selectedRows.Add(rowIndex);
            _lastSelectedRowIndex = rowIndex;
        }

        OnPropertyChanged(nameof(SelectedRows));
    }

    /// <summary>
    /// Toggles the selection state of a column
    /// </summary>
    public void ToggleColumnSelection(int columnIndex)
    {
        if (_selectedColumns.Contains(columnIndex))
            _selectedColumns.Remove(columnIndex);
        else
        {
            _selectedColumns.Add(columnIndex);
            _lastSelectedColumnIndex = columnIndex;
        }

        OnPropertyChanged(nameof(SelectedColumns));
    }

    /// <summary>
    /// Clears all row selections
    /// </summary>
    public void ClearRowSelections()
    {
        if (_selectedRows.Count > 0)
        {
            _selectedRows.Clear();
            OnPropertyChanged(nameof(SelectedRows));
        }
    }

    /// <summary>
    /// Clears all column selections
    /// </summary>
    public void ClearColumnSelections()
    {
        if (_selectedColumns.Count > 0)
        {
            _selectedColumns.Clear();
            OnPropertyChanged(nameof(SelectedColumns));
        }
    }

    /// <summary>
    /// Sets a single row selection, clearing any previous row selections
    /// </summary>
    public void SetSingleRowSelection(int rowIndex)
    {
        _selectedRows.Clear();
        _selectedRows.Add(rowIndex);
        _lastSelectedRowIndex = rowIndex;
        OnPropertyChanged(nameof(SelectedRows));
    }

    /// <summary>
    /// Sets a single column selection, clearing any previous column selections
    /// </summary>
    public void SetSingleColumnSelection(int columnIndex)
    {
        _selectedColumns.Clear();
        _selectedColumns.Add(columnIndex);
        _lastSelectedColumnIndex = columnIndex;
        OnPropertyChanged(nameof(SelectedColumns));
    }

    /// <summary>
    /// Sets a range of row selections from the last selected row to the specified row
    /// </summary>
    public void SetRowRangeSelection(int endRowIndex)
    {
        if (_lastSelectedRowIndex == null)
        {
            SetSingleRowSelection(endRowIndex);
            return;
        }

        int startRow = Math.Min(_lastSelectedRowIndex.Value, endRowIndex);
        int endRow = Math.Max(_lastSelectedRowIndex.Value, endRowIndex);

        _selectedRows.Clear();
        for (int i = startRow; i <= endRow; i++)
        {
            _selectedRows.Add(i);
        }
        _lastSelectedRowIndex = endRowIndex;
        OnPropertyChanged(nameof(SelectedRows));
    }

    /// <summary>
    /// Sets a range of column selections from the last selected column to the specified column
    /// </summary>
    public void SetColumnRangeSelection(int endColumnIndex)
    {
        if (_lastSelectedColumnIndex == null)
        {
            SetSingleColumnSelection(endColumnIndex);
            return;
        }

        int startCol = Math.Min(_lastSelectedColumnIndex.Value, endColumnIndex);
        int endCol = Math.Max(_lastSelectedColumnIndex.Value, endColumnIndex);

        _selectedColumns.Clear();
        for (int i = startCol; i <= endCol; i++)
        {
            _selectedColumns.Add(i);
        }
        _lastSelectedColumnIndex = endColumnIndex;
        OnPropertyChanged(nameof(SelectedColumns));
    }

    /// <summary>
    /// The current search pattern
    /// </summary>
    public string SearchPattern
    {
        get => _searchPattern;
        set
        {
            if (_searchPattern != value)
            {
                _searchPattern = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// The list of search results
    /// </summary>
    public IReadOnlyList<GridPosition> SearchResults => _searchResults.AsReadOnly();

    /// <summary>
    /// The index of the current match in the search results
    /// </summary>
    public int CurrentMatchIndex
    {
        get => _currentMatchIndex;
        set
        {
            if (_currentMatchIndex != value)
            {
                _currentMatchIndex = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Indicates whether a search is currently active
    /// </summary>
    public bool IsSearchActive
    {
        get => _isSearchActive;
        set
        {
            if (_isSearchActive != value)
            {
                _isSearchActive = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Specifies where the caret should be positioned when entering insert mode
    /// </summary>
    public CellEditCaretPosition CellEditCaretPosition
    {
        get => _cellEditCaretPosition;
        set
        {
            if (_cellEditCaretPosition != value)
            {
                _cellEditCaretPosition = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// The type of command being entered in Command mode
    /// </summary>
    public CommandType CurrentCommandType
    {
        get => _commandType;
        set
        {
            if (_commandType != value)
            {
                _commandType = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Error message from command execution
    /// </summary>
    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage != value)
            {
                _errorMessage = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Pending bulk edit range for Visual mode 'i' and 'a' commands
    /// </summary>
    public SelectionRange? PendingBulkEditRange
    {
        get => _pendingBulkEditRange;
        set
        {
            if (_pendingBulkEditRange != value)
            {
                _pendingBulkEditRange = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Original cell value before bulk edit (used to detect inserted text)
    /// </summary>
    public string OriginalCellValueForBulkEdit
    {
        get => _originalCellValueForBulkEdit;
        set
        {
            if (_originalCellValueForBulkEdit != value)
            {
                _originalCellValueForBulkEdit = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// The last change operation for dot command replay
    /// </summary>
    public LastChange? LastChange
    {
        get => _lastChange;
        set
        {
            if (_lastChange != value)
            {
                _lastChange = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Tracks how we entered insert mode (for dot command)
    /// </summary>
    public ChangeType PendingInsertType
    {
        get => _pendingInsertType;
        set
        {
            if (_pendingInsertType != value)
            {
                _pendingInsertType = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// The cursor position when entering insert mode (for dot command tracking)
    /// </summary>
    public GridPosition? InsertModeStartPosition
    {
        get => _insertModeStartPosition;
        set
        {
            if (_insertModeStartPosition != value)
            {
                _insertModeStartPosition = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// The original cell value when entering insert mode (for dot command tracking)
    /// </summary>
    public string InsertModeOriginalValue
    {
        get => _insertModeOriginalValue;
        set
        {
            if (_insertModeOriginalValue != value)
            {
                _insertModeOriginalValue = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Command history for undo/redo operations
    /// </summary>
    public CommandHistory? CommandHistory { get; set; }

    /// <summary>
    /// Custom keybinding configuration loaded from .vimrc
    /// </summary>
    public KeyBindingConfig? KeyBindingConfig { get; set; }

    /// <summary>
    /// Registers for yank/paste operations
    /// </summary>
    public Dictionary<char, string> Registers => _registers;

    /// <summary>
    /// Pending keys for multi-key commands
    /// </summary>
    public KeySequence PendingKeys => _pendingKeys;

    /// <summary>
    /// Switches to a different mode
    /// </summary>
    public void SwitchMode(VimMode mode)
    {
        if (_currentMode == mode)
            return;

        // Exit current mode
        _modeHandler?.OnExit(this);

        // Update mode handler BEFORE updating CurrentMode
        // This ensures GetModeDisplayName() returns the correct value
        // when PropertyChanged fires
        _modeHandler = _modeHandlers[mode];

        // Switch mode (this triggers PropertyChanged)
        CurrentMode = mode;

        // Enter new mode
        _modeHandler.OnEnter(this);

        // Clear pending keys when switching modes
        _pendingKeys.Clear();
        CountPrefix = null;
    }

    /// <summary>
    /// Handles a key press
    /// </summary>
    /// <returns>True if the key was handled</returns>
    public bool HandleKey(Key key, ModifierKeys modifiers, TsvDocument document)
    {
        if (_modeHandler == null)
            return false;

        // Check for expired key sequences
        if (_pendingKeys.Keys.Count > 0 && _pendingKeys.IsExpired())
        {
            _pendingKeys.Clear();
            CountPrefix = null;
        }

        // Check for custom keybindings first
        if (TryExecuteCustomBinding(key, modifiers, document))
        {
            return true;
        }

        return _modeHandler.HandleKey(this, key, modifiers, document);
    }

    /// <summary>
    /// Tries to execute a custom keybinding if one is defined
    /// </summary>
    private bool TryExecuteCustomBinding(Key key, ModifierKeys modifiers, TsvDocument document)
    {
        if (KeyBindingConfig == null)
            return false;

        var binding = new KeyBinding.KeyBinding(key, modifiers);

        if (!KeyBindingConfig.TryGetAction(CurrentMode, binding, out var actionName) || actionName == null)
            return false;

        // Find the action in the registry
        if (!ActionRegistry.Instance.TryGetAction(actionName, out var action) || action == null)
        {
            System.Diagnostics.Debug.WriteLine($"[VimState] Unknown action: {actionName}");
            return false;
        }

        // Execute the action
        var count = CountPrefix ?? 1;
        var context = new VimActionContext(this, document, count);
        var handled = action.Execute(context);

        if (handled)
        {
            // Clear count prefix after successful execution
            CountPrefix = null;
            _pendingKeys.Clear();
        }

        return handled;
    }

    /// <summary>
    /// Resets the state (clears pending keys, count prefix, etc.)
    /// </summary>
    public void ResetState()
    {
        _pendingKeys.Clear();
        CountPrefix = null;
    }

    /// <summary>
    /// Gets the display name of the current mode
    /// </summary>
    public string GetModeDisplayName()
    {
        return _modeHandler?.GetModeName() ?? "UNKNOWN";
    }

    /// <summary>
    /// Sets the search results and moves to the first match
    /// </summary>
    public void SetSearchResults(List<GridPosition> results)
    {
        _searchResults = results;
        _currentMatchIndex = results.Count > 0 ? 0 : -1;
        _isSearchActive = results.Count > 0;
        OnPropertyChanged(nameof(SearchResults));
        OnPropertyChanged(nameof(CurrentMatchIndex));
        OnPropertyChanged(nameof(IsSearchActive));
    }

    /// <summary>
    /// Navigates to the next or previous search match
    /// </summary>
    public void NavigateToNextMatch(bool forward = true)
    {
        if (_searchResults.Count == 0)
            return;

        if (forward)
        {
            _currentMatchIndex = (_currentMatchIndex + 1) % _searchResults.Count;
        }
        else
        {
            _currentMatchIndex = (_currentMatchIndex - 1 + _searchResults.Count) % _searchResults.Count;
        }

        CursorPosition = _searchResults[_currentMatchIndex];
        OnPropertyChanged(nameof(CurrentMatchIndex));
    }

    /// <summary>
    /// Clears the current search
    /// </summary>
    public void ClearSearch()
    {
        _searchPattern = string.Empty;
        _searchResults.Clear();
        _currentMatchIndex = -1;
        _isSearchActive = false;
        _commandType = CommandType.Search;
        _errorMessage = string.Empty;
        OnPropertyChanged(nameof(SearchPattern));
        OnPropertyChanged(nameof(SearchResults));
        OnPropertyChanged(nameof(CurrentMatchIndex));
        OnPropertyChanged(nameof(IsSearchActive));
        OnPropertyChanged(nameof(ErrorMessage));
    }

    /// <summary>
    /// Event raised when a save operation is requested
    /// </summary>
    public event EventHandler? SaveRequested;

    /// <summary>
    /// Event raised when a quit operation is requested
    /// </summary>
    public event EventHandler<bool>? QuitRequested;

    /// <summary>
    /// Event raised when Vim search is activated (to coordinate with FindReplace panel)
    /// </summary>
    public event EventHandler? VimSearchActivated;

    /// <summary>
    /// Event raised when a yank operation is performed (to clear other tabs' LastYank)
    /// </summary>
    public event EventHandler? YankPerformed;

    /// <summary>
    /// Event raised when column widths should be updated after a paste operation
    /// </summary>
    public event EventHandler<IEnumerable<int>>? ColumnWidthUpdateRequested;

    /// <summary>
    /// Event raised when switching to the previous tab is requested
    /// </summary>
    public event EventHandler? PreviousTabRequested;

    /// <summary>
    /// Event raised when switching to the next tab is requested
    /// </summary>
    public event EventHandler? NextTabRequested;

    /// <summary>
    /// Event raised when scrolling current line to center is requested
    /// </summary>
    public event EventHandler? ScrollToCenterRequested;

    /// <summary>
    /// Raises the SaveRequested event
    /// </summary>
    public void OnSaveRequested()
    {
        SaveRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raises the QuitRequested event
    /// </summary>
    /// <param name="forceQuit">True if quit should be forced without prompting</param>
    public void OnQuitRequested(bool forceQuit)
    {
        QuitRequested?.Invoke(this, forceQuit);
    }

    /// <summary>
    /// Raises the VimSearchActivated event
    /// </summary>
    public void OnVimSearchActivated()
    {
        VimSearchActivated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raises the YankPerformed event
    /// </summary>
    public void OnYankPerformed()
    {
        YankPerformed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raises the ColumnWidthUpdateRequested event
    /// </summary>
    /// <param name="columnIndices">The column indices that need width updates</param>
    public void OnColumnWidthUpdateRequested(IEnumerable<int> columnIndices)
    {
        ColumnWidthUpdateRequested?.Invoke(this, columnIndices);
    }

    /// <summary>
    /// Raises the PreviousTabRequested event
    /// </summary>
    public void OnPreviousTabRequested()
    {
        PreviousTabRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raises the NextTabRequested event
    /// </summary>
    public void OnNextTabRequested()
    {
        NextTabRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raises the ScrollToCenterRequested event
    /// </summary>
    public void OnScrollToCenterRequested()
    {
        ScrollToCenterRequested?.Invoke(this, EventArgs.Empty);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Forces a PropertyChanged notification for CursorPosition to refresh UI bindings.
    /// Used when theme changes to update header colors.
    /// </summary>
    public void RefreshCursorPositionBinding()
    {
        OnPropertyChanged(nameof(CursorPosition));
    }
}
