namespace Plugin.NFC.Maui.Sample
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            // Force light theme
            UserAppTheme = AppTheme.Light;

            //MainPage = new AppShell();
            MainPage = new MainPage();
        }
    }
}