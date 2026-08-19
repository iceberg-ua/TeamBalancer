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
    /// <summary>
    /// The longest edge, in pixels, an image is shrunk to before it is decoded. See
    /// <see cref="FitForDecoding"/> for why it sits where it does.
    /// </summary>
    private const int MaxDecodeEdge = 2000;

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
        byte[] image;
        using (var source = await photo.OpenReadAsync())
        using (var buffer = new MemoryStream())
        {
            await source.CopyToAsync(buffer);
            image = buffer.ToArray();
        }

        return await ReadCodeAsync(image);
    }

    /// <summary>
    /// Finds a code in an image, at a size the decoder can survive.
    /// </summary>
    /// <param name="image">The image file's bytes.</param>
    /// <returns>The code's text, or an empty string if the image holds no readable code.</returns>
    private static async Task<string> ReadCodeAsync(byte[] image)
    {
        try
        {
            using var stream = new MemoryStream(FitForDecoding(image, MaxDecodeEdge));
            var results = await BarcodeReader.DecodeAsync(stream, ReaderOptions);

            // An empty string rather than null: the user did choose a picture, it simply had no
            // code in it, and the screen says something different about each of those.
            return results?.FirstOrDefault()?.Value ?? string.Empty;
        }
        catch (Exception)
        {
            // A picture the decoder cannot make sense of is the same outcome to the user as one
            // with no code in it, and far better than the app disappearing.
            return string.Empty;
        }
    }

    /// <summary>
    /// Shrinks an oversized image to something the decoder can hold.
    /// </summary>
    /// <remarks>
    /// A photograph from a current phone is around fifty megapixels, and turning one into a
    /// bitmap costs four bytes a pixel before the decoder has looked at anything - repeatedly,
    /// because AutoRotate and TryHarder both work on copies. That is what was killing the app
    /// outright rather than reporting a failure.
    ///
    /// The ceiling is a compromise, not a maximum: shrinking further would start to cost real
    /// codes. A full squad makes a symbol around 129 modules across, and a decoder needs three
    /// or four pixels of each, so a code filling a third of a 2,000 pixel frame still has five
    /// pixels per module to work with.
    /// </remarks>
    /// <param name="image">The image file's bytes.</param>
    /// <param name="maxEdge">The longest edge to allow, in pixels.</param>
    /// <returns>The image, shrunk if it needed it, or unchanged if it did not.</returns>
    private static byte[] FitForDecoding(byte[] image, int maxEdge)
    {
#if ANDROID
        var bounds = new Android.Graphics.BitmapFactory.Options { InJustDecodeBounds = true };
        Android.Graphics.BitmapFactory.DecodeByteArray(image, 0, image.Length, bounds);

        var longest = Math.Max(bounds.OutWidth, bounds.OutHeight);
        if (longest <= 0 || longest <= maxEdge)
        {
            return image;
        }

        // The decoder only honours powers of two, so this walks up to the largest one that
        // still leaves the image above the ceiling rather than dropping below it.
        var sample = 1;
        while (longest / (sample * 2) >= maxEdge)
        {
            sample *= 2;
        }

        using var options = new Android.Graphics.BitmapFactory.Options { InSampleSize = sample };
        using var bitmap = Android.Graphics.BitmapFactory.DecodeByteArray(image, 0, image.Length, options);
        if (bitmap is null)
        {
            return image;
        }

        using var target = new MemoryStream();

        // PNG rather than JPEG: JPEG's artefacts land on exactly the hard black-and-white edges
        // the decoder is looking for.
        bitmap.Compress(Android.Graphics.Bitmap.CompressFormat.Png!, 100, target);

        return target.ToArray();
#else
        return image;
#endif
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
