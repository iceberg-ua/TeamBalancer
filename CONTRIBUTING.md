# Contributing to TeamBalancer

Thank you for considering contributing to TeamBalancer! This document provides guidelines and instructions for contributing to the project.

## Table of Contents
- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Coding Standards](#coding-standards)
- [Pull Request Process](#pull-request-process)
- [Testing Guidelines](#testing-guidelines)
- [Architecture Guidelines](#architecture-guidelines)

## Code of Conduct

Be respectful, inclusive, and professional. We're all here to build better software together.

## Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally:
   ```bash
   git clone https://github.com/YOUR_USERNAME/teambalancer.git
   cd teambalancer
   ```
3. **Add upstream remote**:
   ```bash
   git remote add upstream https://github.com/ORIGINAL_OWNER/teambalancer.git
   ```
4. **Install dependencies**:
   ```bash
   dotnet restore
   ```
5. **Build the solution**:
   ```bash
   dotnet build
   ```

## Development Workflow

### Creating a Branch

Create a feature branch from `main`:
```bash
git checkout main
git pull upstream main
git checkout -b feature/your-feature-name
```

Branch naming conventions:
- `feature/description` - New features
- `fix/description` - Bug fixes
- `refactor/description` - Code refactoring
- `docs/description` - Documentation updates
- `test/description` - Test additions/improvements

### Making Changes

1. Make your changes in the appropriate project:
   - `TeamBalancer.Core` - Business logic, models, services
   - `TeamBalancer.Desktop` - UI components, pages

2. Follow the [Coding Standards](#coding-standards)

3. Test your changes thoroughly

4. Commit with clear, descriptive messages:
   ```bash
   git commit -m "Add CSV injection prevention to player name validation"
   ```

### Keeping Your Branch Updated

Regularly sync with upstream:
```bash
git fetch upstream
git rebase upstream/main
```

## Coding Standards

### C# Style Guidelines

- Follow [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful variable and method names
- Keep methods small and focused (Single Responsibility Principle)
- Maximum line length: 120 characters

### Code Organization

```csharp
// 1. Using statements (grouped and sorted)
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

// 2. Namespace
namespace TeamBalancer.Core.Services;

// 3. XML documentation
/// <summary>
/// Brief description of the class.
/// </summary>
public class MyService
{
    // 4. Private fields
    private readonly ILogger<MyService> _logger;

    // 5. Constants
    private const int MaxRetries = 3;

    // 6. Constructor
    public MyService(ILogger<MyService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // 7. Public methods
    public void DoSomething()
    {
        // Implementation
    }

    // 8. Private methods
    private void HelperMethod()
    {
        // Implementation
    }
}
```

### Naming Conventions

| Type | Convention | Example |
|------|-----------|---------|
| Class | PascalCase | `PlayerRepository` |
| Interface | IPascalCase | `IPlayerRepository` |
| Method | PascalCase | `BalanceTeams` |
| Property | PascalCase | `PlayerName` |
| Private field | _camelCase | `_playerRepository` |
| Parameter | camelCase | `numberOfTeams` |
| Local variable | camelCase | `balanceScore` |
| Constant | PascalCase | `MaxIterations` |

### XML Documentation

All public APIs must have XML documentation:

```csharp
/// <summary>
/// Balances players into the specified number of teams.
/// </summary>
/// <param name="players">The list of players to balance.</param>
/// <param name="numberOfTeams">The number of teams to create.</param>
/// <returns>A list of balanced teams.</returns>
/// <exception cref="ArgumentException">Thrown when player list is empty.</exception>
public List<Team> BalanceTeams(List<Player> players, int numberOfTeams)
{
    // Implementation
}
```

### Error Handling

- Use specific exception types
- Always log errors with context
- Provide meaningful error messages
- No empty catch blocks (use logging)

```csharp
// ❌ Bad
try
{
    // code
}
catch
{
    // Silent failure
}

// ✅ Good
try
{
    // code
}
catch (FormatException ex)
{
    _logger.LogError(ex, "Failed to parse value: {Value}", value);
    throw new CsvParseException("Invalid format", ex);
}
```

### Dependency Injection

- Always use constructor injection
- Validate dependencies are not null
- Prefer interfaces over concrete types

```csharp
public class MyService
{
    private readonly IPlayerRepository _repository;
    private readonly ILogger<MyService> _logger;

    public MyService(IPlayerRepository repository, ILogger<MyService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
```

## Pull Request Process

### Before Submitting

1. ✅ Code builds without errors or warnings
2. ✅ All tests pass (once tests are added)
3. ✅ Code follows style guidelines
4. ✅ XML documentation is complete
5. ✅ Commit messages are clear and descriptive
6. ✅ Branch is up to date with `main`

### Submitting a Pull Request

1. **Push your branch** to your fork:
   ```bash
   git push origin feature/your-feature-name
   ```

2. **Create a Pull Request** on GitHub with:
   - Clear title describing the change
   - Detailed description of what and why
   - Reference any related issues
   - Screenshots (for UI changes)

3. **PR Template**:
   ```markdown
   ## Description
   Brief description of changes

   ## Type of Change
   - [ ] Bug fix
   - [ ] New feature
   - [ ] Breaking change
   - [ ] Documentation update

   ## Testing
   - [ ] Manual testing completed
   - [ ] Unit tests added/updated
   - [ ] CSV injection test cases verified

   ## Checklist
   - [ ] Code builds without warnings
   - [ ] XML documentation complete
   - [ ] Follows coding standards
   - [ ] No merge conflicts
   ```

### Review Process

- Maintainers will review your PR
- Address feedback and push updates
- Once approved, maintainers will merge

## Testing Guidelines

### Manual Testing

For now, manually test all changes:
- Test happy path scenarios
- Test error cases
- Test edge cases (empty lists, single item, etc.)
- Test CSV import/export with various inputs

### Unit Testing (Coming Soon)

When adding tests:
- One test class per production class
- Clear test method names: `MethodName_Scenario_ExpectedResult`
- Use AAA pattern: Arrange, Act, Assert
- Test both success and failure cases

Example:
```csharp
[Test]
public void IsNameValid_NameStartsWithEquals_ReturnsFalse()
{
    // Arrange
    var player = new Player { Name = "=SUM(A1:A10)" };

    // Act
    var result = player.IsNameValid();

    // Assert
    Assert.IsFalse(result);
}
```

## Architecture Guidelines

### Project Structure

Follow Clean Architecture:
- **Core** - Contains business logic, no dependencies on UI or infrastructure
- **Desktop** - UI layer, depends on Core

### Adding New Features

When adding features, consider:

1. **Model Changes** - Update domain models in `Core/Models/`
2. **Business Logic** - Add services in `Core/Services/`
3. **Interfaces** - Define contracts in `Core/Services/Interfaces/`
4. **UI Components** - Add Blazor components in `Desktop/Components/`
5. **DI Registration** - Register services in `MauiProgram.cs`

### Design Principles

- **Single Responsibility** - Each class has one reason to change
- **Open/Closed** - Open for extension, closed for modification
- **Liskov Substitution** - Subtypes must be substitutable for base types
- **Interface Segregation** - Many specific interfaces over one general
- **Dependency Inversion** - Depend on abstractions, not concretions

### Code Duplication

If you find duplicated code:
1. Extract it to a shared base class or utility method
2. Update both usages to use the shared code
3. Add XML documentation explaining the shared logic

See [BaseTeamBalancingStrategy.cs](TeamBalancer.Core/Services/Balancing/BaseTeamBalancingStrategy.cs) for an example.

## Common Tasks

### Adding a New Balancing Strategy

1. Create new class inheriting from `BaseTeamBalancingStrategy`
2. Implement `BalanceTeams` method
3. Add XML documentation
4. Register in DI container
5. Update UI to include new option

### Adding Player Validation

1. Add validation method to `Player.cs`
2. Call from `CsvPlayerRepository`
3. Update error messages
4. Test with various inputs

### Improving Security

1. Identify vulnerability
2. Add input validation
3. Add output sanitization (defense-in-depth)
4. Log security events
5. Document in CSV_INJECTION_TEST.md

## Questions?

- Check existing issues for similar questions
- Create a new issue with the `question` label
- Be specific and provide context

## Recognition

Contributors will be recognized in:
- GitHub contributors page
- Release notes for significant contributions
- Project acknowledgments

Thank you for contributing to TeamBalancer! 🎉
