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
    private System.Windows.Media.Brush _modeBrush = new SolidColorBrush(Colors.CornflowerBlue);
    private string _currentBranch = string.Empty;
    private int _aheadCount;
    private int _behindCount;
    private bool _isInGitRepo;

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

    public System.Windows.Media.Brush ModeBrush
    {
        get => _modeBrush;
        set => SetProperty(ref _modeBrush, value);
    }

    public void UpdateMode(VimMode mode, string? customModeText = null)
    {
        // Use custom mode text if provided (for VISUAL LINE, VISUAL BLOCK)
        if (!string.IsNullOrEmpty(customModeText))
        {
            ModeText = customModeText;
        }
        else
        {
            ModeText = mode switch
            {
                VimMode.Normal => "NORMAL",
                VimMode.Insert => "INSERT",
                VimMode.Visual => "VISUAL",
                VimMode.Command => "COMMAND",
                _ => "UNKNOWN"
            };
        }

        ModeBrush = mode switch
        {
            VimMode.Normal => new SolidColorBrush(Colors.CornflowerBlue),
            VimMode.Insert => new SolidColorBrush(Colors.LimeGreen),
            VimMode.Visual => new SolidColorBrush(Colors.DodgerBlue),
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

    public string CurrentBranch
    {
        get => _currentBranch;
        set
        {
            if (SetProperty(ref _currentBranch, value))
            {
                OnPropertyChanged(nameof(BranchDisplay));
            }
        }
    }

    public int AheadCount
    {
        get => _aheadCount;
        set
        {
            if (SetProperty(ref _aheadCount, value))
            {
                OnPropertyChanged(nameof(TrackingStatus));
            }
        }
    }

    public int BehindCount
    {
        get => _behindCount;
        set
        {
            if (SetProperty(ref _behindCount, value))
            {
                OnPropertyChanged(nameof(TrackingStatus));
            }
        }
    }

    public bool IsInGitRepo
    {
        get => _isInGitRepo;
        set => SetProperty(ref _isInGitRepo, value);
    }

    public string BranchDisplay => string.IsNullOrEmpty(CurrentBranch) ? string.Empty : $"\ue0a0 {CurrentBranch}";

    public string TrackingStatus
    {
        get
        {
            if (AheadCount == 0 && BehindCount == 0)
                return string.Empty;
            return $" \u2191{AheadCount} \u2193{BehindCount}";
        }
    }

    public void UpdateGitInfo(string? branch, int ahead, int behind)
    {
        IsInGitRepo = !string.IsNullOrEmpty(branch);
        CurrentBranch = branch ?? string.Empty;
        AheadCount = ahead;
        BehindCount = behind;
    }

    public void ClearGitInfo()
    {
        IsInGitRepo = false;
        CurrentBranch = string.Empty;
        AheadCount = 0;
        BehindCount = 0;
    }
}
