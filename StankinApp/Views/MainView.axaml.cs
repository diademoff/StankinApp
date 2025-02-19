using Avalonia.Controls;
using StankinApp.ViewModels;

namespace StankinApp.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        this.DataContext = new MainViewModel();
    }
}
