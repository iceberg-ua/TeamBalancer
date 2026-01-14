namespace TeamBalancer;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();

#if ANDROID
		// Set status bar spacer height to match Android status bar
		Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
		{
			var activity = Platform.CurrentActivity;
			if (activity != null)
			{
				var resourceId = activity.Resources?.GetIdentifier("status_bar_height", "dimen", "android");
				if (resourceId.HasValue && resourceId.Value > 0)
				{
					var statusBarHeight = activity.Resources?.GetDimensionPixelSize(resourceId.Value) ?? 0;
					var density = activity.Resources?.DisplayMetrics?.Density ?? 1;
					StatusBarSpacer.HeightRequest = statusBarHeight / density;
				}
			}
		});
#endif
	}
}
