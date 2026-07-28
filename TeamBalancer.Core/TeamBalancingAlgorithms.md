# Team Balancing Algorithm

This document describes the balancing algorithm TeamBalancer implements, and records the
alternatives that were considered and rejected along the way.

There is one algorithm: **`DraftStrategy`**. It is registered as the app's
`ITeamBalancingStrategy` and is the only implementation. The earlier `SnakeDraftStrategy` and
`IterativeSwapStrategy` classes have been removed - `DraftStrategy` combines what each of them
did into a single two-phase run.

## Data Model Context

Players have three skill attributes (1-3 scale):
- Speed
- TechnicalSkills
- Stamina
- OverallSkillLevel (calculated as average of the three attributes)

Players also carry positions:
- **PrimaryPosition** - Goalkeeper, Defender, Midfielder, Forward, or Unspecified. Unspecified
  exists for data that predates position support; it is not a user-facing choice.
- **SecondaryPosition** - optional fallback position, or null.

Teams track:
- Player count
- Average for each skill attribute
- Overall team skill
- Total skill points

---

## The Algorithm

`DraftStrategy.BalanceTeams` runs two phases in order. Phase A builds a whole, valid
distribution; phase B improves it without ever breaking it.

### Phase A - Constructive seeding (position-grouped snake draft)

**1. Goalkeepers first.** Up to `numberOfTeams` players whose *primary* position is Goalkeeper
are taken, strongest first, and dealt one per team.

**2. Then each position group in turn** - Defenders, Midfielders, Forwards, and finally the
leftover pool - sorted strongest first within the group and dealt in snake order
(A, B, B, A, A, B...).

The pick cursor **carries across groups** rather than resetting for each one. Resetting it
would hand the first team the strongest player of every position in turn.

**3. Secondary positions fill shortfalls.** A group is *short* when it has fewer primary-tagged
players than there are teams, so it cannot give every team one. A short group tops itself up
from the leftover pool, taking the players whose *secondary* position matches, strongest first.

Two rules constrain this:
- A primary match always outranks a secondary match **within a group**, so a fill player can
  never take a pick away from someone who plays the position for real.
- Fill is drawn only from the leftover pool - players with no position group of their own, plus
  surplus goalkeepers. It never raids another outfield group, which would just move the
  shortage somewhere else.

Note the one visible side effect: promoting a leftover into an earlier group makes that group
one pick longer, which shifts the snake cursor for the groups drafted after it. The sequence of
picks is unchanged; who occupies the later slots can differ from a draft with no fill at all.

### Phase B - Bounded refinement (hill climbing on pairwise swaps)

The seeded teams are handed to `BaseTeamBalancingStrategy.ImproveByPairwiseSwaps`, which tries
swapping single players between every pair of teams. A swap is **kept** only when both hold:

1. It lowers the balance score by more than `ImprovementThreshold` (0.0001 - enough to ignore
   floating point noise), and
2. it does not increase the number of teams without a goalkeeper.

Anything else is reverted immediately. Each accepted swap restarts the search; the pass stops
when a full sweep finds no improvement, or after `MaxIterations` (1000) sweeps, so a
pathological pool cannot loop forever.

Because the refinement only ever accepts strict improvements, its result can never score worse
than the plain draft it started from - a property the test suite asserts directly.

### Goalkeepers: hard cap, best-effort floor

Goalkeeper coverage is handled as a **constraint, not a scored term**, in both phases:

- **At most one per team.** Only the first `numberOfTeams` keepers are treated as keepers;
  surplus keepers rejoin the pool as ordinary outfield-eligible players (and can fill a short
  group on their secondary position like any other leftover). Five- and seven-a-side sides only
  field one keeper, and this also stops the snake handing a team a second keeper while another
  has none.
- **As close to one each as supply allows.** If there are fewer keepers than teams, the
  available ones land on different teams and the rest go without. This is never an error and
  never blocks balancing.
- **Refinement can improve coverage but never worsen it.** A swap that would leave more teams
  keeper-less is rejected outright, regardless of how much it improves the score.

Because it is a constraint rather than a scored term, an uneven keeper spread does not on its
own change the balance score.

### Shuffle

`shuffle: true` adds variety without giving up balance:
- **Seeding** shuffles players within skill tiers before drafting, so near-equal players can
  swap places in the pick order but a weak player never jumps ahead of a strong one.
- **Refinement** visits team pairs and players in random order, so when several swaps are
  equally good the choice between them varies instead of always resolving to the lowest index.

With `shuffle: false` the whole run is deterministic.

---

## Balance Scoring

`CalculateBalanceScore` returns a weighted sum of variances across the teams. **Lower is
better**, 0 being perfect. All variances are population variances.

| Dimension | Weight | Notes |
|---|---|---|
| Team strength variance | 2.0 | Weighted highest - strength parity dominates |
| Speed variance | 1.0 | |
| Technical skills variance | 1.0 | |
| Stamina variance | 1.0 | |
| Player count variance | 1.5 | Keeps team sizes equal |
| Position imbalance | 1.0 | Low enough that skill still dominates, high enough to break near-ties |

