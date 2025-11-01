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

## 🚀 Getting Started

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [.NET MAUI workload](https://learn.microsoft.com/en-us/dotnet/maui/get-started/installation)

For platform-specific development:
- **Windows**: Visual Studio 2022 17.8+ with MAUI workload
- **macOS**: Visual Studio for Mac or VS Code with C# extension
- **iOS/Android**: Xcode (macOS) or Android SDK

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/teambalancer.git
   cd teambalancer
   ```

2. **Install .NET MAUI workload** (if not already installed)
   ```bash
   dotnet workload install maui
   ```

3. **Restore dependencies**
   ```bash
   dotnet restore
   ```

4. **Build the solution**
   ```bash
   dotnet build
   ```

### Running the Application

#### Windows
```bash
dotnet build -t:Run -f net9.0-windows10.0.19041.0
```

#### macOS
```bash
dotnet build -t:Run -f net9.0-maccatalyst
```

#### Android
```bash
dotnet build -t:Run -f net9.0-android
```

#### iOS
```bash
dotnet build -t:Run -f net9.0-ios
```

## 📖 Usage

### Adding Players

1. Navigate to the **Add Player** page
2. Enter player name (max 100 characters)
3. Rate each skill attribute (1-3 scale):
   - 1 = Low
   - 2 = Medium
   - 3 = High
4. Click **Add Player**

**Name Validation Rules:**
- Cannot be empty or whitespace
- Cannot contain special characters: `,` `"` `\n` `\r`
- Cannot start with formula characters: `=` `+` `-` `@`
- Maximum length: 100 characters

### Creating Balanced Teams

1. Go to **Player List** page
2. Select players to include in team balancing
3. Choose number of teams (2-N)
4. Select balancing algorithm:
   - **Snake Draft** - Fast, consistent results
   - **Iterative Swap** - Slower, more balanced
5. Optional: Enable **Shuffle** for variety
6. Click **Balance Teams**

### Importing Players from CSV

CSV format:
```csv
Name,Speed,TechnicalSkills,Stamina
John Doe,3,2,3
Jane Smith,2,3,2
Alex Johnson,3,3,1
```

1. Click the **Import** (📥) button on home page
2. Select your CSV file (max 1MB)
3. Review import results
4. Invalid rows are skipped and logged

### Exporting Players to CSV

1. Click the **Export** (📤) button on home page
2. Save the generated CSV file
3. File includes all active players

## 🔧 Configuration

### Algorithm Parameters

Balancing algorithm weights are configured in `BaseTeamBalancingStrategy.cs`:

```csharp
protected const double OverallSkillWeight = 2.0;    // Overall skill importance
protected const double PlayerCountWeight = 1.5;     // Team size importance
```

Iterative swap parameters in `IterativeSwapStrategy.cs`:

```csharp
private const int MaxIterations = 1000;             // Maximum optimization loops
private const double ImprovementThreshold = 0.0001; // Minimum improvement to continue
```

### Data Storage

Player data is stored in CSV format at:
- **Windows**: `%LOCALAPPDATA%\Packages\...\LocalCache\Local\players.csv`
- **macOS**: `~/Library/Containers/.../Data/players.csv`
- **iOS**: App sandbox container
- **Android**: `/data/data/com.companyname.teambalancer.desktop/files/players.csv`

## 🧪 Testing

### Manual Testing

See [CSV_INJECTION_TEST.md](CSV_INJECTION_TEST.md) for comprehensive test cases covering:
- CSV injection prevention
- Name validation
- Import/export functionality

### Unit Testing (Coming Soon)

```bash
dotnet test
```

## 🔒 Security

TeamBalancer implements multiple security layers:

### CSV Injection Prevention
- ✅ Input validation blocks formula characters (`=+-@`)
- ✅ Output sanitization escapes dangerous values
- ✅ Defense-in-depth approach
- ✅ Complies with OWASP guidelines

### Input Validation
- ✅ Comprehensive name validation
- ✅ Skill level range checking (1-3)
- ✅ Length limits to prevent DoS
- ✅ Detailed error messages

### Error Handling
- ✅ Structured logging with Microsoft.Extensions.Logging
- ✅ Specific exception types for debugging
- ✅ Graceful degradation for invalid data
- ✅ No silent failures

## 🛠️ Technology Stack

| Component | Technology | Version |
|-----------|-----------|---------|
| Framework | .NET | 9.0 |
| UI | .NET MAUI + Blazor | Latest |
| Language | C# | 12.0 |
| Platform | Cross-platform | Windows, macOS, iOS, Android |
| Storage | CSV Files | RFC 4180 |
| Logging | Microsoft.Extensions.Logging | 9.0.0 |

## 📊 Algorithm Details

### Snake Draft Strategy

**Time Complexity**: O(n log n) - due to sorting
**Space Complexity**: O(n)

**How it works:**
1. Sort players by overall skill level (descending)
2. Optional: Shuffle within skill tiers for variety
3. Distribute using snake pattern:
   - Round 1: Team A → Team B → Team C
   - Round 2: Team C → Team B → Team A
   - Round 3: Team A → Team B → Team C
   - (continues alternating)

**Best for**: Quick balancing with consistent results

### Iterative Swap Strategy

**Time Complexity**: O(n² × m² × k) where n=teams, m=players/team, k=iterations
**Space Complexity**: O(n × m)

**How it works:**
1. Initial round-robin distribution
2. Calculate baseline balance score
3. For each iteration:
   - Try swapping every pair of players between teams
   - Keep swap if it improves balance score
   - Repeat until no improvement or max iterations
4. Return optimized teams

**Best for**: Maximum balance quality (slower)

### Balance Score Calculation

```
Score = (OverallVariance × 2.0) +
        SpeedVariance +
        TechnicalVariance +
        StaminaVariance +
        (PlayerCountVariance × 1.5)
```

**Lower score = better balance** (0 = perfect)

## 🤝 Contributing

Contributions are welcome! Areas for improvement:

### High Priority
- [ ] Add unit tests for balancing algorithms
- [ ] Add unit tests for CSV parsing and security
- [ ] Implement accessibility features (ARIA, keyboard navigation)
- [ ] Add player editing functionality

### Medium Priority
- [ ] Optimize iterative swap algorithm performance
- [ ] Add player search and filtering
- [ ] Implement undo/redo for team generation
- [ ] Add team configuration save/load

### Nice to Have
- [ ] Dark mode support
- [ ] Multiple language support (i18n)
- [ ] Player statistics and history
- [ ] Team performance tracking
- [ ] Export to PDF/Excel

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [.NET MAUI](https://dotnet.microsoft.com/apps/maui) - Cross-platform framework
- [Blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor) - UI framework
- [Font Awesome](https://fontawesome.com/) - Icons (via CDN)
- [OWASP](https://owasp.org/) - Security best practices

## 📞 Support

For issues, questions, or suggestions:
- 🐛 [Report a bug](https://github.com/yourusername/teambalancer/issues)
- 💡 [Request a feature](https://github.com/yourusername/teambalancer/issues)
- 📧 Email: your.email@example.com

## 📝 Changelog

### Recent Updates

**2025-01-XX** - Security & Code Quality Improvements
- ✅ Added comprehensive CSV injection prevention
- ✅ Implemented structured logging with detailed error messages
- ✅ Refactored balancing strategies to eliminate code duplication
- ✅ Enhanced input validation with clear error messages
- ✅ Added defense-in-depth security measures

**Previous Updates**
- Implemented CSV import/export functionality
- Added player deletion with confirmation
- Replaced futbol icons with star ratings
- Added player name validation

---

**Made with ⚽ and ❤️ using .NET MAUI**
