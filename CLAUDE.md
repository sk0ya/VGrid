# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

VGrid is a WPF-based TSV (Tab-Separated Values) editor with Vim keybindings. The application uses .NET 8 and implements a modal editing system (Normal, Insert, Visual, Command modes) similar to Vim, specifically designed for editing tabular data.

## Build and Test Commands

### Build
```bash
dotnet build
```

### Run the application
```bash
cd src\VGrid
dotnet run
```

### Run tests
```bash
dotnet test
```

### Run a specific test
```bash
dotnet test --filter "FullyQualifiedName~TestName"
```

## Architecture

### MVVM Pattern
The application follows WPF MVVM (Model-View-ViewModel) architecture:
- **Models** (`Models/`): Data structures (`TsvDocument`, `Row`, `Cell`, `GridPosition`, `DiffCell`, `DiffRow`, `GitCommit`)
- **ViewModels** (`ViewModels/`): Application logic and state management (`MainViewModel`, `TsvGridViewModel`, `TabItemViewModel`, `StatusBarViewModel`, `DiffViewerViewModel`, `GitHistoryViewModel`)
- **Views** (`Views/`): XAML windows and user controls

### Vim Engine (State Pattern)
The Vim modal editing system is implemented using the State Pattern in `VimEngine/`:
- **VimState**: Central state manager that coordinates mode switching and key handling
- **IVimMode**: Interface defining mode behavior (`HandleKey`, `OnEnter`, `OnExit`)
- **Mode Implementations**: `NormalMode`, `InsertMode`, `VisualMode`, `CommandMode` each implement `IVimMode`
- **KeySequence**: Handles multi-key commands like `gg`, `yy`, `dd` with timeout-based expiration
- **YankedContent**: Stores yanked (copied) content with type information (Character/Line/Block)
- **SelectionRange**: Represents visual mode selection range
- **ExCommandParser**: Parses ex-commands (`:w`, `:q`, `:wq`, etc.)
- **LastChange**: Tracks last change for dot command repeat functionality
- **Actions/**: Action system with 30+ registered actions for key binding customization
- **KeyBinding/**: Key binding configuration system
- **Vimrc/**: .vimrc file parsing and loading support

Key flow:
1. User presses key → MainWindow/ViewModel receives input
2. VimState.HandleKey() delegates to current mode's IVimMode.HandleKey()
3. Mode implementations mutate VimState (cursor position, mode switches) and TsvDocument (content edits)
4. VimState raises PropertyChanged events
5. ViewModels observe changes and update UI via data binding

### Command Pattern (Undo/Redo)
Edit operations use Command Pattern for undo/redo support:
- **ICommand**: Interface with `Execute()` and `Undo()` methods
- **CommandHistory**: Maintains undo/redo stacks (max 100 commands)
- **Implemented Commands**:
  - `EditCellCommand`: Cell editing
  - `DeleteRowCommand`: Row deletion
  - `DeleteColumnCommand`: Column deletion
  - `DeleteSelectionCommand`: Selection clearing
  - `InsertRowCommand`: Row insertion
  - `InsertColumnCommand`: Column insertion
  - `PasteCommand`: Paste operations (line/block/character)
  - `PasteOverSelectionCommand`: Paste over visual selection
  - `BulkEditCellsCommand`: Multi-cell editing
  - `BulkFindReplaceCommand`: Find/replace in selection
  - `AlignColumnsCommand`: Column alignment with CJK support
  - `SortCommand`: Row sorting by column

When editing cells, wrap operations in commands and execute via `CommandHistory.Execute()`. The `u` key in Normal mode triggers undo, `Ctrl+R` triggers redo.

### Tab Management
The application supports multiple open TSV files via tabs:
- Each tab has its own `TsvDocument`, `VimState`, and `TsvGridViewModel`
- `MainViewModel` manages the tab collection and coordinates file operations
- VimState changes in the active tab update the global `StatusBarViewModel`
- Tab navigation via `<` and `>` keys

### File I/O
- **ITsvFileService**: Interface for file operations
- **TsvFileService**: Implementation using async file I/O
- Supports `.tsv`, `.txt`, `.tab` extensions
- Tab-separated format (splits on `\t`, joins with `\t`)

### Additional Services
- **IGitService / GitService**: Git operations and diff generation
- **ISettingsService / SettingsService**: Application settings persistence
- **IColumnWidthService / ColumnWidthService**: Column width calculation with CJK support
- **ITemplateService / TemplateService**: Template file handling
- **IVimrcService / VimrcService**: .vimrc file parsing
- **ThemeService**: Theme management

## Key Code Locations

### Vim Mode Handling
- Normal mode navigation: `src/VGrid/VimEngine/NormalMode.cs`
- Insert mode editing: `src/VGrid/VimEngine/InsertMode.cs`
- Visual mode selection: `src/VGrid/VimEngine/VisualMode.cs`
- Command mode (search and ex-commands): `src/VGrid/VimEngine/CommandMode.cs`
- Multi-key sequences (e.g., `gg`, `yy`, `dd`): `src/VGrid/VimEngine/KeySequence.cs`
- Yanked content storage: `src/VGrid/VimEngine/YankedContent.cs`
- Ex-command parsing: `src/VGrid/VimEngine/ExCommandParser.cs`
- Last change tracking (dot command): `src/VGrid/VimEngine/LastChange.cs`
- Action definitions: `src/VGrid/VimEngine/Actions/`
- Key binding system: `src/VGrid/VimEngine/KeyBinding/`

### Data Model
- Document structure: `src/VGrid/Models/TsvDocument.cs`
- Row/Cell implementations: `src/VGrid/Models/Row.cs`, `src/VGrid/Models/Cell.cs`
- Cursor position: `src/VGrid/Models/GridPosition.cs`
- Diff models: `src/VGrid/Models/DiffCell.cs`, `src/VGrid/Models/DiffRow.cs`

### ViewModel Coordination
- Application entry and file operations: `src/VGrid/ViewModels/MainViewModel.cs`
- Grid data binding and cell editing: `src/VGrid/ViewModels/TsvGridViewModel.cs`
- Status bar (mode, position display): `src/VGrid/ViewModels/StatusBarViewModel.cs`
- Git diff viewer: `src/VGrid/ViewModels/DiffViewerViewModel.cs`

## Implemented Vim Features

### Normal Mode Commands
- **Movement**: `h`, `j`, `k`, `l`, `0`, `H`, `$`, `L`, `gg`, `G`, `w`, `b`, `J` (10 rows down), `K` (10 rows up), `{` (prev empty row), `}` (next empty row)
- **Yank**: `yy`, `yiw`, `yaw`, `Ctrl+C`
- **Paste**: `p` (paste after), `P` (paste before), `Ctrl+V`
- **Delete**: `dd`, `x`, `diw`, `daw`
- **Change**: `cc` (change line), `ciw`, `caw` (change cell)
- **Undo/Redo**: `u` (undo), `Ctrl+R` (redo)
- **Search**: `/`, `n`, `N`
- **Ex-command**: `:`
- **Mode switch**: `i`, `I`, `a`, `A`, `o`, `O`, `v`, `V`, `Ctrl+V`
- **Leader**: `Space w` (save)
- **Tab navigation**: `<` (prev tab), `>` (next tab)
- **Repeat**: `.` (repeat last change)
- **Align**: `=` (align columns)
- **Scroll**: `zz` (scroll to center)
- **Count prefix**: Any number before command (e.g., `3j`, `5dd`)

### Visual Mode Commands
- **Movement**: `h`, `j`, `k`, `l`, `H`, `L`, `w`, `b`, `0`, `gg`, `G`, `J`, `K`, `{`, `}`
- **Yank**: `y`, `Ctrl+C`
- **Delete**: `d`
- **Paste**: `p` (paste over selection)
- **Bulk edit**: `i`, `a`
- **Visual types**: Character-wise (`v`), Line-wise (`V`), Block-wise (`Ctrl+V`)

### Command Mode
- **Ex-commands**: `:w`, `:write`, `:q`, `:quit`, `:q!`, `:quit!`, `:wq`, `:x`
- **Search**: `/pattern` (supports regex with literal fallback)

### Insert Mode
- **Edit**: Type text to edit cell content
- **Navigation**: Arrow keys, `Tab` (next cell), `Shift+Tab` (prev cell)
- **Exit**: `Esc`, `Enter` (commit and exit)

## Important Notes

### Vim State Synchronization
When implementing new Vim commands:
1. Mutate `VimState.CursorPosition` to move cursor
2. Call `VimState.SwitchMode()` to change modes (not direct property assignment)
3. Use `VimState.PendingKeys.Add()` and check `VimState.PendingKeys.Keys` for multi-key commands (e.g., `gg`, `yy`, `dd`)
4. Support count prefixes via `VimState.CountPrefix`
5. Store yanked content in `VimState.LastYank` (type `YankedContent`)
6. For search functionality, use `VimState.SetSearchResults()` and `VimState.NavigateToNextMatch()`
7. Trigger file operations via events: `VimState.OnSaveRequested()`, `VimState.OnQuitRequested()`
8. Track changes for dot command via `VimState.LastChange`

### Data Binding
- `TsvDocument.Rows` is an `ObservableCollection<Row>` - modifications automatically update UI
- `Row.Cells` is also `ObservableCollection<Cell>` - individual cell changes propagate
- After programmatic document changes, raise `PropertyChanged` on relevant ViewModels

### Command History
- Only use `CommandHistory` for **reversible** edit operations
- Navigation and mode switches should NOT be commands
- Each command must store both old and new values for undo

### CJK Character Support
- Full-width characters (Hiragana, Katakana, Hangul, CJK Ideographs) are treated as 2x display width
- Column alignment (`=` command) properly handles mixed-width characters

### Testing
The project uses xUnit for testing. Tests are located in `tests/VGrid.Tests/`.

## File Structure

```
VGrid/
├── src/VGrid/
│   ├── Commands/          # Command pattern (undo/redo) - 17+ command implementations
│   ├── Controls/          # Custom WPF controls
│   ├── Converters/        # WPF value converters
│   ├── Helpers/           # RelayCommand, ViewModelBase
│   ├── KeyHandling/       # Keyboard input handling
│   ├── Models/            # TsvDocument, Row, Cell, GridPosition, DiffCell, DiffRow
│   ├── Services/          # File I/O, Git, Settings, Theme services
│   ├── Themes/            # Theme resources
│   ├── UI/                # UI managers (FolderTree, TemplateTree)
│   ├── ViewModels/        # MVVM ViewModels
│   ├── Views/             # XAML windows and user controls
│   ├── VimEngine/         # Vim state machine and mode handlers
│   │   ├── Actions/       # Action definitions for key binding
│   │   ├── KeyBinding/    # Key binding configuration
│   │   └── Vimrc/         # .vimrc file support
│   ├── App.xaml[.cs]      # Application entry point
│   └── MainWindow.xaml[.cs] # Main UI window
└── tests/VGrid.Tests/     # xUnit tests
```
