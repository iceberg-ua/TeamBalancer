using Microsoft.AspNetCore.Components;
using TeamBalancer.Components.Layout;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Interfaces;

namespace TeamBalancer.Components.Pages;

/// <summary>
/// Code-behind for the MatchHistoryDetail component: one finished match in full - both
/// line-ups as they stood at the final whistle, the score, and each player's goals and
/// assists.
/// </summary>
/// <remarks>
/// The match is fetched by id rather than handed over by the screen that listed it. That keeps
/// the address a real address - reloading it, or arriving on it after the app was closed and
/// reopened, shows the same match - and costs one scan of a file the history screen has just
/// read anyway, with only the rows of the match being opened built into anything.
/// </remarks>
public partial class MatchHistoryDetail
{
    #region Injected Dependencies

    [Inject]
    private IMatchRepository MatchRepository { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [CascadingParameter]
    private MainLayout? Layout { get; set; }

    #endregion

    #region Parameters

    /// <summary>
    /// Gets or sets the match to show, from the address.
    /// </summary>
    [Parameter]
    public Guid MatchId { get; set; }

    #endregion

    #region Private Fields

    private FinishedMatch? _match;
    private bool _isLoading = true;
    private string _loadError = string.Empty;

    /// <summary>
    /// Which match has been loaded, or null before the first load. What stops the screen
    /// loading the same match over and over - see <see cref="OnParametersSetAsync"/>.
    /// </summary>
    private Guid? _loadedMatchId;

    /// <summary>
    /// Which side's line-up is open. Held rather than derived: the user picked it.
    /// </summary>
    private int _activeTabIndex;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the index of the side being shown, kept inside the sides this match has. A result
    /// always has at least two, so there is always one to show.
    /// </summary>
    private int ActiveIndex =>
        _match is null ? 0 : Math.Clamp(_activeTabIndex, 0, _match.Teams.Count - 1);

    /// <summary>
    /// Gets the line under the title: when the match was played, and how many took part.
    /// </summary>
    private string HeaderSubline
    {
        get
        {
            if (_match is null)
            {
                return string.Empty;
            }

            var count = Loc["playerList.playerCount", _match.PlayerCount];

            return $"{FormatPlayedAt(_match.PlayedAt)} · {count}";
        }
    }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Loads the match named in the address, and again if the address changes to another one.
    /// </summary>
    /// <remarks>
    /// Only when it does change, and that is not a saving. Blazor re-runs this on every render
    /// of the layout: it skips a parameter set only when it can prove nothing changed, and it
    /// can only prove that for a short list of known-immutable types that Guid is not on. A
    /// match id therefore always looks new. Loading unconditionally would then be a loop -
    /// the load refreshes the layout, the layout re-renders this page, the page loads again -
    /// which reads the file forever, and resets the open side out from under whoever tapped it.
    /// </remarks>
    protected override async Task OnParametersSetAsync()
    {
        if (_loadedMatchId == MatchId)
        {
            return;
        }

        await LoadMatch();
    }

    /// <inheritdoc />
    protected override void OnLanguageChanged()
    {
        base.OnLanguageChanged();

        // The header and footer belong to the layout, not to this page.
        Layout?.Refresh();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Finds the match in the history, leaving it null when the file no longer holds it.
    /// </summary>
    private async Task LoadMatch()
    {
        // Claimed before the read rather than after it, so that a load which fails is not
        // started again by the next render.
        var requested = MatchId;
        _loadedMatchId = requested;

        _isLoading = true;
        _loadError = string.Empty;

        // The side last looked at belongs to the match that was open, so a different match
        // opens on its first side rather than wherever the previous one was left.
        _activeTabIndex = 0;

        try
        {
            _match = await MatchRepository.GetByIdAsync(requested);
        }
        catch (Exception ex)
        {
            // The same reasoning as the history list: matches.csv is a file the user's other
            // tools can reach, and a read that fails has to be said rather than thrown out of
            // the renderer, which takes the app down.
            _match = null;
            _loadError = Loc["history.loadError", ex.Message];
        }

        // A newer load is already running for another match; its result is the one to show.
        if (_loadedMatchId != requested)
        {
            return;
        }

        _isLoading = false;

        Layout?.Refresh();
        StateHasChanged();
    }

    /// <summary>
    /// Goes back to the history list.
    /// </summary>
    private void GoBack()
    {
        Navigation.NavigateTo("/history");
    }

    #endregion
}
