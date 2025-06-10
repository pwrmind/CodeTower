# CodeTower

CodeTower is a powerful .NET solution restructuring and architecture scaffolding tool that helps you maintain clean and organized codebases. It provides features for restructuring existing solutions and generating new architectural patterns.

## Features

- **Solution Restructuring**
  - Move and rename namespaces
  - Extract classes to new files
  - Analyze and validate dependencies
  - Automatic backup before changes
  - Comprehensive logging

- **Architecture Scaffolding**
  - Clean Architecture template
  - Onion Architecture template (coming soon)
  - Vertical Slice Architecture template (coming soon)

- **Safety Features**
  - Automatic solution backup
  - Dependency conflict detection
  - Rollback capability
  - Detailed logging

## Installation

```bash
dotnet tool install --global CodeTower
```

## Usage

### Restructuring a Solution

```bash
codetower restructure --solution YourSolution.sln --config restructure.json
```

Example configuration file (restructure.json):
```json
{
  "transformations": [
    {
      "type": "MoveNamespace",
      "source": "OldNamespace",
      "target": "NewNamespace"
    },
    {
      "type": "ExtractClass",
      "source": "LargeClass",
      "target": "Infrastructure.Services"
    }
  ]
}
```

### Generating Architecture Scaffolding

```bash
codetower generate --solution YourSolution.sln --template cleanarchitecture
```

## Supported Transformation Types

- `MoveNamespace`: Move classes from one namespace to another
- `RenameNamespace`: Rename an existing namespace
- `ExtractClass`: Move a class to a new file/namespace
- `GenerateLayer`: Create a new architectural layer with predefined structure

## Architecture Templates

### Clean Architecture
- Domain Layer
  - Entities
  - Value Objects
  - Interfaces
- Application Layer
  - Use Cases
  - Interfaces
  - DTOs
  - Services
- Infrastructure Layer
  - Data
  - Services
  - Repositories
  - External
- Presentation Layer

## Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Acknowledgments

- Built with [Roslyn](https://github.com/dotnet/roslyn)
- Command-line interface powered by [System.CommandLine](https://github.com/dotnet/command-line-api)
