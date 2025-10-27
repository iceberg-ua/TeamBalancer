namespace TeamBalancer.Core.Services.Balancing;

/// <summary>
/// Defines the available team balancing algorithm types.
/// </summary>
public enum BalancingAlgorithmType
{
    /// <summary>
    /// Snake draft algorithm - fast, simple, good for most cases.
    /// Players are sorted by skill and distributed in alternating pattern.
    /// </summary>
    SnakeDraft,

    /// <summary>
    /// Iterative swap algorithm - slower but produces better balance.
    /// Starts with initial distribution and swaps players to minimize skill variance.
    /// </summary>
    IterativeSwap
}
