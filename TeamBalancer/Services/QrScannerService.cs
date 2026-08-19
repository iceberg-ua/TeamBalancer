namespace TeamBalancer.Services;

using TeamBalancer.Core.Localization;
using TeamBalancer.Views;

/// <summary>
/// Reads QR codes with the device camera, using ZXing.
/// </summary>
/// <remarks>
/// Every call into MAUI's device APIs here is marshalled to the UI thread. This screen is
/// Blazor, so its event handlers run on the renderer's dispatcher rather than on the platform's
/// main thread, and both native view creation and the permission prompts fail outright when they
/// are started from anywhere else.
/// </remarks>
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
            // ZXing.Net.Maui has no Windows camera implementation, and the camera is the only
            // way in, so the desktop build offers no scanning at all. It is not a platform
            // anyone shares a squad from.
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

        var navigation = CurrentNavigation();
        if (navigation is null)
        {
            return null;
        }

        // Permission prompt and native view creation both belong on the UI thread, and they are
        // done in one hop so the prompt cannot be answered between them.
        var page = await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Camera>();
            }

            if (status != PermissionStatus.Granted)
            {
                return null;
            }

            var scanPage = new QrScanPage(_localization);
            await navigation.PushModalAsync(scanPage);
            return scanPage;
        });

        return page is null ? null : await page.Result;
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
