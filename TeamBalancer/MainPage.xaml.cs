namespace TeamBalancer;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();
		SetStatusBarSpacerHeight();
	}

	private void SetStatusBarSpacerHeight()
	{
#if ANDROID
		// Minimal spacer - just enough to clear the status bar
		StatusBarSpacer.HeightRequest = 4;
#endif
	}
}
