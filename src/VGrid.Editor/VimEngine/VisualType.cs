namespace VGrid.VimEngine;

/// <summary>
/// Types of visual selection modes
/// </summary>
public enum VisualType
{
    /// <summary>
    /// Character-wise (rectangular) visual mode - activated with 'v'
    /// </summary>
    Character,

    /// <summary>
    /// Line-wise visual mode - activated with 'V' (Shift+V)
    /// Selects entire rows
    /// </summary>
    Line,

    /// <summary>
    /// Block-wise (column) visual mode - activated with Ctrl+V
    /// Selects entire columns
    /// </summary>
    Block
}
