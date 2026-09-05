# Changelog

Notable changes to Team Balancer, newest first.

## Unreleased

### Matches

- A split you are happy with can now be accepted. Accepting takes you to a new
  Match screen, where the game is recorded while it is being played: the score,
  who scored, and who set them up.
- The score works either way round. Leave it alone and it counts the goals as
  you name the scorers. Enter it yourself and it stays put while you catch up on
  who scored — until you name more goals than the figure you entered, at which
  point it follows the scorers. Goals nobody is named for are still part of the
  score, and the screen says how many there are.
- A goal carries at most one assist, so a side can never be credited with more
  assists than it scored. Once every goal on the board has someone down for it,
  the assist button says to add the goal first.
- Players who turn up after kick-off can be added to either side, from the list
  or as somebody new. Nothing is rebalanced around them, and they can score and
  assist like anyone else. Someone put on the wrong side can still be sent over,
  and their goals go with them.
- Finishing the match saves the result. It is written to a file of its own, so a
  game you played survives closing the app. Leaving any other way asks first.
- Reshuffle keeps its place in the footer next to the new Accept button, as its
  own shuffle glyph.

## 2.1 — 2026-08-20

### Sharing

- A squad now travels from one phone to another without a network: show the
  active list as a QR code on one phone, scan it from the other. The camera
  keeps trying until it reads, so there is nothing to line up exactly.
- What arrives is the same squad a shared file carries — the list name and every
  player with their positions and ratings — so it makes no difference which way
  you sent it.
- A squad too big to fit a readable code says so, and asks you to send it as a
  file instead. That is somewhere around 180 players.
- Scanning asks for the camera the first time. Nothing else was added: the app
  does not ask for your photos or your files.

### Import

- Every import now asks where the players should go, whether they came from a
  file or from a code. Before, they went into whichever list happened to be
  open.
- "Into a new list" suggests the sender's list name. "Into a list you already
  have" merges.
- Merging means what you would expect. Players you do not have are added,
  ratings you receive replace the ones you have, and players missing from the
  import are kept — someone not being in the squad you were sent is not the same
  as them having left. Who you had picked for tonight is left alone.

### Fixes

- Importing into a list you had just created could leave that list empty. The
  list appeared, correctly named, with nobody in it.

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
