# Azure DevOps Release Notes

Compiles release notes from related commits and Work Items for Azure DevOps builds triggered by the build run itself. An Azure Function API stores the compiled release notes in Azure Storage Tables, and a Blazor Static Web App displays them.

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
