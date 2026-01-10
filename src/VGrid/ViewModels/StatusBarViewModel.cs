using System.Windows.Media;
using VGrid.Helpers;
using VGrid.VimEngine;

namespace VGrid.ViewModels;

/// <summary>
/// ViewModel for the status bar
/// </summary>
public class StatusBarViewModel : ViewModelBase
{
    private string _modeText = "NORMAL";
    private string _positionText = "0:0";
    private string _messageText = string.Empty;
    private Brush _modeBrush = new SolidColorBrush(Colors.CornflowerBlue);

    public string ModeText
    {
        get => _modeText;
        set => SetProperty(ref _modeText, value);
    }

    public string PositionText
    {
        get => _positionText;
        set => SetProperty(ref _positionText, value);
    }

    public string MessageText
    {
        get => _messageText;
        set => SetProperty(ref _messageText, value);
    }

    public Brush ModeBrush
    {
        get => _modeBrush;
        set => SetProperty(ref _modeBrush, value);
    }

    public void UpdateMode(VimMode mode)
    {
        ModeText = mode switch
        {
            VimMode.Normal => "NORMAL",
            VimMode.Insert => "INSERT",
            VimMode.Visual => "VISUAL",
            VimMode.Command => "COMMAND",
            _ => "UNKNOWN"
        };

        ModeBrush = mode switch
        {
            VimMode.Normal => new SolidColorBrush(Colors.CornflowerBlue),
            VimMode.Insert => new SolidColorBrush(Colors.LimeGreen),
            VimMode.Visual => new SolidColorBrush(Colors.Orange),
            VimMode.Command => new SolidColorBrush(Colors.MediumPurple),
            _ => new SolidColorBrush(Colors.Gray)
        };
    }

    public void UpdatePosition(int row, int column)
    {
        PositionText = $"{row}:{column}";
    }

    public void ShowMessage(string message)
    {
        MessageText = message;
    }

    public void ClearMessage()
    {
        MessageText = string.Empty;
    }
}
