# Azure DevOps Release Notes

Compiles release notes from related commits and Work Items for Azure DevOps builds triggered by the build run itself. An Azure Function API stores the compiled release notes in Azure Storage Tables, and a Blazor Static Web App displays them.

## Compile a release

Post build metadata to `POST /api/releases/compile` with Function authorization. The endpoint returns `202 Accepted` after queueing the request, not after compilation. Its JSON acknowledgment contains `id`, `projectId`, and an origin-relative `releaseUrl`; poll that URL for the compiled release. For example, use `curl` with `FUNCTION_KEY` and the configured Azure DevOps build metadata:

```bash
curl --fail-with-body --silent --show-error \
	-X POST "$API_BASE_URL/releases/compile?code=$FUNCTION_KEY" \
	-H 'Content-Type: application/json' \
	-d '{"organization":"contoso","project":"release-notes","projectId":"project-id","repositoryId":"repository-id","buildId":42}'
```

See [Run the app](docs/run-the-app.md) for a bounded polling example. Before the first persistence, GET returns 404. During recompilation an older release remains readable, so a successful GET does not prove that the latest request has finished.

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
