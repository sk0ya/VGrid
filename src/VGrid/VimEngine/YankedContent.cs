namespace VGrid.VimEngine;

/// <summary>
/// Represents yanked (copied) content from a visual selection
/// </summary>
public class YankedContent
{
    /// <summary>
    /// The yanked cell values as a 2D array
    /// </summary>
    public string[,] Values { get; set; } = new string[0, 0];

    /// <summary>
    /// The type of visual mode that created this yank
    /// </summary>
    public VisualType SourceType { get; set; }

    /// <summary>
    /// The number of rows in the yanked content
    /// </summary>
    public int Rows { get; set; }

    /// <summary>
    /// The number of columns in the yanked content
    /// </summary>
    public int Columns { get; set; }
}
