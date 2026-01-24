using Android.App;
using Android.Content.PM;
using Android.OS;

namespace TeamBalancer;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, ScreenOrientation = ScreenOrientation.Portrait, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

#pragma warning disable CA1422
        Window?.SetStatusBarColor(Android.Graphics.Color.ParseColor("#1b6ec2"));
#pragma warning restore CA1422
    }
}
