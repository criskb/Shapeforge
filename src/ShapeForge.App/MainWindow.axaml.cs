using Avalonia.Controls;
using ShapeForge.App.ViewModels;

namespace ShapeForge.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }
}
