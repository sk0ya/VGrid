using System.Collections.Generic;

namespace VGrid.Commands;

/// <summary>
/// Manages command history for undo/redo functionality
/// </summary>
public class CommandHistory
{
    private readonly Stack<ICommand> _undoStack = new();
    private readonly Stack<ICommand> _redoStack = new();
    private readonly int _maxHistorySize;

    public CommandHistory(int maxHistorySize = 100)
    {
        _maxHistorySize = maxHistorySize;
    }

    /// <summary>
    /// Gets whether undo is available
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Gets whether redo is available
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Gets the number of commands in the undo history
    /// </summary>
    public int UndoCount => _undoStack.Count;

    /// <summary>
    /// Gets the number of commands in the redo history
    /// </summary>
    public int RedoCount => _redoStack.Count;

    /// <summary>
    /// Executes a command and adds it to the history
    /// </summary>
    public void Execute(ICommand command)
    {
        command.Execute();
        _undoStack.Push(command);

        // Clear redo stack when a new command is executed
        _redoStack.Clear();

        // Limit history size
        if (_undoStack.Count > _maxHistorySize)
        {
            // Remove oldest command (bottom of stack)
            var temp = new Stack<ICommand>();
            while (_undoStack.Count > 1)
            {
                temp.Push(_undoStack.Pop());
            }
            _undoStack.Pop(); // Remove oldest
            while (temp.Count > 0)
            {
                _undoStack.Push(temp.Pop());
            }
        }
    }

    /// <summary>
    /// Undoes the last command
    /// </summary>
    public void Undo()
    {
        if (!CanUndo)
        {
            throw new InvalidOperationException("Nothing to undo");
        }

        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
    }

    /// <summary>
    /// Redoes the last undone command
    /// </summary>
    public void Redo()
    {
        if (!CanRedo)
        {
            throw new InvalidOperationException("Nothing to redo");
        }

        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
    }

    /// <summary>
    /// Clears all command history
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}
