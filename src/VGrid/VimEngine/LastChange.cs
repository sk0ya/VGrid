namespace VGrid.VimEngine;

/// <summary>
/// Represents the last change operation for dot command replay
/// </summary>
public class LastChange
{
    /// <summary>
    /// The type of change operation
    /// </summary>
    public ChangeType Type { get; set; } = ChangeType.None;

    /// <summary>
    /// The count prefix used when the change was made (e.g., 3 in "3dd")
    /// </summary>
    public int Count { get; set; } = 1;

    /// <summary>
    /// For insert operations: the text that was inserted
    /// </summary>
    public string? InsertedText { get; set; }

    /// <summary>
    /// For insert operations: where to position the caret (Start/End)
    /// </summary>
    public CellEditCaretPosition CaretPosition { get; set; } = CellEditCaretPosition.Start;

    /// <summary>
    /// For paste operations: the content that was pasted (snapshot at time of paste)
    /// </summary>
    public YankedContent? PastedContent { get; set; }

    /// <summary>
    /// For paste operations: whether it was paste before (P) or after (p)
    /// </summary>
    public bool PasteBefore { get; set; }

    /// <summary>
    /// For visual bulk edit: the selection range
    /// </summary>
    public SelectionRange? BulkEditRange { get; set; }
}

/// <summary>
/// Types of changes that can be repeated with dot command
/// </summary>
public enum ChangeType
{
    /// <summary>
    /// No change recorded
    /// </summary>
    None,

    /// <summary>
    /// Delete current cell (x)
    /// </summary>
    DeleteCell,

    /// <summary>
    /// Delete entire row (dd)
    /// </summary>
    DeleteRow,

    /// <summary>
    /// Delete word/cell (diw, daw)
    /// </summary>
    DeleteWord,

    /// <summary>
    /// Insert text at cursor (i<text><Esc>)
    /// </summary>
    Insert,

    /// <summary>
    /// Insert text after cursor (a<text><Esc>)
    /// </summary>
    InsertAfter,

    /// <summary>
    /// Insert new line below (o<text><Esc>)
    /// </summary>
    InsertLineBelow,

    /// <summary>
    /// Insert new line above (O<text><Esc>)
    /// </summary>
    InsertLineAbove,

    /// <summary>
    /// Paste after cursor (p)
    /// </summary>
    PasteAfter,

    /// <summary>
    /// Paste before cursor (P)
    /// </summary>
    PasteBefore,

    /// <summary>
    /// Delete visual selection (visual mode d)
    /// </summary>
    VisualDelete,

    /// <summary>
    /// Bulk edit in visual mode (visual mode i/a)
    /// </summary>
    VisualBulkEdit
}
