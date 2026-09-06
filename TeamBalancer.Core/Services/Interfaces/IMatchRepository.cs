namespace TeamBalancer.Core.Services.Interfaces;

using TeamBalancer.Core.Models;

/// <summary>
/// Defines how finished matches are stored and read back.
/// </summary>
public interface IMatchRepository
{
    /// <summary>
    /// Stores a finished match. Matches are only ever added, never changed, which is what
    /// makes finishing a game one cheap write however many have been played before it.
    /// </summary>
    /// <param name="match">The match to store.</param>
    Task AppendAsync(MatchRecord match);

    /// <summary>
    /// Reads back every match ever finished, most recent first.
    /// </summary>
    /// <remarks>
    /// Everything is returned rather than the caller's list alone: filtering is a decision for
    /// the screen, and a repository that could only answer for one list would have to be asked
    /// again for every other. Storage is one flat file, so reading all of it is what reading
    /// any of it costs anyway.
    /// </remarks>
    /// <returns>The finished matches, newest first; empty when none have been played.</returns>
    Task<IReadOnlyList<FinishedMatch>> GetAllAsync();
}
