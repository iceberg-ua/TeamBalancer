using TeamBalancer.Core.Models;

namespace TeamBalancer.Desktop.Services;

/// <summary>
/// Service for managing team state across pages.
/// </summary>
public class TeamStateService
{
    private List<Team>? _currentTeams;

    /// <summary>
    /// Event raised when teams are updated.
    /// </summary>
    public event Action? OnTeamsChanged;

    /// <summary>
    /// Gets the current teams.
    /// </summary>
    public List<Team>? CurrentTeams => _currentTeams;

    /// <summary>
    /// Sets the current teams and notifies subscribers.
    /// </summary>
    public void SetTeams(List<Team> teams)
    {
        _currentTeams = teams;
        OnTeamsChanged?.Invoke();
    }

    /// <summary>
    /// Clears the current teams.
    /// </summary>
    public void ClearTeams()
    {
        _currentTeams = null;
        OnTeamsChanged?.Invoke();
    }
}
