using Microsoft.AspNetCore.Components;
using TeamBalancer.Components.Layout;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Interfaces;

namespace TeamBalancer.Components.Pages;

/// <summary>
/// Code-behind for the MatchHistoryDetail component: one finished match in full - both
/// line-ups as they stood at the final whistle, the score, and each player's goals and
/// assists - and the way to delete it from the history.
/// </summary>
/// <remarks>
/// The match is fetched by id rather than handed over by the screen that listed it. That keeps
/// the address a real address - reloading it, or arriving on it after the app was closed and
/// reopened, shows the same match - and costs one scan of a file the history screen has just
/// read anyway, with only the rows of the match being opened built into anything.
/// </remarks>
public partial class MatchHistoryDetail
{
    private const string HistoryRoute = "/history";

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

    /// <summary>
    /// Whether the sheet asking to delete this match is open.
    /// </summary>
    private bool _confirmingDelete;

    /// <summary>
    /// Set while the file is being rewritten without this match, so the button that started it
    /// is held down until it is over - see <see cref="ConfirmDelete"/>.
    /// </summary>
    private bool _isDeleting;

    /// <summary>
    /// Why the last delete failed, or empty. Said on the sheet, which stays open for another
    /// try.
    /// </summary>
    private string _deleteError = string.Empty;

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

    /// <summary>
    /// Gets the result as one line, for the sheet that asks before deleting it - written the
    /// way the Match screen reads a result back before saving it.
    /// </summary>
    private string Scoreline => _match is not { Teams.Count: >= 2 }
        ? string.Empty
        : Loc["match.scoreline",
            TeamName(0),
            _match.Teams[0].Score,
            _match.Teams[1].Score,
            TeamName(1)];

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
        // opens on its first side rather than wherever the previous one was left. A delete
        // that was being asked about belonged to it too.
        _activeTabIndex = 0;
        _confirmingDelete = false;
        _deleteError = string.Empty;

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
    /// Opens the sheet that asks before deleting. Nothing is removed until it is answered.
    /// </summary>
    private void AskToDelete()
    {
        if (_match is null)
        {
            return;
        }

        _deleteError = string.Empty;
        _confirmingDelete = true;

        // The button that asked is in the layout's footer, and stays lit while the sheet is up.
        Layout?.Refresh();
    }

    /// <summary>
    /// Closes the sheet, changing nothing. Refused while the delete is running: the rewrite
    /// finishes either way, and closing the sheet over it would leave the user looking at a
    /// match that is already gone.
    /// </summary>
    private void CancelDelete()
    {
        if (_isDeleting)
        {
            return;
        }

        _confirmingDelete = false;
        _deleteError = string.Empty;

        Layout?.Refresh();
    }

    /// <summary>
    /// Removes the match from the history and goes back to the list, which reads the file
    /// again on arrival and so no longer shows it.
    /// </summary>
    /// <remarks>
    /// A failed delete leaves the sheet open with the reason on it and the match still in the
    /// history: the rewrite goes through a copy of the file, so a failure part way changes
    /// nothing. A match that turns out to be gone already is not a failure - the history the
    /// user asked for is the one they have - so the answer the repository gives is not needed
    /// here.
    /// </remarks>
    private async Task ConfirmDelete()
    {
        if (_match is null || _isDeleting)
        {
            return;
        }

        _isDeleting = true;
        _deleteError = string.Empty;

        try
        {
            await MatchRepository.DeleteAsync(_match.Id);
        }
        catch (Exception ex)
        {
            _deleteError = Loc["history.deleteError", ex.Message];
            _isDeleting = false;

            return;
        }

        Navigation.NavigateTo(HistoryRoute);
    }

    /// <summary>
    /// Goes back to the history list.
    /// </summary>
    private void GoBack()
    {
        Navigation.NavigateTo(HistoryRoute);
    }

    #endregion
}
