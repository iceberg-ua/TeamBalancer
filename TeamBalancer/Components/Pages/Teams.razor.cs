using Microsoft.AspNetCore.Components;
using TeamBalancer.Core.Models;
using TeamBalancer.Core.Services.Balancing;
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
