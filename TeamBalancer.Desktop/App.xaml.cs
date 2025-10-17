namespace TeamBalancer.Desktop;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage())
        {
            Title = "Team Balancer",
            Width = 600,
            Height = 800,
            MinimumWidth = 600,
            MinimumHeight = 800,
            MaximumWidth = 600,
            MaximumHeight = 800
        };

        return window;
    }
}
