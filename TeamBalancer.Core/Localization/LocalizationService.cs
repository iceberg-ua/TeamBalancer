namespace TeamBalancer.Core.Localization;

using System.Globalization;
using System.Text;
using System.Text.Json;

/// <summary>
/// Serves translations from flat key/value JSON files, one per language, with English as the
/// fallback for anything the active language leaves untranslated.
/// </summary>
public sealed class LocalizationService : ILocalizationService
{
    private static readonly IReadOnlyDictionary<string, string> EmptyCatalog =
        new Dictionary<string, string>();

    /// <summary>
    /// Comments and trailing commas are tolerated because these files are hand-edited, often
    /// by someone translating rather than someone writing JSON.
    /// </summary>
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly ITranslationSource _source;
    private readonly ILanguagePreference _preference;
    private readonly Func<CultureInfo> _deviceCulture;

    /// <summary>
    /// Every catalog read so far, keyed by language code. A language is only read from disk
    /// once; switching back to it later is instant.
    /// </summary>
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _catalogs =
        new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyDictionary<string, string> _active = EmptyCatalog;
    private IReadOnlyDictionary<string, string> _fallback = EmptyCatalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalizationService"/> class.
    /// </summary>
    /// <param name="source">Supplies the translation files.</param>
    /// <param name="preference">Stores and reads the user's chosen language.</param>
    /// <param name="deviceCulture">
    /// Reads the culture the device is set to, used when the user has never chosen a
    /// language. Defaults to the current UI culture; tests pass their own.
    /// </param>
    public LocalizationService(
        ITranslationSource source,
        ILanguagePreference preference,
        Func<CultureInfo>? deviceCulture = null)
    {
        _source = source;
        _preference = preference;
        _deviceCulture = deviceCulture ?? (() => CultureInfo.CurrentUICulture);
    }

    /// <inheritdoc />
    public event Action? LanguageChanged;

    /// <inheritdoc />
    public IReadOnlyList<SupportedLanguage> SupportedLanguages => SupportedLanguage.All;

    /// <inheritdoc />
    public string CurrentLanguage { get; private set; } = SupportedLanguage.Default.Code;

    /// <inheritdoc />
    public string this[string key] => Lookup(key) ?? MissingKeyMarker(key);

    /// <inheritdoc />
    public string this[string key, params object[] args]
    {
        get
        {
            var template = Lookup(key);
            if (template is null)
            {
                return MissingKeyMarker(key);
            }

            try
            {
                return string.Format(CultureInfo.CurrentCulture, template, args);
            }
            catch (FormatException)
            {
                // A translation with a malformed placeholder is a bad string, not a reason to
                // tear down the screen rendering it. Show it unsubstituted instead.
                return template;
            }
        }
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _fallback = await LoadAsync(SupportedLanguage.Default.Code).ConfigureAwait(false);

        var initial = ResolveInitialLanguage();
        _active = await LoadAsync(initial.Code).ConfigureAwait(false);
        CurrentLanguage = initial.Code;
    }

    /// <inheritdoc />
    public async Task SetLanguageAsync(string code)
    {
        var language = SupportedLanguage.Find(code)
            ?? throw new ArgumentException($"'{code}' is not a supported language.", nameof(code));

        // Store the choice even when it matches the language already in use: picking the
        // language the app happened to default to is exactly how a user pins it, and without
        // a stored preference the next launch would resolve the device culture all over again.
        _preference.Write(language.Code);

        if (string.Equals(language.Code, CurrentLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _active = await LoadAsync(language.Code).ConfigureAwait(false);
        CurrentLanguage = language.Code;

        LanguageChanged?.Invoke();
    }

    /// <summary>
    /// Marks a key no language translates, in a form that stands out on screen.
    /// </summary>
    private static string MissingKeyMarker(string key) => $"[[{key}]]";

    /// <summary>
    /// Finds a key's translation in the active language, then in the fallback one. Blank
    /// values count as untranslated so that a placeholder entry left empty in a translation
    /// file still shows the English text rather than nothing at all.
    /// </summary>
    private string? Lookup(string key)
    {
        if (_active.TryGetValue(key, out var translated) && !string.IsNullOrEmpty(translated))
        {
            return translated;
        }

        if (_fallback.TryGetValue(key, out var fallback) && !string.IsNullOrEmpty(fallback))
        {
            return fallback;
        }

        return null;
    }

    /// <summary>
    /// Decides which language the app starts in: the language the user chose, else the
    /// device's language when we ship it, else English.
    /// </summary>
    private SupportedLanguage ResolveInitialLanguage() =>
        SupportedLanguage.Find(_preference.Read())
        ?? SupportedLanguage.Find(_deviceCulture().TwoLetterISOLanguageName)
        ?? SupportedLanguage.Default;

    private async Task<IReadOnlyDictionary<string, string>> LoadAsync(string languageCode)
    {
        if (_catalogs.TryGetValue(languageCode, out var cached))
        {
            return cached;
        }

        var catalog = await ReadCatalogAsync(languageCode).ConfigureAwait(false);
        _catalogs[languageCode] = catalog;

        return catalog;
    }

    /// <summary>
    /// Reads and parses one language's file. A file that is missing, unreadable or malformed
    /// degrades to an empty catalog rather than taking the app down with it - every lookup
    /// then falls through to English, which is the same outcome as an untranslated key.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> ReadCatalogAsync(string languageCode)
    {
        try
        {
            using var stream = await _source.OpenAsync(languageCode).ConfigureAwait(false);
            if (stream is null)
            {
                return EmptyCatalog;
            }

            // Read through a StreamReader rather than handing the stream to the deserializer:
            // it strips a byte order mark, which editors add freely and System.Text.Json
            // rejects as an invalid start of a value.
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var json = await reader.ReadToEndAsync().ConfigureAwait(false);

            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json, SerializerOptions);

            return parsed ?? EmptyCatalog;
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            return EmptyCatalog;
        }
    }
}
