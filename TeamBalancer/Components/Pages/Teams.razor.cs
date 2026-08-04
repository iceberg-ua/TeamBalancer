using System.Globalization;
using Microsoft.AspNetCore.Components;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Balancing;
using TeamBalancer.Extensions;
using TeamBalancer.Services;

namespace TeamBalancer.Components.Pages;

public partial class Teams
{
    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private TeamStateService TeamState { get; set; } = default!;

    [Inject]
    private TeamBalancingService BalancingService { get; set; } = default!;

    public List<Team>? GeneratedTeams { get; set; }

    private int _activeTabIndex = 0;

    protected override void OnInitialized()
    {
        base.OnInitialized();

        // Load teams from state service
        GeneratedTeams = TeamState.CurrentTeams;

        // Subscribe to team changes
        TeamState.OnTeamsChanged += OnTeamsChanged;
    }

    private void OnTeamsChanged()
    {
        GeneratedTeams = TeamState.CurrentTeams;
        StateHasChanged();
    }

    private void ReshuffleTeams()
    {
        if (GeneratedTeams == null || !GeneratedTeams.Any())
            return;

        // Collect all players from all teams
        var allPlayers = GeneratedTeams.SelectMany(t => t.Players).ToList();
        var numberOfTeams = GeneratedTeams.Count;

        // Use the balancing service with shuffle=true for variety while maintaining balance
        var newTeams = BalancingService.BalanceTeams(allPlayers, numberOfTeams, shuffle: true);

        // The balancing strategies already name teams "Team A", "Team B", ... - leave those
        // alone so a reshuffle doesn't rename the tabs out from under the user.

        // Update the state service with new teams
        TeamState.SetTeams(newTeams);
    }

    /// <summary>
    /// The highest value a single skill rating can take. Comparison bars are drawn as a
    /// fraction of it, so a full bar means a team averages the maximum on that skill.
    /// </summary>
    private const double MaxSkillRating = 3;

    /// <summary>
    /// A single position count shown in a team's position summary.
    /// </summary>
    private sealed record PositionSummaryEntry(string Label, string CssClass, int Count, string Title);

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
    /// Gets a team's overall rating: the mean of its three skill averages. This is the
    /// figure the comparison legend puts next to each team's name.
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
    /// Counts a team's players by primary position. Every real position is returned even when
    /// its count is zero - a missing goalkeeper is exactly what this summary should reveal.
    /// Players without a position land in a separate "Unset" entry, which is only shown when
    /// it is non-empty.
    /// </summary>
    private IEnumerable<PositionSummaryEntry> GetPositionSummary(Team team)
    {
        foreach (var position in PositionExtensions.SelectablePositions)
        {
            var count = team.Players.Count(p => p.PrimaryPosition == position);
            yield return new PositionSummaryEntry(
                position.ToAbbreviation(),
                position.ToBadgeClass(),
                count,
                Loc["teams.positionCountTitle", count, position.ToDisplayName(Loc)]);
        }

        var unsetCount = team.Players.Count(p => p.PrimaryPosition == Position.Unspecified);
        if (unsetCount > 0)
        {
            yield return new PositionSummaryEntry(
                Loc["position.unset"],
                "pos-unset",
                unsetCount,
                Loc["teams.unsetCountTitle", unsetCount]);
        }
    }

    private void GoToCreateTeams()
    {
        Navigation.NavigateTo("/create-teams");
    }

    private void GoHome()
    {
        Navigation.NavigateTo("/");
    }

    public override void Dispose()
    {
        TeamState.OnTeamsChanged -= OnTeamsChanged;

        base.Dispose();
    }
}
