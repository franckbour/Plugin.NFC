namespace Plugin.NFC.Maui.Sample;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Force light theme
        UserAppTheme = AppTheme.Light;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }
}