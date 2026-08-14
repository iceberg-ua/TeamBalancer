# Changelog

Notable changes to Team Balancer, newest first.

## 2.0 — 2026-08-14

### Languages

- The app speaks English, German and Ukrainian. Switch language from the home
  screen at any time — nothing needs restarting.
- It starts in your device's language when that is one of the three, and in
  English otherwise. Your choice is remembered.

### Player lists

- You can keep more than one squad. Create, rename, delete and switch lists from
  the home header; each keeps its own players.
- Your existing players are moved into a list for you on first launch, so
  nothing is lost and nothing needs setting up.

### Home and Select Players

- Home now shows the squad instead of a pitch graphic: the position counts
  across the top, then every player with their position, fallback position and
  rating. Tap a player to edit them.
- Select Players was rebuilt to match. The whole row is the tap target, a filled
  check marks who is coming, and the header carries the list you are picking
  from, its size and a running count of who is picked.
- Positions are shown the same way on both screens, so nothing changes meaning
  as you move between them.
- The squad size sits next to the list name on Home, as it already did on Select
  Players.

### Teams

- The scoreboard at the top is now the team switcher. Each half carries that
  team's name, overall rating and head count; tap a half to see its lineup. The
  separate tab bar and the legend below are gone.
- The header says how the draw came out — Balanced, or which side it leans to
  and by how much.
- The lineup reads like the other screens: position, name, rating, tinted to the
  team you are looking at.
- A player can be moved to the other side from their row when there are two
  teams. Nothing is rebalanced around them — the figures just update.
- Leaving the screen or reshuffling now asks first, because either one throws
  away the split you are looking at.

### Import

- Import says what it could not take. Names that were too long, ratings outside
  1–3 and players already in the list are each reported, instead of a file that
  quietly lost half its rows reading as a plain success.
- A file whose players are all in the list already says so, rather than
  reporting an error.
- The first row is no longer assumed to be a heading. A file that starts with a
  player — typed by hand, or saved out of a spreadsheet — keeps them, and a file
  with translated column titles is still read correctly.
- Names can be 20 characters, up from 15, and a name that is only too long is
  shortened and imported rather than dropped.

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
