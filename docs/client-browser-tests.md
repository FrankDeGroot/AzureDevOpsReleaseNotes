# Client browser tests

[tests/Client.BrowserTests](../tests/Client.BrowserTests) uses Playwright for .NET to exercise the Blazor release dashboard and detail navigation in Chromium. The tests are categorized as `Integration` and are excluded from the regular unit-test run. They target `http://localhost:4280` by default and use live same-origin API requests by default; a failed live request never switches to stub data. Set `CLIENT_BASE_URL` to target another client host.

This Windows-backed workspace does not support Playwright's `chmod` build step on the mounted source tree. Keep the build output on the Linux filesystem, then install Chromium using the generated script. Run the dependency install only if Playwright reports missing Linux libraries:

```bash
dotnet build tests/Client.BrowserTests -p:BaseOutputPath=/tmp/client-browser-tests-build/
pwsh /tmp/client-browser-tests-build/Debug/net10.0/playwright.ps1 install chromium
sudo pwsh /tmp/client-browser-tests-build/Debug/net10.0/playwright.ps1 install-deps
```

For a live run, start the client, Functions host, and Static Web Apps CLI as described in [Run the app](run-the-app.md). Set up the Azure DevOps test project and run the API integration tests first so the browser has a seeded release:

```powershell
. ./.env.ps1
dotnet test tests/Api.IntegrationTests --filter "Category=Integration"
```

Then run the browser suite in its live default mode:

```bash
dotnet test tests/Client.BrowserTests --filter "Category=Integration" -p:BaseOutputPath=/tmp/client-browser-tests-build/
```

For an isolated UI run, keep the client and Static Web Apps CLI running and explicitly enable deterministic API stubs:

```bash
CLIENT_API_MODE=stub dotnet test tests/Client.BrowserTests --filter "Category=Integration" -p:BaseOutputPath=/tmp/client-browser-tests-build/
```