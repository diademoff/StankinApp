using StankinApp.ViewModels;

namespace StankinApp
{
    public partial class MainPage : ContentPage
    {
        MainViewModel _context;
        public MainPage(MainViewModel context)
        {
            InitializeComponent();
            _context = context;
            this.BindingContext = context;
        }
    }
}
