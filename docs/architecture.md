# Architecture

## Software Components

- **Frontend**: Blazor WebAssembly ([src/Client](../src/Client))
- **Backend**: Azure Functions isolated worker ([src/Api](../src/Api))
- **Shared**: Domain models ([src/Shared](../src/Shared))

## Infrastructure Components

- **Static Web App**: Hosts the Blazor WebAssembly frontend.
- **Azure Functions**: Hosts the backend API using a system-assigned managed identity.
- **Azure Storage Tables**: Stores compiled release notes in the existing Functions StorageV2 account; the Function identity has table data access.
- **GitHub Actions**: Automates the deployment of the application components.
- **Bicep**: Infrastructure as code for provisioning Azure resources.
