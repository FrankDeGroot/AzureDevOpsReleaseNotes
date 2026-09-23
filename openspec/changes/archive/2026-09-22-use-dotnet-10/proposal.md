# Proposal

## Why

The solution currently has a mixed runtime configuration where the Blazor client targets .NET 10 while the API, Shared library, and test projects target .NET 8. Upgrading all projects to target .NET 10 unifies the solution ecosystem, ensures compatibility with the installed .NET 10 SDK, and unlocks .NET 10 language, runtime performance, and library enhancements.

## What Changes

- Upgrade target framework for `src/Api/Api.csproj` from `net8.0` to `net10.0`.
- Upgrade target framework for `src/Shared/Shared.csproj` from `net8.0` to `net10.0`.
- Upgrade target framework for `tests/Api.Tests/Api.Tests.csproj` from `net8.0` to `net10.0`.
- Update package dependencies (such as `Microsoft.Extensions.Hosting` and any related Azure Functions Worker or test packages) to versions compatible with .NET 10.
- Verify that solution build, test execution, and Azure Functions runtime configuration function cleanly under .NET 10.

## Capabilities

### New Capabilities
<!-- None -->

### Modified Capabilities
<!-- None (pure framework and runtime upgrade; skip_specs: true configured in .openspec.yaml) -->

## Impact

- **Affected Projects**: `src/Api/Api.csproj`, `src/Shared/Shared.csproj`, `tests/Api.Tests/Api.Tests.csproj`, `src/Client/Client.csproj`.
- **Dependencies**: Target frameworks updated to `net10.0`, Microsoft.Extensions.* and related SDK dependencies upgraded for .NET 10 compatibility.
- **Build & CI/CD**: Clean build across all projects and unit tests running on .NET 10 SDK (`10.0.100-rc.2.25502.107`).
