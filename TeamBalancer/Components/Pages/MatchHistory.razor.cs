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
    private string _loadError = string.Empty;

    /// <summary>
    /// Counts the loads that have been started. A load that finds the count moved on has been
    /// overtaken - by a list switch, or by coming back to the screen - and drops what it read
    /// rather than writing the previous squad's games in under the current squad's name.
    /// </summary>
    private int _loadGeneration;

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
        var generation = ++_loadGeneration;

        _isLoading = true;
        _loadError = string.Empty;

        try
        {
            // Asked for rather than read off the repository: CurrentListId is Guid.Empty until
            // something has made it resolve, and this screen can be arrived at without the home
            // screen having run - a restart landing on the route the app was left on. Reading
            // it unresolved would filter every match away and show a squad with a season behind
            // it the screen for a squad that has never played.
            var listId = await ActivePlayerRepository.GetCurrentListIdAsync();

            // Ordering is the repository's - most recent first - and is deliberately not redone
            // here, so the one place that knows how the file is written is the one place that
            // decides what "most recent" means.
            var everything = await MatchRepository.GetAllAsync();

            var mine = everything.Where(match => match.ListId == listId).ToList();
            var name = await ActiveListName(listId);

            // Nothing is written into the screen until every read is back, so a load that has
            // been overtaken has nothing half-applied to undo.
            if (generation != _loadGeneration)
            {
                return;
            }

            _matches = mine;
            _activeListName = name;
        }
        catch (Exception ex)
        {
            if (generation != _loadGeneration)
            {
                return;
            }

            // matches.csv is plain text in a directory the user's other tools can reach, so a
            // read can fail on a phone that is doing nothing wrong. Said on the screen, the way
            // the Match screen says a save failed - the alternative is an exception out of the
            // renderer, which takes the app down, behind a spinner that never stops.
            _matches = [];
            _activeListName = string.Empty;
            _loadError = Loc["history.loadError", ex.Message];
        }

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
    /// <remarks>
    /// The task is not held onto because <see cref="LoadHistory"/> reports its own failures on
    /// the screen rather than throwing: there is nothing left for a caller to observe.
    /// </remarks>
    private void HandleListChanged() => InvokeAsync(LoadHistory);

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
    private static bool WasBeaten(FinishedMatch match, int index) =>
        match.Teams[index].Score < match.Teams[1 - index].Score;

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
