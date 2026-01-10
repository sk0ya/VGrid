namespace VGrid.Commands;

/// <summary>
/// Interface for undoable commands
/// Note: This is different from System.Windows.Input.ICommand
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Executes the command
    /// </summary>
    void Execute();

    /// <summary>
    /// Undoes the command
    /// </summary>
    void Undo();

    /// <summary>
    /// Description of what this command does
    /// </summary>
    string Description { get; }
}
