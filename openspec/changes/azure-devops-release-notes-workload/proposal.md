# Proposal

## Why

Development teams using Azure DevOps pipelines often lack an automated, centralized, and accessible way to generate, store, and view release notes derived directly from build runs, commit histories, and associated work items. This change introduces an integrated Azure workload to automatically compile release notes during pipeline execution, persist them in Azure Cosmos DB, and expose them through a modern Blazor WebAssembly frontend.

## What Changes

- Add a .NET Isolated Azure Function API providing endpoints to compile release notes from build triggers and query stored notes.
- Integrate Azure DevOps REST API client in the Function to fetch build changes (commits) and linked work items using the pipeline's runtime access token.
- Add an Azure Cosmos DB repository for structured persistence and querying of release notes.
- Add an Azure Static Web Apps frontend built with .NET Blazor WebAssembly to browse, filter, and view detailed release notes.
- Provide sample Azure DevOps YAML pipeline step definitions to invoke the compilation endpoint.

## Capabilities

### New Capabilities
- `release-notes-compiler`: Ingestion and compilation of Azure DevOps build changes and work items into release note documents via Azure Functions.
- `release-notes-storage`: Persistence and retrieval of compiled release note records in Azure Cosmos DB.
- `release-notes-viewer`: Web-based dashboard and detail viewer for release notes implemented in Blazor WebAssembly on Azure Static Web Apps.

### Modified Capabilities
<!-- None -->

## Impact

- **New Projects & Components**: Adds Blazor WebAssembly client (`src/Client`), Azure Functions backend (`src/Api`), shared models (`src/Shared`), and sample pipeline definitions (`pipelines/`).
- **External Dependencies**: Azure DevOps REST APIs, Azure Cosmos DB SDK for .NET, Microsoft.Azure.Functions.Worker.
