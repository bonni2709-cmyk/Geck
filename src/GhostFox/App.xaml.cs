namespace GhostFox;

public partial class App : Microsoft.Maui.Controls.Application
{
    public App(MainPage mainPage)
    {
        InitializeComponent();
        MainPage = new NavigationPage(mainPage);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState);
        window.Title = "GhostFox — private Gecko browser";
        window.Width = 1400;
        window.Height = 900;
        return window;
    }
}
