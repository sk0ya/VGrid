using System.Windows;
using VGrid.ViewModels;

namespace VGrid.Views;

/// <summary>
/// Interaction logic for GitBranchWindow.xaml
/// </summary>
public partial class GitBranchWindow : Window
{
    public GitBranchWindow(GitBranchViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
