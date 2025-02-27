using StankinApp.ViewModels;

namespace StankinApp
{
    public partial class MainPage : ContentPage
    {
        MainViewModel context = new MainViewModel();

        public MainPage()
        {
            InitializeComponent();
            this.BindingContext = context;
            context.OnPropertyChanged();
        }
    }
}
