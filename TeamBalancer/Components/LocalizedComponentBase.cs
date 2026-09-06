namespace TeamBalancer.Components;

using System.Globalization;
using Microsoft.AspNetCore.Components;
using TeamBalancer.Core.Localization;

/// <summary>
/// Base class for components that render translated text. It hands them the localization
/// service as <c>Loc</c> and re-renders them when the language changes, which is what keeps
/// the screen the switcher was used on from staying in the old language.
/// </summary>
/// <remarks>
/// It also holds the few display conventions every screen renders a match by - what a side is
/// called, what tints it, and how a date is written - because each of them is answered from
/// the current language and each was otherwise copied onto every screen that needed it.
/// </remarks>
public abstract class LocalizedComponentBase : ComponentBase, IDisposable
{
    [Inject]
    protected ILocalizationService Loc { get; set; } = default!;

    /// <summary>
    /// The culture behind <see cref="DateCulture"/>, resolved on first use and dropped when the
    /// language changes. Held rather than looked up per call: a date is formatted once per row,
    /// and a history of sixty games would otherwise take sixty culture lookups per repaint to
    /// answer a question whose answer only changes when the switcher is used.
    /// </summary>
    private CultureInfo? _dateCulture;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        Loc.LanguageChanged += HandleLanguageChanged;
    }

    /// <summary>
    /// Gets the culture dates are written in: the one the user picked in the app, falling back
    /// to the device's.
    /// </summary>
    /// <remarks>
    /// The app translates its words without touching the thread's culture, which is right for
    /// almost every screen - there is nothing culture-shaped on them. A date is different: it
    /// is the content rather than a detail of it, and "6 Sep" sitting in a Ukrainian screen
    /// reads as a bug. An unknown or unmapped code falls back rather than throwing, so a
    /// language added to the switcher without a matching culture still shows dates.
    /// </remarks>
    protected CultureInfo DateCulture
    {
        get
        {
            if (_dateCulture is not null)
            {
                return _dateCulture;
            }

            try
            {
                _dateCulture = CultureInfo.GetCultureInfo(Loc.CurrentLanguage);
            }
            catch (CultureNotFoundException)
            {
                _dateCulture = CultureInfo.CurrentCulture;
            }

            return _dateCulture;
        }
    }

    /// <summary>
    /// Writes when something happened, in the reader's own timezone. Storage is UTC so that
    /// games keep their order across a timezone change; only the reading converts.
    /// </summary>
    /// <param name="playedAt">The UTC timestamp to write.</param>
    protected string FormatPlayedAt(DateTime playedAt)
    {
        var local = playedAt.ToLocalTime();
        var culture = DateCulture;

        // The culture's own short date and short time, rather than a pattern of our own: a
        // pattern picked here would be wrong in some language the app already ships.
        return $"{local.ToString("d", culture)} · {local.ToString("t", culture)}";
    }

    /// <summary>
    /// Gets the name shown for the side at a position in a match. The strategies name teams
    /// "Team A", "Team B", ... by that same position, and rebuilding the label here is what
    /// lets it be translated - the stored name is generated in Core, which has no localization
    /// of its own, and a finished match keeps the name it was stored under.
    /// </summary>
    /// <param name="index">The side's index.</param>
    protected string TeamName(int index) => Loc["teams.name", (char)('A' + index)];

    /// <summary>
    /// Gets the modifier class that tints a side's chrome. Sides alternate between the accent
    /// and its sibling shade rather than taking a hue of their own.
    /// </summary>
    /// <param name="index">The side's index.</param>
    protected static string TeamColorClass(int index) => index % 2 == 0 ? "team-a" : "team-b";

    /// <summary>
    /// Reacts to a language change. Overrides should call the base implementation and then
    /// refresh anything this component renders outside its own markup, such as the header
    /// and footer it hands to the layout.
    /// </summary>
    protected virtual void OnLanguageChanged() => StateHasChanged();

    public virtual void Dispose()
    {
        Loc.LanguageChanged -= HandleLanguageChanged;
    }

    /// <summary>
    /// Hops onto the renderer's context before touching component state - the event is
    /// raised by the localization service, which makes no promise about which thread it
    /// arrives on.
    /// </summary>
    /// <remarks>
    /// The cached culture is dropped here rather than in <see cref="OnLanguageChanged"/> so
    /// that an override forgetting to call its base cannot leave dates in the old language.
    /// </remarks>
    private void HandleLanguageChanged() => InvokeAsync(() =>
    {
        _dateCulture = null;

        OnLanguageChanged();
    });
}
