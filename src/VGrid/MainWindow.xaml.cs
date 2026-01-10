using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using VGrid.ViewModels;

namespace VGrid;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // Subscribe to document changes to regenerate columns
        _viewModel.GridViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.GridViewModel.Document))
            {
                GenerateColumns();
            }
        };

        // Generate initial columns
        GenerateColumns();

        // Set focus to the grid
        Loaded += (s, e) => TsvGrid.Focus();
    }

    private void GenerateColumns()
    {
        TsvGrid.Columns.Clear();

        var columnCount = _viewModel?.GridViewModel.ColumnCount ?? 0;
        for (int i = 0; i < columnCount; i++)
        {
            var columnIndex = i; // Capture for closure
            var column = new DataGridTextColumn
            {
                Header = $"Column {i + 1}",
                Binding = new Binding($"Cells[{i}].Value")
                {
                    Mode = BindingMode.TwoWay,
                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                },
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            };

            TsvGrid.Columns.Add(column);
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel == null)
            return;

        // Handle key through Vim state
        var handled = _viewModel.VimState.HandleKey(e.Key, Keyboard.Modifiers, _viewModel.GridViewModel.Document);

        if (handled)
        {
            e.Handled = true;
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        // Prevent default handling of some keys
        if (e.Key == Key.Escape || e.Key == Key.I || e.Key == Key.V ||
            e.Key == Key.H || e.Key == Key.J || e.Key == Key.K || e.Key == Key.L)
        {
            e.Handled = true;
        }
    }
}
