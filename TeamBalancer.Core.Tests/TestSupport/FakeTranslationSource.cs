namespace TeamBalancer.Core.Tests.TestSupport;

using System.Text;
using TeamBalancer.Core.Localization;

/// <summary>
/// Serves translation files from strings held in memory, so localization tests can describe
/// exactly which keys each language defines without touching the shipped files.
/// </summary>
public sealed class FakeTranslationSource : ITranslationSource
{
    private readonly Dictionary<string, string> _filesByLanguage;

    /// <param name="filesByLanguage">The JSON body for each language code.</param>
    public FakeTranslationSource(Dictionary<string, string> filesByLanguage)
    {
        _filesByLanguage = filesByLanguage;
    }

    /// <summary>
    /// Gets the number of times a file has been opened, which is how the tests tell a cached
    /// language from one being re-read.
    /// </summary>
    public int OpenCount { get; private set; }

    public Task<Stream?> OpenAsync(string languageCode)
    {
        OpenCount++;

        if (!_filesByLanguage.TryGetValue(languageCode, out var json))
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(new MemoryStream(Encoding.UTF8.GetBytes(json)));
    }
}
