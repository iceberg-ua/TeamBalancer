using System.Globalization;
using Microsoft.AspNetCore.Components;
using TeamBalancer.Components.Layout;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Balancing;
using TeamBalancer.Core.Services.Interfaces;
using TeamBalancer.Services;

namespace TeamBalancer.Components.Pages;

public partial class Teams
{
    /// <summary>
    /// Where the back arrow goes: the screen the split was made from, with the same players
    /// still picked.
    /// </summary>
    private const string SelectPlayersRoute = "/create-teams";

    private const string HomeRoute = "/";

    /// <summary>
    /// Where accepting the split goes: the screen the game itself is recorded on.
    /// </summary>
    private const string MatchRoute = "/match";

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private TeamStateService TeamState { get; set; } = default!;

    [Inject]
    private TeamBalancingService BalancingService { get; set; } = default!;

    [Inject]
    private IActivePlayerRepository ActivePlayerRepository { get; set; } = default!;

    [Inject]
    private IPlayerListRepository PlayerListRepository { get; set; } = default!;

    [Inject]
    private MatchStateService MatchState { get; set; } = default!;

    [CascadingParameter]
    private MainLayout? Layout { get; set; }

    public List<Team>? GeneratedTeams { get; set; }

    private int _activeTabIndex = 0;

    private string _activeListName = string.Empty;

    /// <summary>
    /// The guarded action waiting on an answer, if any. Both are asked the same way, so the
    /// sheet is one block of markup driven by which of them is pending.
    /// </summary>
    private enum PendingAction
    {
        None,
        Leave,
        Reshuffle
    }

    private PendingAction _pending = PendingAction.None;

    private string _leaveDestination = HomeRoute;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        // Load teams from state service
        GeneratedTeams = TeamState.CurrentTeams;

