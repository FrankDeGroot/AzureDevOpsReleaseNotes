# Design

## Context

The solution currently contains a mix of target frameworks: `src/Client/Client.csproj` is configured for `net10.0`, while `src/Api/Api.csproj`, `src/Shared/Shared.csproj`, and `tests/Api.Tests/Api.Tests.csproj` target `net8.0`. The development environment has the .NET 10 SDK (`10.0.100-rc.2.25502.107`) installed. See `proposal.md` for motivation.

## Goals / Non-Goals

**Goals:**
- Retarget `src/Api/Api.csproj`, `src/Shared/Shared.csproj`, and `tests/Api.Tests/Api.Tests.csproj` to `net10.0`.
- Update package references across projects to be compatible with .NET 10.
- Ensure all solution projects build cleanly and unit tests pass under the .NET 10 SDK.

**Non-Goals:**
- Modifying the REST API routes, request/response models, or Cosmos DB schema.
- Refactoring application business logic or Blazor UI components beyond framework compatibility.

## Decisions

- **Decision: Unify Target Framework on `net10.0`**
  - *Rationale*: Standardizing on `net10.0` across client, server, shared library, and tests eliminates cross-TFM dependency quirks and enables full utilization of the installed .NET 10 runtime and tooling.
  - *Alternatives considered*: Retaining `net8.0` for backend while frontend uses `net10.0` was rejected to avoid mismatched language features, serializer differences, and split build targets.

- **Decision: Dependency alignment for .NET 10**
  - *Rationale*: Azure Functions Isolated Worker v4 models run as standalone executables (`OutputType=Exe`), allowing smooth transition to `net10.0` with Microsoft.Azure.Functions.Worker packages and updated Microsoft.Extensions.* references.
  - *Alternatives considered*: Retaining older packages; evaluated on a per-package basis to avoid breaking breaking changes during compile/test execution.

## Risks / Trade-offs

- **[Risk] Worker SDK or MSBuild target deprecations/warnings on .NET 10 SDK** → *Mitigation*: Verify build logs and test execution across all projects to ensure clean compilation.
- **[Risk] Intermediate build artifact conflicts between .NET 8 and .NET 10 obj outputs** → *Mitigation*: Perform a full clean of `bin` and `obj` directories during apply.
