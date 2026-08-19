namespace TeamBalancer.Services;

using TeamBalancer.Core.Localization;
using TeamBalancer.Views;
using ZXing.Net.Maui;

/// <summary>
/// Reads QR codes with ZXing, either live from the camera or out of an image on the phone.
/// </summary>
/// <remarks>
/// Every call into MAUI's device APIs here is marshalled to the UI thread. This screen is
/// Blazor, so its event handlers run on the renderer's dispatcher rather than on the platform's
/// main thread, and both the media picker and the permission prompts fail outright when they are
/// started from anywhere else.
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

    /// <summary>
    /// The decode settings both routes use. TryHarder earns its cost here: a full squad makes a
    /// dense symbol, and it is being read off another phone's screen or out of a photograph of
    /// one, where a single quick pass often fails.
    /// </summary>
    private static BarcodeReaderOptions ReaderOptions => new()
    {
        Formats = BarcodeFormats.TwoDimensional,
        AutoRotate = true,
        TryHarder = true,
        Multiple = false
    };

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

    /// <inheritdoc />
    public async Task<string?> ScanFromImageAsync()
    {
        var photo = await MainThread.InvokeOnMainThreadAsync(PickImageAsync);
        if (photo is null)
        {
            return null;
        }

        // Buffered into memory rather than decoded from the picker's stream directly. On Android
        // the stream can come back from a content provider without support for seeking, and
        // decoding an image needs to read the header and then go back over the pixels.
        using var buffer = new MemoryStream();
        using (var source = await photo.OpenReadAsync())
        {
            await source.CopyToAsync(buffer);
        }

        buffer.Position = 0;

        var results = await BarcodeReader.DecodeAsync(buffer, ReaderOptions);

        // An empty string rather than null: the user did choose a picture, it simply had no
        // code in it, and the screen says something different about each of those.
        return results?.FirstOrDefault()?.Value ?? string.Empty;
    }

    /// <summary>
    /// Asks the user for the image holding the code, using whichever picker on this platform
    /// can actually reach where a received picture is kept.
    /// </summary>
    /// <remarks>
    /// The two platforms genuinely differ, so this is a split rather than a preference. On iOS
    /// an image someone sent lands in the photo library, which the document picker cannot see at
    /// all, so the media picker is the only one that can reach it. On Android the reverse holds
    /// in practice: the system photo picker depends on the media provider resolving the item,
    /// which fails on emulators and for pictures held only in the cloud - it reports "Can't load
    /// some photos" and hands back nothing. The document picker asks the storage layer directly,
    /// browses Downloads and the messengers' own folders where a received code actually sits,
    /// and grants access to the single file the user chose without any permission at all.
    /// </remarks>
    /// <returns>The chosen file, or null if the user picked nothing.</returns>
    private Task<FileResult?> PickImageAsync()
    {
#if IOS || MACCATALYST
        return MediaPicker.Default.PickPhotoAsync();
#else
        return FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = _localization["share.pickImageTitle"],
            FileTypes = FilePickerFileType.Images
        });
#endif
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
