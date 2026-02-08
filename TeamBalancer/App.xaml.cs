using TeamBalancer.Core.Services.Interfaces;

namespace TeamBalancer;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new MainPage()) { Title = "Team Balancer" };

		// Save player selection state when the app goes to background or closes
		window.Stopped += async (s, e) =>
		{
			var repo = IPlatformApplication.Current?.Services.GetService<IPlayerRepository>();
			if (repo != null)
			{
				await repo.SaveChangesAsync();
			}
		};

		return window;
	}
}
