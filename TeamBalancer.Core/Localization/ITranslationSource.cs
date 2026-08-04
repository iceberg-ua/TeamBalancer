namespace TeamBalancer.Core.Localization;

/// <summary>
/// Supplies the raw JSON translation file for a language. Implemented by the host app,
/// which knows how its assets are packaged.
/// </summary>
public interface ITranslationSource
{
    /// <summary>
    /// Opens the translation file for a language.
    /// </summary>
    /// <param name="languageCode">The two-letter code of the language to read.</param>
    /// <returns>
    /// A stream over the file's UTF-8 JSON, or null when the app ships no file for that
    /// language. A null result is not an error: the caller falls back to English.
    /// </returns>
    Task<Stream?> OpenAsync(string languageCode);
}
