using Avalonia.Styling;
using SukiUI;
using SukiUI.Controls;

namespace StankinApp.Views;

public partial class MainWindow : SukiWindow
{
    public MainWindow()
    {
        SukiTheme.GetInstance().ChangeBaseTheme(ThemeVariant.Light);
        InitializeComponent();
    }
}
