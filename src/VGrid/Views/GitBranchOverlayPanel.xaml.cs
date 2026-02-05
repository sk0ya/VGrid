using System.Windows;
using System.Windows.Input;
using VGrid.ViewModels;

namespace VGrid.Views;

public partial class GitBranchOverlayPanel : System.Windows.Controls.UserControl
{
    public GitBranchOverlayPanel()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        CloseOverlay();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseOverlay();
            e.Handled = true;
            return;
        }
        base.OnPreviewKeyDown(e);
    }

    private void CloseOverlay()
    {
        // Navigate up to find MainViewModel and close the overlay
        var window = Window.GetWindow(this);
        if (window?.DataContext is MainViewModel mainViewModel)
        {
            mainViewModel.CloseGitBranchOverlay();
        }
    }
}
