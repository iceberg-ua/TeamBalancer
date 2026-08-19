namespace TeamBalancer.Services;

using TeamBalancer.Core.Localization;
using TeamBalancer.Views;
using ZXing.Net.Maui;

/// <summary>
/// Reads QR codes with ZXing, either live from the camera or out of an image on the phone.
/// </summary>
public sealed class QrScannerService : IQrScannerService
{
    private readonly ILocalizationService _localization;

    /// <summary>
    /// Initializes a new instance of the QrScannerService class.
    /// </summary>
    /// <param name="localization">The translations the scanner page shows.</param>
    public QrScannerService(ILocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    /// <inheritdoc />
    public bool IsCameraScanningSupported
    {
        get
        {
#if WINDOWS
            // ZXing.Net.Maui has no Windows camera implementation. The desktop build still
            // imports from an image, which is the route that matters there anyway.
            return false;
#else
            return true;
#endif
        }
    }

    /// <inheritdoc />
    public async Task<string?> ScanWithCameraAsync()
    {
        if (!IsCameraScanningSupported)
        {
            return null;
        }

        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            status = await Permissions.RequestAsync<Permissions.Camera>();
        }

        if (status != PermissionStatus.Granted)
        {
            return null;
        }

        var navigation = CurrentNavigation();
        if (navigation is null)
        {
            return null;
        }

        // Built and pushed on the UI thread: this is called from a Blazor event handler, which
        // does not run there, and creating a native camera view off it throws on both platforms.
        var page = await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var scanPage = new QrScanPage(_localization);
            await navigation.PushModalAsync(scanPage);
            return scanPage;
        });

        return await page.Result;
    }

    /// <inheritdoc />
    public async Task<string?> ScanFromImageAsync()
    {
        var photo = await MediaPicker.Default.PickPhotoAsync();
        if (photo is null)
        {
            return null;
        }

        using var stream = await photo.OpenReadAsync();

        var results = await BarcodeReader.DecodeAsync(stream, new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.TwoDimensional,
            AutoRotate = true,
            TryHarder = true,
            Multiple = false
        });

        // An empty string rather than null: the user did choose a picture, it simply had no
        // code in it, and the screen says something different about each of those.
        return results?.FirstOrDefault()?.Value ?? string.Empty;
    }

    /// <summary>
    /// Finds the navigation stack to push the scanner onto. The app has a single window whose
    /// page hosts the Blazor view, and the scanner goes over the top of it.
    /// </summary>
    /// <returns>The navigation stack, or null if no window is up yet.</returns>
    private static INavigation? CurrentNavigation() =>
        Application.Current?.Windows.Count > 0
            ? Application.Current.Windows[0].Page?.Navigation
            : null;
}
