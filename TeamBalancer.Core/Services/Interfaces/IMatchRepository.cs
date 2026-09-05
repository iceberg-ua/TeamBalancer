namespace TeamBalancer.Core.Services.Interfaces;

using TeamBalancer.Core.Models;

/// <summary>
/// Defines how finished matches are stored. Only writing is defined here: nothing in the app
/// reads matches back yet, and the screen that will browse them is a separate piece of work.
/// </summary>
public interface IMatchRepository
{
    /// <summary>
    /// Stores a finished match. Matches are only ever added, never changed, so this is the
    /// whole of the contract.
    /// </summary>
    /// <param name="match">The match to store.</param>
    Task AppendAsync(MatchRecord match);
}
