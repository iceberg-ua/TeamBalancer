# Changelog

Notable changes to Team Balancer, newest first.

## 1.2 — 2026-07-28

### Positions

- Players can now be assigned a primary position on the field, plus an optional
  secondary position.
- Team balancing now takes goalkeeper coverage and position spread into account
  when generating teams.
- The player list and the generated team views show position information.
- Players added before this update show no position until you edit them.

### Balancing

- Rebuilt the balancing algorithm. It now drafts the teams by position group and
  then refines that draft by trying swaps, instead of picking between two
  separate methods that each did half the job.
- Teams are compared on their combined strength rather than their average
  player. When the squads come out uneven — an odd turnout, or a 3-a-side
  against a 4 — the smaller side is now given the stronger players to make up
  for being a player down.
- A player's secondary position is used to fill a gap when there are not enough
  players for a position to give every team one.

### Appearance

- New app icon.
- Fixed the green flash on every cold start: the launch screen now matches the
  app's own dark palette instead of the old brand green.
- The team roster is sorted down the pitch — goalkeeper, defenders, midfielders,
  forwards, then anyone without a position — strongest first within each group.
- The tabs, balance comparison and position counts on the Teams screen stay put
  while the roster scrolls.
- Long lists fade at the edges to show there is more to scroll to.

### Fixes

- The back button on the Teams screen returns to Select Players instead of
  jumping home and dropping the roster you had picked.
- A star rating no longer stays dimmed after you tap it on a touchscreen.
- Short lists no longer show a shadow over the last rows when there is nothing
  to scroll.
