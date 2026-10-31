# Architecture

## Software Components

- **Frontend**: Blazor WebAssembly ([src/Client](../src/Client))
- **Host and backend**: ASP.NET Core .NET 10 ([src/Api](../src/Api)), serving the existing client and API from one origin and publish output.
- **Shared**: Domain models ([src/Shared](../src/Shared))

## Infrastructure Components

- **App Service**: One Linux web app on a dedicated B1-default plan hosts the client assets and API. There is no separate production frontend or Functions deployment.
- **Easy Auth**: Validates Entra tokens; application authorization protects compilation only. Local Development permits anonymous compilation, but an Azure-hosted Development instance does not.
- **Managed identity**: The web app's outbound identity calls Azure DevOps independently of the inbound caller token.
- **Cosmos DB**: Stores the compiled release notes data.
- **GitHub Actions**: Automates the deployment of the application components.
- **Bicep**: Infrastructure as code for provisioning Azure resources.
- **Application Insights and Log Analytics**: Shared application telemetry and diagnostics. Cosmos records and existing monitoring are preserved during hosting migration.
