using TeamBalancer.Core.Models;

namespace TeamBalancer.Core.Services.Balancing;

/// <summary>
/// Implements a two-phase team balancing strategy: a constructive position-group snake draft
/// that seeds the teams, followed by a bounded pairwise-swap refinement of that seed.
/// <para>
/// Phase A seeds one goalkeeper per team where supply allows, then drafts the rest of the pool
/// group by group - defenders, midfielders, forwards, then everyone left - strongest first
/// within each group and in snake order, carrying the pick cursor across groups. A player's
/// <see cref="Player.SecondaryPosition"/> acts as a fill signal only: it can pull a player out
/// of the leftover pool into a group that is short on primaries, but never outranks a primary
/// match. Phase B then hands the seeded teams to
/// <see cref="BaseTeamBalancingStrategy.ImproveByPairwiseSwaps"/>.
/// </para>
/// </summary>
/// <remarks>
/// Secondary position is a seeding signal only - <see cref="CalculateBalanceScore"/> and
/// <see cref="CalculatePositionImbalance"/> still score primary positions alone, per the
/// "secondary position stays low-weight for now" decision. Phase B therefore cannot see the
/// difference between a player filling a group on his secondary position and any other player.
/// </remarks>
public class DraftStrategy : BaseTeamBalancingStrategy
{
    private readonly Random _random = new();

    /// <summary>
    /// The outfield groups in draft order. Goalkeepers are seeded before these and the
    /// leftover pool is drafted after them, so neither appears here.
    /// </summary>
    private static readonly Position[] OutfieldDraftOrder =
    [
        Position.Defender,
        Position.Midfielder,
        Position.Forward
    ];

    /// <summary>
    /// Balances players by seeding a position-group snake draft and then refining it with
    /// pairwise swaps.
    /// </summary>
    /// <param name="players">The list of players to balance.</param>
    /// <param name="numberOfTeams">The number of teams to create.</param>
    /// <param name="shuffle">
    /// If true, players are shuffled within skill tiers while seeding and the refinement
    /// visits swaps in random order, adding variety without giving up balance.
    /// </param>
    public override List<Team> BalanceTeams(List<Player> players, int numberOfTeams, bool shuffle = false)
    {
        if (players == null || players.Count == 0)
        {
            throw new ArgumentException("Player list cannot be null or empty.", nameof(players));
        }

        if (numberOfTeams < 2)
        {
            throw new ArgumentException("Number of teams must be at least 2.", nameof(numberOfTeams));
        }

        // Phase A: constructive seeding.
        var teams = SeedTeams(players, numberOfTeams, shuffle);

        // Phase B: bounded refinement of that seed.
        ImproveByPairwiseSwaps(teams, shuffle ? _random : null);

        return teams;
    }

    /// <summary>
    /// Phase A on its own: builds the teams and fills them with a position-group snake draft -
    /// keepers first, then each outfield group, then whoever is left. Exposed to derived
    /// classes so the seeding can be exercised without the refinement pass on top of it.
    /// </summary>
    /// <param name="players">The list of players to seed with.</param>
    /// <param name="numberOfTeams">The number of teams to create.</param>
    /// <param name="shuffle">Whether to shuffle players within skill tiers while seeding.</param>
    /// <returns>The seeded, unrefined teams.</returns>
    protected List<Team> SeedTeams(List<Player> players, int numberOfTeams, bool shuffle)
    {
        var teams = new List<Team>();

        for (int i = 0; i < numberOfTeams; i++)
        {
            teams.Add(new Team
            {
                Name = $"Team {(char)('A' + i)}"
            });
        }

        // Exactly one keeper per team - five- and seven-a-side sides only field one. When
        // fewer goalkeepers than teams exist the draft starts at team index 0 moving forward,
        // so each available keeper lands on a different team and the remaining teams simply go
        // without one. A missing keeper is never an error.
        var goalkeepers = SortForDraft(players.Where(p => p.PrimaryPosition == Position.Goalkeeper), shuffle);
        var keepers = goalkeepers.Take(numberOfTeams).ToList();

        // Everyone with no primary group of their own, plus the surplus keepers, who are
        // ordinary outfield-eligible players from here on. Groups short of primaries draw
        // their fill from this pool, and whatever survives is drafted last.
        var leftovers = players
            .Where(p => p.PrimaryPosition == Position.Unspecified)
            .Concat(goalkeepers.Skip(numberOfTeams))
            .ToList();

        var groups = new List<List<Player>>();

        foreach (var position in OutfieldDraftOrder)
        {
            groups.Add(BuildGroup(players, position, numberOfTeams, leftovers, shuffle));
        }

        // The snake cursor carries across position groups on purpose. Resetting it per group
        // would hand the first team the strongest player of every position.
        int currentTeamIndex = 0;
        int direction = 1;

        DraftGroup(teams, keepers, ref currentTeamIndex, ref direction);

        foreach (var group in groups)
        {
            DraftGroup(teams, group, ref currentTeamIndex, ref direction);
        }

        DraftGroup(teams, SortForDraft(leftovers, shuffle), ref currentTeamIndex, ref direction);

        return teams;
    }

