using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using VGrid.Commands;
using VGrid.Models;

namespace VGrid.VimEngine;

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

    // Mode handlers
    private readonly Dictionary<VimMode, IVimMode> _modeHandlers = new();

    public VimState()
    {
        // Initialize mode handlers
        _modeHandlers[VimMode.Normal] = new NormalMode();
        _modeHandlers[VimMode.Insert] = new InsertMode();
        _modeHandlers[VimMode.Visual] = new VisualMode();

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
    /// Command history for undo/redo operations
    /// </summary>
    public CommandHistory? CommandHistory { get; set; }

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

        return _modeHandler.HandleKey(this, key, modifiers, document);
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

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
