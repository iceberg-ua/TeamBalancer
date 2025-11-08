using Microsoft.AspNetCore.Components;
using TeamBalancer.Core.Models;
using TeamBalancer.Desktop.Services;

namespace TeamBalancer.Desktop.Components.Pages;

public partial class Teams : IDisposable
{
    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private TeamStateService TeamState { get; set; } = default!;

    public List<Team>? GeneratedTeams { get; set; }

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

        // Shuffle the players randomly
        var random = new Random();
        var shuffledPlayers = allPlayers.OrderBy(x => random.Next()).ToList();

        // Redistribute players into teams
        var newTeams = new List<Team>();
        for (int i = 0; i < numberOfTeams; i++)
        {
            newTeams.Add(new Team { Name = $"Team {i + 1}", Players = new List<Player>() });
        }

        // Distribute players evenly across teams
        for (int i = 0; i < shuffledPlayers.Count; i++)
        {
            var teamIndex = i % numberOfTeams;
            newTeams[teamIndex].Players.Add(shuffledPlayers[i]);
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
