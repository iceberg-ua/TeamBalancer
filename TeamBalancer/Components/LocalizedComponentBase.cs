namespace TeamBalancer.Components;

using Microsoft.AspNetCore.Components;
using TeamBalancer.Core.Localization;

/// <summary>
/// Base class for components that render translated text. It hands them the localization
/// service as <c>Loc</c> and re-renders them when the language changes, which is what keeps
/// the screen the switcher was used on from staying in the old language.
/// </summary>
public abstract class LocalizedComponentBase : ComponentBase, IDisposable
{
    [Inject]
    protected ILocalizationService Loc { get; set; } = default!;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        Loc.LanguageChanged += HandleLanguageChanged;
    }

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
    private void HandleLanguageChanged() => InvokeAsync(OnLanguageChanged);
}
