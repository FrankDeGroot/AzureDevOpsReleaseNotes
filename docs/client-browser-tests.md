# Client browser tests

[tests/Client.BrowserTests](../tests/Client.BrowserTests) uses Playwright for .NET to exercise the dashboard, detail navigation/refresh, and theme in Chromium. The tests are categorized as `Integration` and target `http://localhost:7071` by default. Live same-origin requests remain the default; failed live requests never switch to stub data. Set `CLIENT_BASE_URL` for another host.

The dev-container bootstrap installs the browser and Linux libraries. Retry with `bash .devcontainer/bootstrap.sh`. Keep generated browser-test output on a writable Linux filesystem because Windows-backed mounts can reject Playwright's executable permission changes. The default bootstrap location is `${TMPDIR:-/tmp}/client-browser-tests-build/`; use that same location for builds and tests:

```bash
build_directory="${TMPDIR:-/tmp}/client-browser-tests-build/"
dotnet build tests/Client.BrowserTests -p:BaseOutputPath="$build_directory"
```

## Isolated checks

Start only the combined host with `dotnet run --project src/Api`, then explicitly select stubs. No Azure login or Cosmos/Azure DevOps requests are needed:

```bash
CLIENT_API_MODE=stub dotnet test tests/Client.BrowserTests --filter 'Category=Integration' -p:BaseOutputPath="${TMPDIR:-/tmp}/client-browser-tests-build/"
```

## Live checks

Keep the same combined host running. Prepare Cosmos trust, sign in with `az login`, and seed release data with the API integration suite:

```powershell
. ./.env.ps1
dotnet test tests/Api.IntegrationTests --filter 'Category=Integration&Target!=Azure'
```

Then run the browser suite in its live default mode:

```bash
CLIENT_API_MODE=live dotnet test tests/Client.BrowserTests --filter 'Category=Integration' -p:BaseOutputPath="${TMPDIR:-/tmp}/client-browser-tests-build/"
```

CI runs the live suite against the deployed App Service URL after API compilation succeeds. Public browser requests carry no compile token. Missing browsers/libraries are setup failures; repair bootstrap rather than changing test mode implicitly.