using Microsoft.AspNetCore.Components;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Balancing;
using TeamBalancer.Extensions;
using TeamBalancer.Services;

namespace TeamBalancer.Components.Pages;

public partial class Teams : IDisposable
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

        // Rename teams to match current naming (Team 1, Team 2, etc.)
        for (int i = 0; i < newTeams.Count; i++)
        {
            newTeams[i].Name = $"Team {i + 1}";
        }

        // Update the state service with new teams
        TeamState.SetTeams(newTeams);
    }

    /// <summary>
    /// A single position count shown in a team's position summary.
    /// </summary>
    private sealed record PositionSummaryEntry(string Label, string CssClass, int Count, string Title);

    /// <summary>
    /// Counts a team's players by primary position. Every real position is returned even when
    /// its count is zero - a missing goalkeeper is exactly what this summary should reveal.
    /// Players without a position land in a separate "Unset" entry, which is only shown when
    /// it is non-empty.
    /// </summary>
    private static IEnumerable<PositionSummaryEntry> GetPositionSummary(Team team)
    {
        foreach (var position in PositionExtensions.SelectablePositions)
        {
            var count = team.Players.Count(p => p.PrimaryPosition == position);
            yield return new PositionSummaryEntry(
                position.ToAbbreviation(),
                position.ToBadgeClass(),
                count,
                $"{count} × {position.ToDisplayName()}");
        }

        var unsetCount = team.Players.Count(p => p.PrimaryPosition == Position.Unspecified);
        if (unsetCount > 0)
        {
            yield return new PositionSummaryEntry(
                "Unset",
                "pos-unset",
                unsetCount,
                $"{unsetCount} player(s) with no position set");
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

    public void Dispose()
    {
        TeamState.OnTeamsChanged -= OnTeamsChanged;
    }
}
