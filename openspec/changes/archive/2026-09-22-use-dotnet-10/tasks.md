# Tasks

## 1. Project Target Framework Updates

- [x] 1.1 Update `src/Shared/Shared.csproj` to target `net10.0` and verify compilation with `dotnet build src/Shared/Shared.csproj`
- [x] 1.2 Update `src/Api/Api.csproj` to target `net10.0`, align `Microsoft.Extensions.Hosting` and Azure Functions packages, and verify compilation with `dotnet build src/Api/Api.csproj`
- [x] 1.3 Update `tests/Api.Tests/Api.Tests.csproj` to target `net10.0` and verify compilation with `dotnet build tests/Api.Tests/Api.Tests.csproj`

## 2. Solution-Wide Verification

- [x] 2.1 Rebuild the entire solution (`ReleaseNotes.slnx`) on the .NET 10 SDK and verify successful build output
- [x] 2.2 Execute all unit tests via `dotnet test` and verify all tests pass against the upgraded .NET 10 target
