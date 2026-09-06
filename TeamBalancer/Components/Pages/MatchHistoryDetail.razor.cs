using System.Globalization;
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
/// reopened, shows the same match - and costs one read of a file the history screen has just
/// read anyway.
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

    /// <summary>
    /// Which side's line-up is open. Held rather than derived: the user picked it.
    /// </summary>
    private int _activeTabIndex;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the index of the side being shown, kept inside the sides this match actually has.
    /// </summary>
    private int ActiveIndex =>
        _match is null || _match.Teams.Count == 0
            ? 0
            : Math.Clamp(_activeTabIndex, 0, _match.Teams.Count - 1);

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
    /// Gets the culture dates are written in: the one the user picked in the app, falling back
    /// to the device's. The same reasoning as on the history list - see MatchHistory.
    /// </summary>
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

    /// <summary>
    /// Loads the match named in the address, and again if the address changes to another one.
    /// </summary>
    protected override async Task OnParametersSetAsync()
    {
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
        _isLoading = true;

        var everything = await MatchRepository.GetAllAsync();

        _match = everything.FirstOrDefault(match => match.Id == MatchId);

        // The side last looked at belongs to the match that was open, so a different match
        // opens on its first side rather than wherever the previous one was left.
        _activeTabIndex = 0;
        _isLoading = false;

        Layout?.Refresh();
        StateHasChanged();
    }

    /// <summary>
    /// Writes when the match was played, in the reader's own timezone.
    /// </summary>
    /// <param name="playedAt">The UTC timestamp the match was finished at.</param>
    private string FormatPlayedAt(DateTime playedAt)
    {
        var local = playedAt.ToLocalTime();
        var culture = DateCulture;

        return $"{local.ToString("d", culture)} · {local.ToString("t", culture)}";
    }

    /// <summary>
    /// Gets the class that tints a side, matching the Teams and Match screens: the first side
    /// is the accent, the second the palette's other hue.
    /// </summary>
    /// <param name="index">Which side.</param>
    private static string TeamColorClass(int index) => index == 0 ? "team-a" : "team-b";

    /// <summary>
    /// Goes back to the history list.
    /// </summary>
    private void GoBack()
    {
        Navigation.NavigateTo("/history");
    }

    #endregion
}