All four skill terms are measured per roster slot - a team's total divided by the *mean* squad
size, not its own - so that a team fielding fewer players is not flattered by the division. See
[Uneven team sizes](#uneven-team-sizes) for why that matters.

**Strength is counted in attribute points**, the sum of a team's speed, technical and stamina
totals - not the mean of them, which is what `Player.OverallSkillLevel` returns. The two differ
only by a factor of three per team, but that factor squares into a ninth once it reaches the
variance, and the three attribute terms are then *summed* against it. Measured as a mean,
strength was worth roughly a twentieth of the spread terms it is supposed to outrank, and splits
with identical team totals lost to splits that merely spread stamina more evenly. Counting
attribute points puts strength in the same units as the terms it competes with, so the 2.0 reads
as what it claims: strength parity counts double any single attribute's spread.

**Position imbalance** is the sum, over Defender / Midfielder / Forward, of the variance in how
many players of that position each team holds. Two positions are deliberately excluded:

- **Goalkeeper**, because it is enforced as a hard constraint (above) rather than scored.
- **Unspecified**, because those players are treated as fully flexible.

A pool where nobody has a position set therefore scores exactly as it did before position
support existed.

### Uneven team sizes

An odd pool cannot be split evenly, and the count term keeps the sizes within one of each other
rather than papering over the gap. The side that ends up a player down is compensated in
quality: because strength is scored on team **totals**, a three-man side has to match a four-man
side's total, which it can only do by holding better players.

Seven players of ratings 3, 3, 2, 2, 2, 1, 1 split 3 v 4 as:

```
3-man team:  3  2  2   total 7.0   rating 2.33
4-man team:  3  2  1 1  total 7.0   rating 1.75
```

Equal totals is the target, not a starting point to be tilted further. A short-handed side is
not given a handicap on top - it is not owed *more* than parity - and the extra body is not
scored as strength in its own right. The rule is simply that the sum on each side matches, and
the smaller squad reaches that sum with fewer, better players.

Where the arithmetic allows no exact match - fifteen players might split no closer than 14.67
against 15.00, since a total moves in thirds - the score treats both ways of assigning the last
third as equal and either may come out. A tie-break biasing that third toward the short-handed
side was tried and dropped: it decided 8 pools in 520 at a weight small enough to be safe, and
at a weight large enough to matter it started overshooting parity into the handicap above.

The measured effect across 520 uneven-size pools of 5 to 15 players: no short-handed team is
weaker per player than a fuller one, and the mean gap between the strongest and weakest team's
totals fell by a quarter to a half depending on pool size. `DraftStrategyTests` pins both, the
latter as per-configuration ceilings.

Scoring reads **primary positions only**. A player filling a group on his secondary position
counts toward his primary position in the score, not the group he filled - secondary position
is a seeding signal for now, and deliberately carries no scoring weight.

---

## Implementation Structure

```csharp
namespace TeamBalancer.Core.Services.Balancing
{
    public interface ITeamBalancingStrategy
    {
        List<Team> BalanceTeams(List<Player> players, int numberOfTeams, bool shuffle = false);
        double CalculateBalanceScore(List<Team> teams);
    }

    // Shared scoring and the swap-refinement pass.
    public abstract class BaseTeamBalancingStrategy : ITeamBalancingStrategy { }

    // Phase A (seeding draft) + phase B (refinement). The only strategy.
    public class DraftStrategy : BaseTeamBalancingStrategy { }

    public class TeamBalancingService
    {
        public List<Team> BalanceTeams(
            List<Player> players,
            int numberOfTeams,
            ITeamBalancingStrategy strategy,
            bool shuffle = false);
    }
}
```

`ITeamBalancingStrategy` and the abstract base are kept as the seam for a future alternative
algorithm, even though only one implementation exists today.

---

## Considered and Rejected

Kept for the record. None of these are being pursued; the two that were actually built have
since been folded into `DraftStrategy` and deleted.

### Built, then superseded

**Snake Draft (`SnakeDraftStrategy`)** - the position-grouped snake draft on its own, with no
refinement. Fast and intuitive, but greedy: it never revisits a pick, so it settles for
whatever the draft order happens to produce. It survives as phase A of `DraftStrategy`.

**Iterative Swap (`IterativeSwapStrategy`)** - round-robin seeding followed by the same
hill-climbing swap pass. Better balance than pure greedy, but its quality depended heavily on a
weak initial distribution. It survives as phase B of `DraftStrategy`, now fed by a much better
seed.

Merging the two removed the need for users to choose an algorithm - a choice the UI never
actually exposed - and removed a second implementation that was drifting out of sync.

### Considered, never built

**Bin Packing / First-Fit** - sort by skill descending, assign each player to the team with the
lowest current total. Fast and simple, but it is another greedy method with no backtracking,
and it ignores positions entirely. The snake draft covers the same ground and spreads positions.

**Genetic Algorithm** - evolve a population of candidate splits against a fitness function.
Would likely find better optima and escape local ones, but it is slow, non-deterministic,
needs tuning (population size, mutation rate), and is hard to explain to a user staring at a
team sheet. Disproportionate for pools of 10-30 players.

**Constraint Programming / ILP** - formulate the split as a mathematical optimisation and solve
it exactly. Genuinely optimal and handles complex constraints naturally, but it requires an
external solver dependency (OR-Tools, CPLEX) in a MAUI app, and can be slow to scale. Not worth
the dependency for the quality gap involved.

**Multi-objective / Pareto optimisation** - balance each attribute as a separate objective
rather than folding them into one score. Partly adopted rather than rejected: the current
scoring is a weighted sum over all three attributes plus team size and position spread, which
captures most of the benefit without the complexity of a Pareto front or a min-max formulation.