    /// <summary>
    /// Builds one outfield group: every player whose primary position matches, strongest
    /// first, followed by secondary-position fill if the group cannot cover a team each.
    /// Fillers are taken out of <paramref name="leftovers"/> so they are not drafted twice.
    /// </summary>
    /// <remarks>
    /// Primaries are always ordered ahead of fillers, so no filler can take a pick away from a
    /// player who plays the position for real. Note that promoting a filler out of the
    /// leftover pool does move it earlier in the overall draft, which shifts the snake cursor
    /// for the groups after it - the pick sequence itself is unchanged, but a later group's
    /// players can land on different teams than they would have with no fill at all.
    /// </remarks>
    private List<Player> BuildGroup(
        List<Player> players,
        Position position,
        int numberOfTeams,
        List<Player> leftovers,
        bool shuffle)
    {
        var primaries = SortForDraft(players.Where(p => p.PrimaryPosition == position), shuffle);

        // A group is short when it cannot give every team one of its own.
        int shortfall = numberOfTeams - primaries.Count;

        if (shortfall <= 0)
        {
            return primaries;
        }

        var fillers = SortForDraft(leftovers.Where(p => p.SecondaryPosition == position), shuffle)
            .Take(shortfall)
            .ToList();

        var promoted = new HashSet<Player>(fillers);
        leftovers.RemoveAll(promoted.Contains);

        return [.. primaries, .. fillers];
    }

    /// <summary>
    /// Sorts a position group by skill for drafting, applying tier shuffling when requested.
    /// </summary>
    private List<Player> SortForDraft(IEnumerable<Player> players, bool shuffle)
    {
        var sorted = players
            .OrderByDescending(p => p.OverallSkillLevel)
            .ThenByDescending(p => p.Speed)
            .ThenByDescending(p => p.TechnicalSkills)
            .ThenByDescending(p => p.Stamina)
            .ToList();

        return shuffle ? ShuffleWithinTiers(sorted) : sorted;
    }

    /// <summary>
    /// Distributes one group of players across the teams using the snake draft pattern:
    /// A, B, B, A, A, B... (each team picks once, then direction reverses). The cursor is
    /// passed by reference so successive groups continue the pattern rather than restarting.
    /// </summary>
    private static void DraftGroup(List<Team> teams, List<Player> group, ref int currentTeamIndex, ref int direction)
    {
        foreach (var player in group)
        {
            teams[currentTeamIndex].AddPlayer(player);

            // Calculate next team index
            int nextIndex = currentTeamIndex + direction;

            // If next index is out of bounds, reverse direction (team picks again at the turn)
            if (nextIndex >= teams.Count || nextIndex < 0)
            {
                direction = -direction;
            }
            else
            {
                currentTeamIndex = nextIndex;
            }
        }
    }

    /// <summary>
    /// Shuffles players within skill tiers to add variety while maintaining balance.
    /// Groups players by similar skill levels and randomizes within each group.
    /// </summary>
    private List<Player> ShuffleWithinTiers(List<Player> sortedPlayers)
    {
        var result = new List<Player>();

        // Define tier size - group players into tiers of similar skill
        // For example, every 2-4 players of similar skill are shuffled together
        int tierSize = Math.Max(2, sortedPlayers.Count / 6); // Adjust tier size based on player count

        for (int i = 0; i < sortedPlayers.Count; i += tierSize)
        {
            // Get players in this tier
            var tier = sortedPlayers
                .Skip(i)
                .Take(tierSize)
                .OrderBy(_ => _random.Next()) // Shuffle within tier
                .ToList();

            result.AddRange(tier);
        }

        return result;
    }
}
