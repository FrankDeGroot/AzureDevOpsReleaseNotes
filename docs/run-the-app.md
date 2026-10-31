# Run the app

Rebuild the development container, then follow [Local emulators](local-emulators.md) for bootstrap and recovery. The workspace provides .NET 10, PowerShell 7, Azure CLI, and Playwright Chromium.

From the repository root, start the single Development host:

```bash
dotnet run --project src/Api
```

Open `http://localhost:7071`. The same process serves the Blazor WebAssembly client and `/api`; no SWA proxy, separate client process, or Functions tools are needed. Configuration uses ordinary ASP.NET Core appsettings, environment variables, and user secrets, not `local.settings.json`.

Static delivery and isolated tests require no Azure login or external-service connection. For real API data, the Cosmos emulator must be ready and trusted. Sign in with `az login` before compilation; your identity must have access to the Azure DevOps project. See [Integration tests](integration-tests.md) and [Client browser tests](client-browser-tests.md) for isolated versus live checks.

## Authentication

In Azure, `POST /api/releases/compile` requires an Entra bearer token accepted by App Service Easy Auth for the configured tenant and API audience. Missing authentication returns JSON HTTP 401, not a login redirect. The viewer, release reads, and health remain public. Function keys and `code` query parameters no longer authorize compilation.

Only local Development bypasses inbound authentication. The bypass is disabled whenever App Service hosting markers are present, including an Azure app accidentally configured as Development. Never manufacture `X-MS-CLIENT-PRINCIPAL` headers: they are trusted only behind the Azure platform boundary, not on ordinary local hosts. The server's outbound Azure DevOps identity remains separate from the caller's token.

Application Insights is optional locally. Set `APPLICATIONINSIGHTS_CONNECTION_STRING` for server telemetry; client telemetry uses its existing public configuration. Do not place API bearer tokens in client settings.