namespace TeamBalancer.Services;

/// <summary>
/// Renders text as a QR code the Blazor UI can display.
/// </summary>
public interface IQrCodeService
{
    /// <summary>
    /// Renders text as a QR code image.
    /// </summary>
    /// <param name="content">The text the code should carry.</param>
    /// <param name="pixelSize">The width and height of the rendered image, in pixels.</param>
    /// <returns>
    /// A data URI holding a PNG, ready to be used as the src of an img element. It is returned
    /// inline rather than as a file because the screen showing it is a web view, and a data URI
    /// needs neither a temporary file nor a scheme the view is allowed to load from disk.
    /// </returns>
    Task<string> CreateQrImageAsync(string content, int pixelSize = 1024);
}
