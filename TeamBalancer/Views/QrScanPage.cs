namespace TeamBalancer.Views;

using TeamBalancer.Core.Localization;
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;

/// <summary>
/// The camera viewfinder, shown over the app while the user points the phone at a QR code.
/// </summary>
/// <remarks>
/// This is a MAUI page rather than another Blazor screen, which is the one place the app steps
/// outside its web view. A live camera preview has to be a native view, and a still photo would
/// be the wrong trade here: a full squad produces a dense code, and a live preview gets to retry
/// every frame until one decodes, where a photograph gets a single attempt at whatever the user
/// happened to capture.
/// </remarks>
public sealed class QrScanPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _result = new();
    private readonly CameraBarcodeReaderView _reader;
    private int _handled;

    /// <summary>
    /// Initializes a new instance of the QrScanPage class.
    /// </summary>
    /// <param name="loc">The translations for the page's own text.</param>
    public QrScanPage(ILocalizationService loc)
    {
        ArgumentNullException.ThrowIfNull(loc);

        Title = loc["share.scanTitle"];
        BackgroundColor = Color.FromArgb("#161826");

        _reader = new CameraBarcodeReaderView
        {
            Options = new BarcodeReaderOptions
            {
                Formats = BarcodeFormats.TwoDimensional,
                AutoRotate = true,

                // A dense code photographed off another phone's screen is exactly the case
                // that needs the slower, more thorough decode. There is nothing else competing
                // for the frame, so the cost does not show.
                TryHarder = true,
                Multiple = false
            }
        };

        _reader.BarcodesDetected += OnBarcodesDetected;

        var hint = new Label
        {
            Text = loc["share.scanHint"],
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(24, 0, 24, 12)
        };

        var cancel = new Button
        {
            Text = loc["common.cancel"],
            Margin = new Thickness(24, 0, 24, 32)
        };
        cancel.Clicked += async (_, _) => await CloseAsync(null);

        var layout = new Grid
        {
            RowDefinitions =
            [
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            ]
        };

        layout.Add(_reader);
        layout.Add(hint, 0, 1);
        layout.Add(cancel, 0, 2);

        Content = layout;
    }

    /// <summary>
    /// Gets the text of the code that was read, once the page closes. Null means the user left
    /// without scanning anything.
    /// </summary>
    public Task<string?> Result => _result.Task;

    /// <summary>
    /// Answers the hardware back button the same way the cancel button does, so leaving the
    /// page never strands the caller waiting on a scan that will not arrive.
    /// </summary>
    /// <returns>False, letting the navigation proceed.</returns>
    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    /// <summary>
    /// Takes the first code the camera reads and closes.
    /// </summary>
    /// <param name="sender">The reader view.</param>
    /// <param name="e">The codes found in the current frame.</param>
    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var value = e.Results?.FirstOrDefault()?.Value;
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        // Detection fires per frame off the camera thread, so a code held in view raises this
        // repeatedly. Only the first one through gets to close the page.
        if (Interlocked.Exchange(ref _handled, 1) != 0)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(async () => await CloseAsync(value));
    }

    /// <summary>
    /// Stops the camera, dismisses the page and hands the result back to the caller.
    /// </summary>
    /// <param name="value">The code that was read, or null if the user cancelled.</param>
    private async Task CloseAsync(string? value)
    {
        // Detaching first stops the preview delivering frames into a page that is on its way
        // out, which on Android otherwise logs a stream of errors after the pop.
        _reader.BarcodesDetected -= OnBarcodesDetected;
        _reader.IsDetecting = false;

        if (Navigation.ModalStack.Contains(this))
        {
            await Navigation.PopModalAsync();
        }

        _result.TrySetResult(value);
    }
}
