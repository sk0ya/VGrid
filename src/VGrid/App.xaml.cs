using System.IO;
using System.Windows;
using VGrid.Services;

namespace VGrid;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    /// <summary>
    /// Gets the folder path specified via command line arguments.
    /// </summary>
    public string? StartupFolderPath { get; private set; }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Parse command line arguments
        ParseCommandLineArgs(e.Args);

        // Initialize Jump List with recent folders
        InitializeJumpList();

        // Create and show the main window
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }

    private void ParseCommandLineArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--folder" && i + 1 < args.Length)
            {
                var folderPath = args[i + 1];
                if (Directory.Exists(folderPath))
                {
                    StartupFolderPath = folderPath;
                }
                break;
            }
        }
    }

    private void InitializeJumpList()
    {
        try
        {
            var settingsService = new SettingsService();
            var recentFolders = settingsService.GetRecentFolders();
            settingsService.UpdateJumpList(recentFolders);
        }
        catch
        {
            // Silently fail if Jump List initialization fails
        }
    }
}

