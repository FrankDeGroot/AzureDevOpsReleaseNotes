# Tasks

## 1. Solution Structure & Shared Models

- [x] 1.1 Initialize .NET solution with `Client` (Blazor WASM), `Api` (Functions Isolated), and `Shared` projects, verifying solution builds with `dotnet build`.
- [x] 1.2 Define shared domain models in `Shared` (`ReleaseNoteDocument`, `WorkItemSummary`, `CommitSummary`, `CompileRequest`), verifying types compile cleanly.

## 2. Azure Function Backend Implementation

- [x] 2.1 Implement Azure DevOps REST API client in `Api` to fetch build changes and work items, verifying with unit tests or mock HTTP responses.
- [x] 2.2 Implement Cosmos DB service in `Api` for persisting and querying release notes documents, verifying document serialization and CRUD logic.
- [x] 2.3 Implement HTTP endpoints (`/api/releases/compile`, `/api/releases`, `/api/releases/{id}`) in `Api`, verifying endpoint parameter validation and response codes.

## 3. Blazor WebAssembly Frontend Implementation

- [x] 3.1 Create API client and state management in `Client` to interact with `/api/releases` endpoints, verifying successful mock service calls.
- [x] 3.2 Implement Dashboard page in `Client` listing releases with build metadata and summary counts, verifying UI layout and rendering.
- [x] 3.3 Implement Release Details page in `Client` with work item lists, external links, and commit history, verifying navigation and detail view rendering.

## 4. Pipeline Integration & Static Web App Config

- [x] 4.1 Add `staticwebapp.config.json` for routing and configuration, verifying valid JSON configuration.
- [x] 4.2 Create sample Azure DevOps YAML pipeline file (`pipelines/azure-pipelines.yml`) demonstrating build trigger invocation to compile release notes.
