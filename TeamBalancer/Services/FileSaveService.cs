namespace TeamBalancer.Services;

/// <summary>
/// Implementation of file save service using MAUI's Share API.
/// </summary>
public class FileSaveService : IFileSaveService
{
    /// <inheritdoc/>
    public async Task<string?> SaveAndShareAsync(string fileName, string content, string contentType)
    {
        // Save to cache directory first
        var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllTextAsync(filePath, content);

        // Share the file using platform's share dialog
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Save Players Export",
            File = new ShareFile(filePath, contentType)
        });

        return filePath;
    }
}
