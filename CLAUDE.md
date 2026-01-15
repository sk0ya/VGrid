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
- **Models** (`Models/`): Data structures (`TsvDocument`, `Row`, `Cell`, `GridPosition`)
- **ViewModels** (`ViewModels/`): Application logic and state management (`MainViewModel`, `TsvGridViewModel`, `TabItemViewModel`, `StatusBarViewModel`)
- **Views**: XAML files (`MainWindow.xaml`)

### Vim Engine (State Pattern)
The Vim modal editing system is implemented using the State Pattern in `VimEngine/`:
- **VimState**: Central state manager that coordinates mode switching and key handling
- **IVimMode**: Interface defining mode behavior (`HandleKey`, `OnEnter`, `OnExit`)
- **Mode Implementations**: `NormalMode`, `InsertMode`, `VisualMode`, `CommandMode` each implement `IVimMode`
- **KeySequence**: Handles multi-key commands like `gg`, `yy`, `dd` with timeout-based expiration
- **YankedContent**: Stores yanked (copied) content with type information (Character/Line/Block)
- **SelectionRange**: Represents visual mode selection range
- **ExCommandParser**: Parses ex-commands (`:w`, `:q`, `:wq`, etc.)

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
- **EditCellCommand**: Concrete command for cell editing
- **DeleteRowCommand**: Command for deleting entire rows
- **DeleteColumnCommand**: Command for deleting entire columns
- **DeleteSelectionCommand**: Command for deleting selected cell ranges

When editing cells, wrap operations in commands and execute via `CommandHistory.Execute()`. The `u` key in Normal mode triggers undo.

### Tab Management
The application supports multiple open TSV files via tabs:
- Each tab has its own `TsvDocument`, `VimState`, and `TsvGridViewModel`
- `MainViewModel` manages the tab collection and coordinates file operations
- VimState changes in the active tab update the global `StatusBarViewModel`

### File I/O
- **ITsvFileService**: Interface for file operations
- **TsvFileService**: Implementation using async file I/O
- Supports `.tsv`, `.txt`, `.tab` extensions
- Tab-separated format (splits on `\t`, joins with `\t`)

## Key Code Locations

### Vim Mode Handling
- Normal mode navigation: `src/VGrid/VimEngine/NormalMode.cs`
- Insert mode editing: `src/VGrid/VimEngine/InsertMode.cs`
- Visual mode selection: `src/VGrid/VimEngine/VisualMode.cs`
- Command mode (search and ex-commands): `src/VGrid/VimEngine/CommandMode.cs`
- Multi-key sequences (e.g., `gg`, `yy`, `dd`): `src/VGrid/VimEngine/KeySequence.cs`
- Yanked content storage: `src/VGrid/VimEngine/YankedContent.cs`
- Ex-command parsing: `src/VGrid/VimEngine/ExCommandParser.cs`

### Data Model
- Document structure: `src/VGrid/Models/TsvDocument.cs`
- Row/Cell implementations: `src/VGrid/Models/Row.cs`, `src/VGrid/Models/Cell.cs`
- Cursor position: `src/VGrid/Models/GridPosition.cs`

### ViewModel Coordination
- Application entry and file operations: `src/VGrid/ViewModels/MainViewModel.cs`
- Grid data binding and cell editing: `src/VGrid/ViewModels/TsvGridViewModel.cs`
- Status bar (mode, position display): `src/VGrid/ViewModels/StatusBarViewModel.cs`

## Development Phases

The project follows a phased development plan:
- **Phase 1-9** (✅ Complete): Basic MVVM structure, Vim modes (Normal/Insert/Visual), basic navigation (`hjkl`, `0`, `$`, `gg`), mode switching (`i`, `a`, `o`, `v`, `Esc`), status bar UI
- **Phase 10-14** (✅ Complete):
  - Delete operations: `dd` (delete line), `x` (delete cell), `diw`/`daw` (delete word/cell)
  - Yank/paste: `yy` (yank line), `yiw`/`yaw` (yank word/cell), `p` (paste after), `P` (paste before), `Ctrl+C`/`Ctrl+V` (copy/paste)
  - Word movement: `w` (next non-empty cell), `b` (previous non-empty cell)
  - Search: `/pattern` (regex search), `n` (next match), `N` (previous match)
  - Command mode: `:w` (save), `:q` (quit), `:wq`/`:x` (save and quit), `:q!` (force quit)
  - Visual mode enhancements: Line-wise (`V`), Block-wise (`Ctrl+V`), yank/delete selections
  - Additional navigation: `H` (line start), `L` (last non-empty column)
  - Undo support: `u` (undo last change)
  - Leader key: `Space w` (save file)
- **Phase 15-18** (Planned): Sort/filter operations, macro recording/playback, configuration customization, advanced ex-commands
- **Phase 19-20** (Planned): Polish and comprehensive testing

## Implemented Vim Features

### Normal Mode Commands
- **Movement**: `h`, `j`, `k`, `l`, `0`, `H`, `$`, `L`, `gg`, `w`, `b`
- **Yank**: `yy`, `yiw`, `yaw`, `Ctrl+C`
- **Paste**: `p` (paste after), `P` (paste before), `Ctrl+V`
- **Delete**: `dd`, `x`, `diw`, `daw`
- **Undo**: `u`
- **Search**: `/`, `n`, `N`
- **Ex-command**: `:`
- **Mode switch**: `i`, `I`, `a`, `A`, `o`, `O`, `v`, `V`, `Ctrl+V`
- **Leader**: `Space w` (save)
- **Count prefix**: Any number before command (e.g., `3j`, `5dd`)

### Visual Mode Commands
- **Movement**: `h`, `j`, `k`, `l`, `H`, `L`, `w`, `b`, `0`
- **Yank**: `y`, `Ctrl+C`
- **Delete**: `d`
- **Bulk edit**: `i`, `a`
- **Visual types**: Character-wise (`v`), Line-wise (`V`), Block-wise (`Ctrl+V`)

### Command Mode
- **Ex-commands**: `:w`, `:q`, `:q!`, `:wq`, `:x`
- **Search**: `/pattern` (supports regex)

### Insert Mode
- **Edit**: Type text to edit cell content
- **Navigation**: Arrow keys
- **Exit**: `Esc`

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

### Data Binding
- `TsvDocument.Rows` is an `ObservableCollection<Row>` - modifications automatically update UI
- `Row.Cells` is also `ObservableCollection<Cell>` - individual cell changes propagate
- After programmatic document changes, raise `PropertyChanged` on relevant ViewModels

### Command History
- Only use `CommandHistory` for **reversible** edit operations
- Navigation and mode switches should NOT be commands
- Each command must store both old and new values for undo

### Testing
The project uses xUnit for testing. Tests are located in `tests/VGrid.Tests/`.

## File Structure

```
VGrid/
├── src/VGrid/
│   ├── Commands/          # Command pattern (undo/redo)
│   ├── Converters/        # WPF value converters
│   ├── Helpers/           # RelayCommand, ViewModelBase
│   ├── Models/            # TsvDocument, Row, Cell, GridPosition
│   ├── Services/          # File I/O (ITsvFileService)
│   ├── ViewModels/        # MVVM ViewModels
│   ├── VimEngine/         # Vim state machine and mode handlers
│   ├── App.xaml[.cs]      # Application entry point
│   └── MainWindow.xaml[.cs] # Main UI window
└── tests/VGrid.Tests/     # xUnit tests
```
