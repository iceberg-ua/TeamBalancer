# ⚽ TeamBalancer

A cross-platform application that automatically divides football (soccer) players into balanced teams based on their skill levels. Built with .NET 9 and .NET MAUI, TeamBalancer ensures fair and competitive matches by intelligently analyzing player abilities across multiple attributes.

![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20macOS%20%7C%20iOS%20%7C%20Android-blue)
![.NET](https://img.shields.io/badge/.NET-9.0-purple)
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

## 🏗️ Architecture

TeamBalancer follows **Clean Architecture** principles with clear separation of concerns:

```
TeamBalancer/
├── TeamBalancer.Core/              # Business logic (platform-agnostic)
│   ├── Models/                     # Domain models (Player, Team)
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
    └── Services/                  # UI-specific services
```

### Key Design Patterns
- **Strategy Pattern** - `ITeamBalancingStrategy` keeps the balancing algorithm swappable
  (currently one implementation, `DraftStrategy`)
- **Repository Pattern** - Abstract data persistence
- **Dependency Injection** - Loose coupling and testability
- **Clean Architecture** - Domain logic independent of UI and infrastructure