using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using StankinApp.ViewModels;

namespace StankinApp.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        this.DataContext = new MainViewModel();
        this.scrollviewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;

    }
}
