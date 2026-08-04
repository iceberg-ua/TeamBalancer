namespace TeamBalancer.Localization;

using TeamBalancer.Core.Localization;

/// <summary>
/// Reads the translation files that ship inside the app package as raw assets.
/// </summary>
public sealed class MauiTranslationSource : ITranslationSource
{
    /// <summary>
    /// The folder the translation files are deployed under. Must match the LogicalName given
    /// to the Resources\Languages MauiAsset items in the csproj.
    /// </summary>
    private const string LanguagesFolder = "Languages";

    /// <inheritdoc />
    public async Task<Stream?> OpenAsync(string languageCode)
    {
        try
        {
            return await FileSystem.OpenAppPackageFileAsync($"{LanguagesFolder}/{languageCode}.json");
        }
        catch (FileNotFoundException)
        {
            // Nothing shipped for that language. The caller falls back to English rather than
            // treating this as fatal.
            return null;
        }
    }
}
