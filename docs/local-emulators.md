# Local Emulators

Run **Dev Containers: Rebuild Container** with Docker Compose available on the host. The .NET 10 workspace provides PowerShell 7, Azure CLI, GitHub CLI, Node/OpenSpec, and Playwright Chromium with its Linux libraries. The Linux Cosmos DB emulator runs alongside it in gateway mode. No Azurite, Functions host, or SWA proxy is needed.

Creation runs [.devcontainer/bootstrap.sh](../.devcontainer/bootstrap.sh): solution restore, browser-test build on the Linux filesystem, and package-matched Chromium/library installation. It is repeatable and does not sign in, request secrets, create cloud resources, or run live tests. Retry partial setup or refresh browsers after changing the Playwright package with:

```bash
bash .devcontainer/bootstrap.sh
```

The container configures the API's Cosmos settings. On each start, [.devcontainer/trust-cosmos-certificate.sh](../.devcontainer/trust-cosmos-certificate.sh) waits up to approximately three minutes for readiness and imports the current HTTPS certificate. If setup fails, inspect the `cosmosdb` service logs on the host, ensure it is running, and retry:

```bash
bash .devcontainer/trust-cosmos-certificate.sh
curl --fail https://localhost:8081/
```

An HTTP application error from the Cosmos endpoint can still demonstrate successful TLS; certificate errors indicate trust setup has not succeeded. Do not disable TLS validation. The key in [.devcontainer/cosmos-emulator.key](../.devcontainer/cosmos-emulator.key) is public and only for local development.

- Combined application (forwarded): `http://localhost:7071`.
- Cosmos DB endpoint: `https://localhost:8081`.
- Cosmos Data Explorer: `http://localhost:1234`.

Cosmos data persists in the `cosmos-data` named volume. Restart/rebuild without deleting volumes preserves records. Never use volume deletion as a routine rebuild or hosting-migration step.

## Verify the workspace

```bash
dotnet --version
pwsh --version
dotnet build src/Api
dotnet publish src/Api -c Release -o bin/app-publish
dotnet test tests/Api.Tests
```

Start the app as described in [Run the app](run-the-app.md), then run explicit stub-browser tests from [Client browser tests](client-browser-tests.md). These isolated checks require no Azure credentials or live dependency requests. Real API tests additionally require ready/trusted Cosmos, `az login`, and Azure DevOps test-project settings. Missing prerequisites are setup failures, not permission to silently use stubs.

[Run the app](run-the-app.md)