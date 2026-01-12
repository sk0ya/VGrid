using VGrid.Helpers;
using VGrid.Models;
using VGrid.VimEngine;

namespace VGrid.ViewModels;

/// <summary>
/// ViewModel for a single tab (open TSV file)
/// </summary>
public class TabItemViewModel : ViewModelBase
{
    private string _header;
    private bool _isDirty;

    public TabItemViewModel(string filePath, TsvDocument document, VimState vimState, TsvGridViewModel gridViewModel)
    {
        FilePath = filePath;
        Document = document;
        VimState = vimState;
        GridViewModel = gridViewModel;
        _header = System.IO.Path.GetFileName(filePath) ?? "Untitled";

        // Initialize FindReplaceViewModel
        if (vimState.CommandHistory != null)
        {
            FindReplaceViewModel = new FindReplaceViewModel(document, vimState, vimState.CommandHistory);

            // Subscribe to VimSearchActivated event to close FindReplace panel
            vimState.VimSearchActivated += (s, e) => FindReplaceViewModel.Close();
        }
        else
        {
            throw new ArgumentException("VimState must have CommandHistory initialized", nameof(vimState));
        }

        // Subscribe to document changes
        document.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TsvDocument.IsDirty))
            {
                IsDirty = document.IsDirty;
            }
        };
    }

    public string FilePath { get; set; }
    public TsvDocument Document { get; }
    public VimState VimState { get; }
    public TsvGridViewModel GridViewModel { get; }
    public FindReplaceViewModel FindReplaceViewModel { get; }

    public string Header
    {
        get => _header;
        set => SetProperty(ref _header, value);
    }

    public bool IsDirty
    {
        get => _isDirty;
        set
        {
            if (SetProperty(ref _isDirty, value))
            {
                // Update header to show dirty state
                var fileName = System.IO.Path.GetFileName(FilePath) ?? "Untitled";
                Header = value ? $"{fileName}*" : fileName;
            }
        }
    }
}
