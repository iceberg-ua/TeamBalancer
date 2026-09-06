using System.Globalization;
using Microsoft.AspNetCore.Components;
using TeamBalancer.Components.Layout;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Interfaces;

namespace TeamBalancer.Components.Pages;

/// <summary>
/// Code-behind for the MatchHistory component: the games this squad has finished, most recent
/// first. Read-only - matches.csv is only ever appended to, and nothing here changes a result
/// that has already been written down.
/// </summary>
public partial class MatchHistory
{
    #region Injected Dependencies

    [Inject]
    private IMatchRepository MatchRepository { get; set; } = default!;

    [Inject]
    private IActivePlayerRepository ActivePlayerRepository { get; set; } = default!;

    [Inject]
    private IPlayerListRepository PlayerListRepository { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [CascadingParameter]
    private MainLayout? Layout { get; set; }

    #endregion

    #region Private Fields

    private List<FinishedMatch> _matches = [];
    private string _activeListName = string.Empty;
    private bool _isLoading = true;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the line under the title: which squad's history this is, and how many games are in
    /// it. The name is dropped rather than left dangling before the separator when there is
    /// none to show, as on the Teams and Match screens.
    /// </summary>
    private string HeaderSubline
    {
        get
        {
            var played = Loc["history.matchCount", _matches.Count];

            return string.IsNullOrEmpty(_activeListName)
                ? played
                : $"{_activeListName} · {played}";
        }
    }

    /// <summary>
    /// Gets the culture dates are written in: the one the user picked in the app, falling back
    /// to the device's.
    /// </summary>
    /// <remarks>
    /// The app translates its words without touching the thread's culture, which is right for
    /// almost every screen - there is nothing culture-shaped on them. A date is different: it
    /// is the content here rather than a detail of it, and "6 Sep" sitting in a Ukrainian
    /// screen reads as a bug. An unknown or unmapped code falls back rather than throwing, so a
    /// language added to the switcher without a matching culture still shows dates.
    /// </remarks>
    private CultureInfo DateCulture
    {
        get
        {
            try
            {
                return CultureInfo.GetCultureInfo(Loc.CurrentLanguage);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.CurrentCulture;
            }
        }
    }

    #endregion

    #region Lifecycle Methods

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        base.OnInitialized();

        ActivePlayerRepository.ListChanged += HandleListChanged;
    }

    /// <summary>
    /// Loads on every arrival rather than once, so a match finished since this screen was last
    /// open is here when it is come back to.
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
        await LoadHistory();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        ActivePlayerRepository.ListChanged -= HandleListChanged;

        base.Dispose();
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
    /// Reads the history and keeps the active list's part of it.
    /// </summary>
    /// <remarks>
    /// Filtered here rather than in the repository: one flat file holds every match, so reading
    /// all of them is what reading any of them costs, and the screen is the thing that has an
    /// opinion about which squad is being looked at.
    /// </remarks>
    private async Task LoadHistory()
    {
        _isLoading = true;

        // Ordering is the repository's - most recent first - and is deliberately not redone
        // here, so the one place that knows how the file is written is the one place that
        // decides what "most recent" means.
        var everything = await MatchRepository.GetAllAsync();
        var listId = ActivePlayerRepository.CurrentListId;

        _matches = [.. everything.Where(match => match.ListId == listId)];
        _activeListName = await ActiveListName(listId);

        _isLoading = false;

        Layout?.Refresh();
        StateHasChanged();
    }

    /// <summary>
    /// Names the active list for the subline, or nothing when it cannot be found - a list
    /// deleted while this screen was open should not take the screen down with it.
    /// </summary>
    /// <param name="listId">The list to name.</param>
    private async Task<string> ActiveListName(Guid listId)
    {
        var list = await PlayerListRepository.GetByIdAsync(listId);

        return list?.Name ?? string.Empty;
    }

    /// <summary>
    /// Reloads after the active list changed, since this screen shows one list's games.
    /// </summary>
    private void HandleListChanged() => InvokeAsync(LoadHistory);

    /// <summary>
    /// Writes when a match was played, in the reader's own timezone. Storage is UTC so that
    /// games keep their order across a timezone change; only the reading converts.
    /// </summary>
    /// <param name="playedAt">The UTC timestamp the match was finished at.</param>
    private string FormatPlayedAt(DateTime playedAt)
    {
        var local = playedAt.ToLocalTime();
        var culture = DateCulture;

        // The culture's own short date and short time, rather than a pattern of our own: a
        // pattern picked here would be wrong in some language the app already ships.
        return $"{local.ToString("d", culture)} · {local.ToString("t", culture)}";
    }

    /// <summary>
    /// Names one side of a match.
    /// </summary>
    /// <remarks>
    /// A finished match always has two sides, because that is what a split is. This reads a
    /// file rather than a split, though, so both accessors answer for an index that is not
    /// there instead of letting a hand-edited row take the screen down.
    /// </remarks>
    /// <param name="match">The match.</param>
    /// <param name="index">Which side.</param>
    private static string SideName(FinishedMatch match, int index) =>
        index < match.Teams.Count ? match.Teams[index].Name : string.Empty;

    /// <summary>
    /// Gets what one side scored.
    /// </summary>
    /// <param name="match">The match.</param>
    /// <param name="index">Which side.</param>
    private static int SideScore(FinishedMatch match, int index) =>
        index < match.Teams.Count ? match.Teams[index].Score : 0;

    /// <summary>
    /// Gets whether one side lost.
    /// </summary>
    /// <remarks>
    /// Losing is what the row marks, rather than winning. The two are not opposites here: a
    /// draw is neither, and dimming the side that did not win would dim both halves of a draw
    /// and leave it reading as two defeats. Asking who was beaten leaves a draw at full
    /// strength on both sides, which is what a draw is.
    /// </remarks>
    /// <param name="match">The match.</param>
    /// <param name="index">Which side.</param>
    private static bool WasBeaten(FinishedMatch match, int index)
    {
        if (match.Teams.Count < 2)
        {
            return false;
        }

        var mine = SideScore(match, index);
        var theirs = SideScore(match, 1 - index);

        return mine < theirs;
    }

    /// <summary>
    /// Opens one match in full.
    /// </summary>
    /// <param name="matchId">The match to show.</param>
    private void OpenMatch(Guid matchId)
    {
        Navigation.NavigateTo($"/history/{matchId}");
    }

    /// <summary>
    /// Navigates back to the home screen.
    /// </summary>
    private void GoHome()
    {
        Navigation.NavigateTo("/");
    }

    #endregion
}