        // Subscribe to team changes
        TeamState.OnTeamsChanged += OnTeamsChanged;
    }

    /// <summary>
    /// Reads the name of the list this split came out of, for the line under the title. The
    /// Teams screen cannot change which list is active, so reading it once is enough. A list
    /// that cannot be named yet leaves the subline as the player count alone.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        var lists = await PlayerListRepository.GetAllAsync();

        _activeListName = lists
            .FirstOrDefault(l => l.Id == ActivePlayerRepository.CurrentListId)?.Name
            ?? string.Empty;

        // The subline is part of the header, which belongs to the layout rather than to this
        // page, so re-rendering the page alone would leave it empty.
        Layout?.Refresh();
    }

    private void OnTeamsChanged()
    {
        GeneratedTeams = TeamState.CurrentTeams;
        StateHasChanged();
    }

    /// <summary>
    /// Gets the index of the team being shown. Teams can be regenerated underneath us, so the
    /// stored index is never trusted blindly.
    /// </summary>
    private int ActiveIndex => GeneratedTeams is { Count: > 0 }
        ? Math.Clamp(_activeTabIndex, 0, GeneratedTeams.Count - 1)
        : 0;

    /// <summary>
    /// Gets whether the two sides can be put head to head - the scoreboard comparison, the
    /// header pill and moving a player between teams all assume there is exactly one other
    /// side to compare with or move to.
    /// </summary>
    private bool IsComparable => GeneratedTeams is { Count: 2 };

    /// <summary>
    /// Gets whether a player can be sent to the other team from their row.
    /// </summary>
    private bool CanMovePlayers => IsComparable;

    /// <summary>
    /// Gets the label on the button that sends a player over: the team they are not on.
    /// </summary>
    private string MoveTitle => Loc["teams.moveTo", TeamName(1 - ActiveIndex)];

    /// <summary>
    /// Gets the line under the title: which list the split came out of, and how many players
    /// it put on the pitch. The name is dropped rather than left dangling before the separator
    /// when there is none to show.
    /// </summary>
    private string HeaderSubline
    {
        get
        {
            var players = GeneratedTeams?.Sum(t => t.Players.Count) ?? 0;
            var count = Loc["playerList.playerCount", players];

            return string.IsNullOrEmpty(_activeListName)
                ? count
                : $"{_activeListName} · {count}";
        }
    }

    /// <summary>
    /// How far two overall ratings may sit apart and still be called level. The scoreboard
    /// shows one decimal, so anything closer than half of one is a difference the screen
    /// itself does not show.
    /// </summary>
    private const double LevelTolerance = 0.05;

    /// <summary>
    /// Gets the gap between the two teams' overall ratings.
    /// </summary>
    private double OverallGap => IsComparable
        ? Math.Abs(Overall(GeneratedTeams![0]) - Overall(GeneratedTeams[1]))
        : 0;

    /// <summary>
    /// Gets whether the draw came out level - which is what the header pill says when it did.
    /// </summary>
    private bool IsLevel => OverallGap < LevelTolerance;

    /// <summary>
    /// Gets the header pill's wording: "Balanced" for a level draw, otherwise the team the
    /// draw leans to and the size of the lean.
    /// </summary>
    private string BalanceLabel
    {
        get
        {
            if (IsLevel)
            {
                return Loc["teams.balanced"];
            }

            var leader = Overall(GeneratedTeams![0]) > Overall(GeneratedTeams[1]) ? 0 : 1;

            return Loc["teams.aheadBy", TeamName(leader), OverallGap.ToString("F1")];
        }
    }

    /// <summary>
    /// Sends a player to the other team. Nothing is rebalanced around them: this is the user
    /// overruling the draw, and the scoreboard reports what that did rather than undoing it.
    /// </summary>
    /// <param name="player">The player to move off the team being shown.</param>
    private void MovePlayer(Player player)
    {
        var teams = GeneratedTeams;
        if (teams is not { Count: 2 })
        {
            return;
        }

        var from = teams[ActiveIndex];
        var to = teams[1 - ActiveIndex];

        if (!from.RemovePlayer(player))
        {
            return;
        }

        to.AddPlayer(player);

        // The same list, changed in place - handing it back is what tells the rest of the app
        // the split has moved on.
        TeamState.SetTeams(teams);

        // The pill reporting how the draw stands lives in the header, which belongs to the
        // layout rather than to this page.
        Layout?.Refresh();
    }

    /// <summary>
    /// Gets whether the split is one that can be played: two sides, both with someone on them.
    /// </summary>
    private bool CanAccept => GeneratedTeams is { Count: 2 }
        && GeneratedTeams.All(t => t.Players.Count > 0);

    /// <summary>
    /// Takes the split as it stands and goes off to play it.
    /// </summary>
    /// <remarks>
    /// This is the one way off this screen that asks nothing first. Every other exit throws
    /// the split away, which is what the sheet exists to confirm; accepting keeps it, so
    /// there is nothing to warn about.
    ///
    /// The split is left in <see cref="TeamState"/> rather than cleared. The match holds sides
    /// of its own from here on, and clearing would only matter if the user came back to this
    /// screen - which, having accepted, they do by drawing again from Select Players anyway.
    /// </remarks>
    private void AcceptTeams()
    {
        if (!CanAccept)
        {
            return;
        }

        MatchState.StartMatch(MatchRecord.FromTeams(GeneratedTeams!, ActivePlayerRepository.CurrentListId));

        Navigation.NavigateTo(MatchRoute);
    }

    /// <summary>
    /// Asks before leaving. The split only ever lives in memory, so walking away from this
    /// screen is what throws it out - the sheet says so, and the answer decides.
    /// </summary>
    /// <param name="destination">Where to go once the user says yes.</param>
    private void AskToLeave(string destination)
    {
        // Nothing to lose before a split exists.
        if (GeneratedTeams is not { Count: > 0 })
        {
            Navigation.NavigateTo(destination);
            return;
        }

        _leaveDestination = destination;
        _pending = PendingAction.Leave;

        RefreshArmedButtons();
    }

    /// <summary>
    /// Gets whether the way out to a destination is the one currently being asked about. The
    /// arrow and the house both leave, so the sheet lights up only the one that opened it.
    /// </summary>
    /// <param name="destination">The route the button leads to.</param>
    private bool IsArmed(string destination) =>
        _pending == PendingAction.Leave && _leaveDestination == destination;

    /// <summary>
    /// Repaints the header and footer, which hold two of the three buttons a sheet can be
    /// opened from and belong to the layout rather than to this page.
    /// </summary>
    private void RefreshArmedButtons() => Layout?.Refresh();

    /// <summary>
    /// Leaves, discarding the split and any moves made by hand.
    /// </summary>
    private void ConfirmLeave()
    {
        _pending = PendingAction.None;

        // Cleared before navigating: the teams outlive this page inside the state service, and
        // leaving them there would contradict the question that was just answered.
        TeamState.ClearTeams();

        Navigation.NavigateTo(_leaveDestination);
    }

    private void AskToReshuffle()
    {
        _pending = PendingAction.Reshuffle;

        RefreshArmedButtons();
    }

    private void CancelPending()
    {
        _pending = PendingAction.None;

        RefreshArmedButtons();
    }

    /// <summary>
    /// Draws both teams again from the same players.
    /// </summary>
    private void ConfirmReshuffle()
    {
        _pending = PendingAction.None;

        if (GeneratedTeams == null || GeneratedTeams.Count == 0)
            return;

        // Collect all players from all teams
        var allPlayers = GeneratedTeams.SelectMany(t => t.Players).ToList();
        var numberOfTeams = GeneratedTeams.Count;

        // Use the balancing service with shuffle=true for variety while maintaining balance
        var newTeams = BalancingService.BalanceTeams(allPlayers, numberOfTeams, shuffle: true);

        // The balancing strategies already name teams "Team A", "Team B", ... - leave those
        // alone so a reshuffle doesn't rename the sides out from under the user.

        // Update the state service with new teams
        TeamState.SetTeams(newTeams);

        // The header pill belongs to the layout, and a new draw is exactly when it changes.
        Layout?.Refresh();
    }

    /// <summary>
    /// The highest value a single skill rating can take. Comparison bars are drawn as a
    /// fraction of it, so a full bar means a team averages the maximum on that skill.
    /// </summary>
    private const double MaxSkillRating = 3;

    /// <summary>
    /// One skill compared across the two teams, rendered as a mirrored pair of bars.
    /// </summary>
    private sealed record BalanceMetric(string Label, double LeftValue, double RightValue);

    /// <summary>
    /// Gets the three skills the comparison card puts head to head.
    /// </summary>
    private IEnumerable<BalanceMetric> GetBalanceMetrics(Team left, Team right)
    {
        yield return new BalanceMetric(Loc["skill.speed"], left.AverageSpeed, right.AverageSpeed);
        yield return new BalanceMetric(Loc["skill.technical"], left.AverageTechnicalSkills, right.AverageTechnicalSkills);
        yield return new BalanceMetric(Loc["skill.stamina"], left.AverageStamina, right.AverageStamina);
    }

    /// <summary>
    /// Gets the name shown for the team at a position in the list. The strategies name teams
    /// "Team A", "Team B", ... by that same position, and rebuilding the label here is what
    /// lets it be translated - the stored name is generated in Core, which has no
    /// localization of its own.
    /// </summary>
    /// <param name="index">The team's index in <see cref="GeneratedTeams"/>.</param>
    private string TeamName(int index) => Loc["teams.name", (char)('A' + index)];

    /// <summary>
    /// Gets a team's overall rating: the mean of its three skill averages. This is the figure
    /// each half of the scoreboard puts under the team's name.
    /// </summary>
    private static double Overall(Team team) =>
        (team.AverageSpeed + team.AverageTechnicalSkills + team.AverageStamina) / 3.0;

    /// <summary>
    /// Renders an average rating as a CSS width. The invariant culture keeps the decimal
    /// separator a dot, which a comma locale would otherwise turn into an invalid length.
    /// </summary>
    private static string Percent(double value) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Math.Clamp(value / MaxSkillRating * 100, 0, 100):F1}%");

    /// <summary>
    /// Gets the modifier class that tints a team's chrome. Teams alternate between the
    /// accent and its sibling shade rather than taking a hue of their own.
    /// </summary>
    private static string TeamColorClass(int index) => index % 2 == 0 ? "team-a" : "team-b";

    /// <summary>
    /// Goes back to pick players, from the empty screen where there is no split to lose.
    /// </summary>
    private void GoToCreateTeams()
    {
        Navigation.NavigateTo(SelectPlayersRoute);
    }

    public override void Dispose()
    {
        TeamState.OnTeamsChanged -= OnTeamsChanged;

        base.Dispose();
    }
}
