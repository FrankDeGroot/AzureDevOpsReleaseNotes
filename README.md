# Azure DevOps Release Notes

Compiles commits and work items for Azure DevOps builds, stores release notes in Cosmos DB, and displays them in Blazor WebAssembly. One ASP.NET Core application on Azure App Service serves both the viewer and `/api` endpoints. Compilation uses Microsoft Entra Easy Auth in Azure; the viewer and read APIs remain public.

## Table of Contents

- [Local Emulators](docs/local-emulators.md)
	- [Run the app](docs/run-the-app.md)
- [Environment configuration](docs/environment-configuration.md)
- [Integration tests](docs/integration-tests.md)
- [Client browser tests](docs/client-browser-tests.md)
- [Deployment](docs/deployment.md)
	- [Architecture](docs/architecture.md)
- [Deployment Setup](docs/deployment-setup.md)
	1. [Configure Azure OIDC for GitHub Actions](docs/configure-azure-oidc-for-github-actions.md)
	2. [Provision with Azure CLI](docs/provision-with-azure-cli.md)
	3. [Deploy with GitHub Actions](docs/deploy-with-github-actions.md)
	4. [Set up the Azure DevOps test project](docs/set-up-the-azure-devops-test-project.md)
