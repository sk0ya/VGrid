# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

VGrid is a WPF-based TSV (Tab-Separated Values) editor with Vim keybindings. The application uses .NET 8 and implements a modal editing system (Normal, Insert, Visual modes) similar to Vim, specifically designed for editing tabular data.

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
- **Mode Implementations**: `NormalMode`, `InsertMode`, `VisualMode` each implement `IVimMode`
- **KeySequence**: Handles multi-key commands like `gg` with timeout-based expiration

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

When editing cells, wrap operations in commands and execute via `CommandHistory.Execute()`.

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
- Multi-key sequences (e.g., `gg`): `src/VGrid/VimEngine/KeySequence.cs`

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
- **Phase 10-14** (Planned): Delete operations (`dd`, `x`), yank/paste (`yy`, `p`), word movement (`w`, `b`, `e`), search (`/`, `?`)
- **Phase 15-18** (Planned): Command mode (`:w`, `:q`, `:wq`), substitution (`:s///`), sort/filter operations
- **Phase 19-20** (Planned): Polish and comprehensive testing

## Important Notes

### Vim State Synchronization
When implementing new Vim commands:
1. Mutate `VimState.CursorPosition` to move cursor
2. Call `VimState.SwitchMode()` to change modes (not direct property assignment)
3. Use `KeySequence.Add()` and check `KeySequence.Keys` for multi-key commands
4. Support count prefixes via `VimState.CountPrefix`

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
