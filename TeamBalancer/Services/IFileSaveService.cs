namespace TeamBalancer.Services;

/// <summary>
/// Service for saving files using platform-native mechanisms.
/// </summary>
public interface IFileSaveService
{
    /// <summary>
    /// Saves content to a file and shares it using the platform's share dialog.
    /// </summary>
    /// <param name="fileName">The name of the file to save.</param>
    /// <param name="content">The content to save.</param>
    /// <param name="contentType">The MIME type of the content.</param>
    /// <returns>The path where the file was saved, or null if cancelled.</returns>
    Task<string?> SaveAndShareAsync(string fileName, string content, string contentType);
}
