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

### Team Balancing Algorithms
- 🐍 **Snake Draft Strategy** - Greedy algorithm with optional tier-based shuffling
- 🔄 **Iterative Swap Strategy** - Advanced optimization through player swapping
- 📈 Balance scoring based on:
  - Overall team skill variance
  - Individual attribute distribution (Speed, Technical, Stamina)
  - Team size equality
- 🎲 Optional shuffle mode for variety while maintaining balance

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
│   │   ├── Balancing/             # Team balancing strategies
│   │   │   ├── BaseTeamBalancingStrategy.cs
│   │   │   ├── SnakeDraftStrategy.cs
│   │   │   └── IterativeSwapStrategy.cs
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
- **Strategy Pattern** - Pluggable team balancing algorithms
- **Repository Pattern** - Abstract data persistence
- **Dependency Injection** - Loose coupling and testability
- **Clean Architecture** - Domain logic independent of UI and infrastructure