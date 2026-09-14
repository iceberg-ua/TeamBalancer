# ⚽ TeamBalancer

A cross-platform application that automatically divides football (soccer) players into balanced teams based on their skill levels. Built with .NET 10 and .NET MAUI, TeamBalancer ensures fair and competitive matches by intelligently analyzing player abilities across multiple attributes.

![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20iOS%20%7C%20Android-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![License](https://img.shields.io/badge/license-MIT-green)

## ✨ Features

### Player Management
- 👥 Add, view, and delete players
- ⭐ Multi-attribute skill rating system (1-3 scale):
  - **Speed** - Player's pace and agility
  - **Technical Skills** - Ball control, passing, and dribbling
  - **Stamina** - Endurance and fitness level
- 📊 Overall skill level automatically calculated from attributes
- 💾 Persistent storage with CSV file format

### Player Lists
- 📋 Named lists, created, renamed, deleted and switched from the home header
- 🗂️ Each list keeps its own CSV file; the screens that deal in players see only the active one
- ⬆️ An install upgrading from a single-list build has its squad migrated into a list, keeping
  the existing `players.csv` rather than moving it

### Team Balancing
- 🐍 **Draft Strategy** - the single balancing algorithm, run in two phases:
  - **Seed** - a position-grouped snake draft (goalkeepers, defenders, midfielders, forwards,
    then everyone else), strongest first within each group, with the pick order carried across
    groups instead of restarting
  - **Refine** - a bounded pass of pairwise player swaps that keeps only the swaps which
    measurably improve the balance score
- 🥅 Goalkeeper handling - one keeper per team is a hard cap and a best-effort floor: surplus
  keepers are drafted as outfield players, and if there are fewer keepers than teams the extra
  teams simply go without one rather than the balancer failing
- 🔀 Secondary positions - used as fallback fill when a position group cannot cover every team;
  a player's primary position always takes precedence
- 📈 Balance scoring based on:
  - Overall team skill variance
  - Individual attribute distribution (Speed, Technical, Stamina)
  - Team size equality
  - Position imbalance across teams
- 🎲 Optional shuffle mode for variety while maintaining balance

See [TeamBalancingAlgorithms.md](TeamBalancer.Core/TeamBalancingAlgorithms.md) for the full
description of the algorithm and the alternatives that were considered.

### Data Management
- 📥 **CSV Import** - Bulk import players from CSV files
- 📤 **CSV Export** - Export player list for external use
- 🔒 **CSV Injection Prevention** - Comprehensive security validation
- ✅ Input validation with detailed error messages

### Sharing
- 📱 **QR code sharing** - the active list is drawn as a QR code on one phone and read
  with the camera on another, with no network between them
- 🧾 The code carries the same CSV the export writes, so a squad sent as a code and a squad
  sent as a file arrive as the same thing, through one parser
- 🗜️ Payload is the list name and CSV, deflated and base32-encoded behind a `TB1:` marker;
  base32 keeps it inside the QR alphanumeric set, which stores 5.5 bits per character where
  byte mode stores 8. The marker carries a format version, and decompression is capped
- 📏 Above 2,200 characters - near 180 players - the code is not drawn at all, since a symbol
  that dense mostly fails to scan; the app asks for the file route instead
- 🎯 Every import asks where the players land: a new list, named after the sender's, or a
  merge into an existing one that adds newcomers, takes the sender's ratings for players
  already there, and removes nobody

### Matches and History
- ✅ **Match recording** - accepting a split opens a Match screen that records the score, the
  scorers and their assists while the game is played; the score counts the named goals, or
  holds a figure set by hand - typed in, or tapped up with plus - until more goals are named
  than it says
- 🎯 One assist per goal at most, so a side can never be credited with more assists than it
  scored; goals with nobody named for them still count and are shown as unattributed
- ➕ Late arrivals join either side, from the list or as someone new, without a rebalance; a
  player moved across takes their goals with them
- 💾 Finished matches are appended to `matches.csv` beside the player files, one row per player
  per match, with the match and side columns repeated on each row. The player's name is
  stored with the row, so renaming or removing someone does not rewrite a game already played
- 📜 **History** - the active list's matches, newest first; opening one shows both line-ups as
  they stood at the final whistle and what each player scored and set up
- 🛡️ The reader drops a damaged row rather than the file, and drops a match left with fewer
  than two sides; an install that has never finished a match simply has no file yet

### Languages
- 🌍 English, German and Ukrainian, switchable from the home screen without restarting
- 📱 Starts in the device's language when it is one of the three, and falls back to English
  otherwise; the choice is remembered on the device
- 📝 Translations live in plain JSON files under `TeamBalancer/Resources/Languages/`, one per
  language, keyed by flat dot-notation strings such as `playerList.title`

## 🏗️ Architecture

TeamBalancer follows **Clean Architecture** principles with clear separation of concerns:

```
TeamBalancer/
├── TeamBalancer.Core/              # Business logic (platform-agnostic)
│   ├── Models/                     # Domain models (Player, Team)
│   ├── Localization/              # Translation lookup and fallback
│   ├── Services/
│   │   ├── Balancing/             # Team balancing
│   │   │   ├── BaseTeamBalancingStrategy.cs   # Scoring + swap refinement
│   │   │   └── DraftStrategy.cs               # Seeding draft + refinement
│   │   ├── Csv/                   # CSV parsing and persistence
│   │   └── Interfaces/            # Service abstractions
│   └── Exceptions/                # Custom exception types
│
└── TeamBalancer.Desktop/          # .NET MAUI Blazor Hybrid UI
    ├── Components/
    │   ├── Pages/                 # Main application pages
    │   └── Shared/                # Reusable UI components
    ├── Localization/              # Platform side of localization
    ├── Resources/Languages/       # en.json, de.json, uk.json
    └── Services/                  # UI-specific services
```

### Key Design Patterns
- **Strategy Pattern** - `ITeamBalancingStrategy` keeps the balancing algorithm swappable
  (currently one implementation, `DraftStrategy`)
- **Repository Pattern** - Abstract data persistence
- **Dependency Injection** - Loose coupling and testability
- **Clean Architecture** - Domain logic independent of UI and infrastructure