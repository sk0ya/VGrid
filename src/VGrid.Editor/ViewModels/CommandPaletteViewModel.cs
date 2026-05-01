using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using VGrid.Helpers;
using VGrid.Models;
using VGrid.VimEngine;
using VGrid.VimEngine.Actions;
using VGrid.VimEngine.KeyBinding;

namespace VGrid.ViewModels;

/// <summary>
/// ViewModel for the command palette
/// </summary>
public class CommandPaletteViewModel : ViewModelBase
{
    private readonly TsvDocument _document;
    private readonly VimState _vimState;
    private readonly Func<string?> _getFolderPath;
    private readonly Action<string>? _openFileAction;

    private string _filterText = string.Empty;
    private bool _isVisible;
    private int _selectedIndex;
    private CommandPaletteMode _currentMode = CommandPaletteMode.All;
    private readonly List<CommandPaletteItem> _commandItems;
    private List<CommandPaletteItem> _fileItems = new();

    private static readonly string[] SupportedExtensions = { ".tsv", ".txt", ".tab", ".csv" };

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
            {
                UpdateFilteredItems();
            }
        }
    }

    public bool IsVisible
    {
        get => _isVisible;
        set => SetProperty(ref _isVisible, value);
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetProperty(ref _selectedIndex, value);
    }

    public CommandPaletteMode CurrentMode
    {
        get => _currentMode;
        set
        {
            if (SetProperty(ref _currentMode, value))
            {
                OnPropertyChanged(nameof(ModeText));
                UpdateFilteredItems();
            }
        }
    }

    public string ModeText => CurrentMode switch
    {
        CommandPaletteMode.All => "All",
        CommandPaletteMode.Commands => "Commands",
        CommandPaletteMode.Files => "Files",
        _ => "All"
    };

    public ObservableCollection<CommandPaletteItem> FilteredItems { get; } = new();

    public ICommand CloseCommand { get; }

    public CommandPaletteViewModel(TsvDocument document, VimState vimState, Func<string?> getFolderPath, Action<string>? openFileAction = null)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _vimState = vimState ?? throw new ArgumentNullException(nameof(vimState));
        _getFolderPath = getFolderPath ?? throw new ArgumentNullException(nameof(getFolderPath));
        _openFileAction = openFileAction;

        CloseCommand = new RelayCommand(_ => Close());

        // Load all command actions
        _commandItems = LoadAllActions();
    }

    private List<CommandPaletteItem> LoadAllActions()
    {
        var items = new List<CommandPaletteItem>();
        var registry = ActionRegistry.Instance;

        foreach (var action in registry.GetAllActions())
        {
            var displayName = CommandPaletteItem.CreateDisplayName(action.Name);
            var keyBinding = DefaultKeyBindings.GetKeyBindingForAction(action.Name);
            items.Add(new CommandPaletteItem(action.Name, displayName, keyBinding, action));
        }

        return items.OrderBy(i => i.DisplayName).ToList();
    }

    private void LoadFileItems()
    {
        _fileItems.Clear();

        var folderPath = _getFolderPath();
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            return;

        try
        {
            var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Take(1000) // Limit to avoid performance issues
                .ToList();

            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(folderPath, file);
                _fileItems.Add(new CommandPaletteItem(file, relativePath));
            }

            _fileItems = _fileItems.OrderBy(f => f.RelativePath).ToList();
        }
        catch
        {
            // Ignore errors when enumerating files
        }
    }

    private void UpdateFilteredItems()
    {
        FilteredItems.Clear();

        var filter = _filterText?.Trim().ToLowerInvariant() ?? string.Empty;

        IEnumerable<CommandPaletteItem> sourceItems = CurrentMode switch
        {
            CommandPaletteMode.Commands => _commandItems,
            CommandPaletteMode.Files => _fileItems,
            _ => _commandItems.Concat(_fileItems)
        };

        IEnumerable<CommandPaletteItem> filtered;
        if (string.IsNullOrEmpty(filter))
        {
            filtered = sourceItems;
        }
        else
        {
            filtered = sourceItems.Where(item =>
            {
                if (item.ItemType == CommandPaletteItemType.Command)
                {
                    return item.DisplayName.ToLowerInvariant().Contains(filter) ||
                           item.ActionName.ToLowerInvariant().Contains(filter) ||
                           item.KeyBinding.ToLowerInvariant().Contains(filter);
                }
                else
                {
                    return item.DisplayName.ToLowerInvariant().Contains(filter) ||
                           (item.RelativePath?.ToLowerInvariant().Contains(filter) ?? false);
                }
            });
        }

        foreach (var item in filtered.Take(200)) // Limit displayed items
        {
            FilteredItems.Add(item);
        }

        // Reset selection to first item
        SelectedIndex = FilteredItems.Count > 0 ? 0 : -1;
    }

    public void Open()
    {
        // Load files first before any filtering
        LoadFileItems();

        // Set properties without triggering multiple updates
        _filterText = string.Empty;
        OnPropertyChanged(nameof(FilterText));

        _currentMode = CommandPaletteMode.All;
        OnPropertyChanged(nameof(CurrentMode));
        OnPropertyChanged(nameof(ModeText));

        // Now update filtered items with all data loaded
        UpdateFilteredItems();

        bool wasVisible = IsVisible;
        IsVisible = true;

        // Trigger property change even if already visible (for refocus)
        if (wasVisible)
        {
            OnPropertyChanged(nameof(IsVisible));
        }
    }

    /// <summary>
    /// Cycles to the next mode (All -> Commands -> Files -> All)
    /// </summary>
    public void CycleMode()
    {
        CurrentMode = CurrentMode switch
        {
            CommandPaletteMode.All => CommandPaletteMode.Commands,
            CommandPaletteMode.Commands => CommandPaletteMode.Files,
            CommandPaletteMode.Files => CommandPaletteMode.All,
            _ => CommandPaletteMode.All
        };
    }

    public void Close()
    {
        IsVisible = false;
        FilterText = string.Empty;
    }

    public void MoveUp()
    {
        if (FilteredItems.Count == 0)
            return;

        if (SelectedIndex > 0)
            SelectedIndex--;
        else
            SelectedIndex = FilteredItems.Count - 1; // Wrap to bottom
    }

    public void MoveDown()
    {
        if (FilteredItems.Count == 0)
            return;

        if (SelectedIndex < FilteredItems.Count - 1)
            SelectedIndex++;
        else
            SelectedIndex = 0; // Wrap to top
    }

    public bool ExecuteSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= FilteredItems.Count)
            return false;

        var item = FilteredItems[SelectedIndex];
        Close();

        if (item.ItemType == CommandPaletteItemType.Command)
        {
            // Execute the action
            if (item.Action != null)
            {
                var context = new VimActionContext(_vimState, _document);
                return item.Action.Execute(context);
            }
        }
        else if (item.ItemType == CommandPaletteItemType.File)
        {
            // Open the file
            if (!string.IsNullOrEmpty(item.FilePath) && _openFileAction != null)
            {
                _openFileAction(item.FilePath);
                return true;
            }
        }

        return false;
    }
}
