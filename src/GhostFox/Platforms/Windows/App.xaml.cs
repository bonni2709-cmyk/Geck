namespace GhostFox.WinUI;

/// <summary>Точка входа WinUI (Windows App SDK). Дальше — обычный MAUI MauiProgram.</summary>
public partial class App : MauiWinUIApplication
{
    public App() => InitializeComponent();

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
