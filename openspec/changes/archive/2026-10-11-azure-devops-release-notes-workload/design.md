# Design

## Context

See `proposal.md` for background and problem motivation. The workload coordinates three primary systems: Azure DevOps Pipelines & REST APIs, Azure Functions (.NET Isolated), and an Azure Static Web App hosting Blazor WebAssembly, backed by Cosmos DB.

## Goals / Non-Goals

**Goals:**
- Provide a unified .NET solution structure (`Client`, `Api`, `Shared`).
- Use Cosmos DB for persisting structured JSON documents of release notes.
- Integrate with Azure DevOps REST API via Bearer token passed from the executing build pipeline.
- Offer a Blazor WebAssembly frontend for dashboard viewing and drill-down into release notes.

**Non-Goals:**
- Automated provisioning of Azure infrastructure (handled separately via Bicep/Terraform/Azure Portal).
- Multi-tenant enterprise RBAC within the viewer app in the initial release.

## Decisions

### 1. Solution Architecture: Integrated Static Web App with .NET Isolated Backend
- **Decision**: Host frontend in Blazor WebAssembly and backend in Azure Functions (.NET Isolated Worker) within Azure Static Web Apps structure.
- **Rationale**: Eliminates CORS complexity, allows shared .NET C# models between frontend and API, and simplifies local development using SWA CLI.
- **Alternatives Considered**: Standalone App Service / Container Apps (higher cost and operational overhead).

### 2. Pipeline Invocation Pattern
- **Decision**: REST HTTP POST call from the pipeline step with `System.AccessToken` forwarded in the Authorization header.
- **Rationale**: Explicit, synchronous feedback in pipeline logs, avoids needing to configure asynchronous webhooks or service hooks in Azure DevOps.
- **Alternatives Considered**: Service hooks (requires admin setup in ADO project settings).

### 3. Cosmos DB Document Partitioning
- **Decision**: Partition key by `/projectId`.
- **Rationale**: Release notes are scoped to team projects; queries are typically filtered by project and ordered by build timestamp.

## Risks / Trade-offs

- **[Risk] Azure DevOps Rate Limits / Token Permissions** → **Mitigation**: Pipeline must ensure "Allow scripts to access the OAuth token" is checked; API handles batch queries for work item expansions.
- **[Risk] Large Builds with Thousands of Commits** → **Mitigation**: Paginate or cap top commit changes in summary document.
