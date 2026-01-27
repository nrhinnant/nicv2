# 001 — Solution Skeleton

## Behavior

This milestone establishes the .NET solution structure for the WfpTrafficControl project. It creates the foundational projects without any WFP logic.

### Projects Created

| Project | Type | Purpose |
|---------|------|---------|
| `Shared` | Class Library (.NET 8) | Shared models, GUIDs, error types |
| `Service` | Worker Service (.NET 8 Windows) | Windows Service for policy control |
| `Cli` | Console App (.NET 8) | CLI tool (`wfpctl`) |
| `Tests` | xUnit Test Project | Unit and integration tests |

### Directory Layout

```
/
├── WfpTrafficControl.sln
├── src/
│   ├── shared/
│   │   ├── Shared.csproj
│   │   └── Placeholder.cs
│   ├── service/
│   │   ├── Service.csproj
│   │   ├── Program.cs
│   │   └── Worker.cs
│   └── cli/
│       ├── Cli.csproj
│       └── Program.cs
├── tests/
│   ├── Tests.csproj
│   └── SanityTests.cs
├── scripts/
│   └── .gitkeep
└── docs/
    └── features/
```

## Configuration / Schema Changes

None. This is scaffolding only.

## How to Build

```bash
# Build entire solution
dotnet build

# Build specific project
dotnet build src/service/Service.csproj
```

## How to Run Tests

```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

## How to Run CLI (dev mode)

```bash
dotnet run --project src/cli/Cli.csproj
```

Output:
```
WfpTrafficControl CLI
Usage: wfpctl <command>

Commands:
  status    - Show service and policy status
  validate  - Validate a policy file
  apply     - Apply a policy file
  rollback  - Rollback to previous policy
  enable    - Enable traffic control
  disable   - Disable traffic control
  logs      - Show logs

(Not yet implemented)
```

## Rollback / Uninstall Behavior

Not applicable — no runtime artifacts created by this milestone.

## Known Limitations

- Service compiles but does not perform any WFP operations yet
- CLI displays help only; no commands implemented
- Shared library contains only a placeholder class

## Dependencies

| Package | Version | Project |
|---------|---------|---------|
| Microsoft.Extensions.Hosting | 8.0.0 | Service |
| Microsoft.Extensions.Hosting.WindowsServices | 8.0.0 | Service |
| Microsoft.NET.Test.Sdk | 17.9.0 | Tests |
| xunit | 2.7.0 | Tests |
| xunit.runner.visualstudio | 2.5.7 | Tests |
| coverlet.collector | 6.0.1 | Tests |
