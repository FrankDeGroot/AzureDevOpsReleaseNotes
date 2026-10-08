# Design

## Context

- The API (`src/Api`) authenticates to Azure DevOps through `TokenCredential`. In Development this is `DefaultAzureCredential` (the developer's `az login`). In Azure it is `ManagedIdentityCredential` (the system-assigned identity of `func-release-notes-<suffix>`). Cosmos DB uses the emulator locally, configured in `src/Api/local.settings.json`, and the Bicep-provisioned account in Azure.
- `CosmosReleaseNoteStore.CheckAsync` reads the container without creating it. On a fresh emulator, `GET health` reports Cosmos as unhealthy until the first upsert creates the database and container.
- `POST releases/compile` is `AuthorizationLevel.Function`. The local Functions host does not enforce keys; the deployed app does.
- `.github/workflows/azure-static-web-apps.yml` (owned by the in-flight `add-bicep-github-deployment` change) runs `dotnet test` on the whole solution before publishing, then deploys the Function App with `Azure/functions-action`.
- `.gitignore` ignores `.env*`. The developer's current `.env` uses legacy keys (`TENANT_ID`, `CLIENT_ID`, `CLIENT_SECRET`, `BUILD_ID`, `REPOSITORY_ID`). `.env.ps1` is maintained by hand.
- PowerShell 7 and `gh` are available in the dev container. The Azure CLI `azure-devops` extension is not installed.

## Goals / Non-Goals

**Goals:**
- One integration suite that runs unchanged against the local host and the deployed Function App. Only the base URL and function key differ.
- Setup scripts that can be re-run at any time and converge to the same state.
- No long-lived Azure DevOps credentials: the developer uses `az login` and the deployed app uses its managed identity.

**Non-Goals:**
- No changes to API behavior, endpoints, or authentication. The one exception is a bug that the integration tests exposed: the build work items API returns `id` values as JSON strings, and the API now parses them (covered by a unit test).
- No mocking or emulation of Azure DevOps.
- No tests for the Blazor client or Static Web Apps routing. The suite calls the Function App directly.
- No teardown script for the Azure DevOps project.
- No cleanup of Cosmos records written by the tests. Compile is an upsert keyed by `{projectId}:{buildId}`, so repeated runs overwrite a single record.

## Decisions

### 1. Separate `tests/Api.IntegrationTests` project, xUnit, `Category=Integration` trait
The integration tests are a new xUnit project. It references only `Shared` (for `ReleaseNoteDocument` / `CompileRequest` DTOs) and uses `HttpClient`. Every test class carries `[Trait("Category", "Integration")]`.
- The pre-deploy workflow step becomes `dotnet test --filter "Category!=Integration"`. The post-deploy step runs `dotnet test tests/Api.IntegrationTests --filter "Category=Integration"`.
- *Alternative:* add the tests to `Api.Tests` and use the trait only. This was rejected because a separate project keeps the HTTP-only tests free of the Functions worker dependencies, and makes it hard to start the suite against the wrong target by accident.

### 2. Configuration via environment variables
The suite reads its settings from environment variables:

| Variable | Purpose |
| --- | --- |
| `RELEASE_NOTES_API_URL` | API base URL. Defaults to `http://localhost:7071/api`. |
| `FUNCTION_KEY` | Optional function key, sent as `x-functions-key`. |
| `AZURE_DEVOPS_ORGANIZATION` | Azure DevOps organization. |
| `AZURE_DEVOPS_PROJECT` | Azure DevOps project name. |
| `AZURE_DEVOPS_PROJECT_ID` | Azure DevOps project ID. |
| `AZURE_DEVOPS_REPOSITORY_ID` | Repository ID for the compile request. |
| `AZURE_DEVOPS_BUILD_ID` | ID of the test build. |

`RELEASE_NOTES_API_URL` changes meaning: it was the full compile endpoint URL and is now the API base URL.
- Locally, these come from dot-sourcing `.env.ps1`. In CI, the workflow sets `RELEASE_NOTES_API_URL` and `FUNCTION_KEY` from deployment outputs and sets the `AZURE_DEVOPS_*` values from repository variables.
- A shared settings helper collects every missing required value and fails with a single message that points to `scripts/Initialize-AzureDevOpsTestProject.ps1`.
- *Alternative:* `appsettings`/user-secrets in the test project. This was rejected because environment variables are the common denominator for `.env.ps1`, GitHub Actions, and the sync script.

### 3. Compile once in a shared fixture, then assert
An `IAsyncLifetime` class fixture posts the compile request once and stores the HTTP status and body. Individual tests then check:
- the compile response;
- read-back by ID;
- presence in the project list;
- health;
- idempotent re-compile.

Compiling first guarantees the Cosmos database and container exist before `GET health` runs, which avoids the empty-emulator false negative described in Context. HTTP timeouts are generous (about 100 s) to allow for a cold start on the Flex Consumption plan.
- *Alternative:* have health create the container. This was rejected because it changes API behavior, which is a non-goal.

### 4. Deployed run: function key and URL from Azure CLI after deploy
After `Azure/functions-action`, a new workflow step:
1. runs `az functionapp keys list -g rg-azure-devops-release-notes -n <functionAppName> --query functionKeys.default` and passes the result to `::add-mask::`;
2. sets `RELEASE_NOTES_API_URL=https://<functionAppDefaultHostname>/api`, using the existing `functionAppDefaultHostname` Bicep output, which the deploy step adds to `$GITHUB_OUTPUT`;
3. runs the integration project.

The step polls `GET releases` for a short time before running tests, to absorb the deployment's cold start and propagation. It polls `GET releases` rather than `GET health` because `GET releases` creates the Cosmos container on a fresh account, while `GET health` only reads it.
- *Alternative:* call through the Static Web App `/api` linked backend. This was rejected because it adds SWA routing as a variable and is not needed to prove that the API reaches Cosmos DB and Azure DevOps.

### 5. Azure DevOps setup script uses REST plus an Azure CLI token (no `azure-devops` extension, no PAT)
`scripts/Initialize-AzureDevOpsTestProject.ps1` obtains its Azure DevOps token with `az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798` and calls the Azure DevOps REST APIs with `Invoke-RestMethod`. Each step is a get-or-create:

1. **Project**: `GET _apis/projects/{name}`. If it is missing, `POST _apis/projects` (Git, Basic process), then poll the operation until it completes.
2. **Repository**: reuse the project's default Git repo, which has the same name as the project.
3. **Work item**: a WIQL query for a `Task` tagged `release-notes-integration-seed`. If none is found, create one. The `Task` type exists in all built-in processes.
4. **Seed commit**: if the repo has no `main` branch, push an initial commit through `POST _apis/git/repositories/{id}/pushes` that contains `README.md` and `azure-pipelines.yml`. The commit message references `#<workItemId>`. The script also adds an `ArtifactLink` relation (`vstfs:///Git/Commit/{projectId}%2F{repoId}%2F{commitId}`) to the work item, unless the work item already has that link.
5. **Pipeline**: `GET _apis/pipelines`, filtered by name. If it is missing, `POST _apis/pipelines` pointing to `/azure-pipelines.yml` on `main`. The seed YAML uses an agentless job (`pool: server`, `Delay@1` with 0 minutes). This way the build does not need Microsoft-hosted agent parallelism, which new organizations often lack.
6. **Build**: look for the most recent succeeded build of the pipeline where both `builds/{id}/changes` and `builds/{id}/workitems` are non-empty. If none exists, push one more commit to `main` that references the work item (the first build of a pipeline can report no changes), queue a run, and poll until it completes. The script makes at most two attempts, then fails with a clear message.
7. **Managed identity access**: resolve `func-release-notes-$env:AZURE_RESOURCE_SUFFIX`, or `-FunctionAppName`, with `az functionapp identity show` to get the principal ID. Then:
   - `POST vssps.dev.azure.com/{org}/_apis/graph/serviceprincipals` with `originId` set to the principal ID. This operation is idempotent: it returns the existing descriptor.
   - Ensure a `serviceprincipalentitlements` entry with **Basic** access exists, through `vsaex.dev.azure.com`. Stakeholder access cannot read repository commit details.
   - Add the descriptor to the project's `Readers` group through the Graph memberships API, after checking `HEAD` membership first.

   If the app cannot be resolved, the script warns and skips this step.
8. **Write `.env`**: upsert the five `AZURE_DEVOPS_*` keys, keeping every other line as it is.

Parameters default from the environment: `-Organization` from `AZURE_DEVOPS_ORGANIZATION`, `-Project` from `AZURE_DEVOPS_PROJECT` (defaulting to `ReleaseNotesIntegration`), and `-FunctionAppName`.
- *Alternative:* the `az devops` / `az pipelines` CLI. This was rejected because it needs an extension install and still lacks commands for Graph service principals and entitlements, so REST would be needed anyway.
- *Alternative:* a PAT. This was rejected per the user decision to use Azure CLI and managed identity only.

### 6. `.env` sync script
`scripts/Sync-Environment.ps1 [-EnvFile .env] [-SkipGitHub] [-Repo owner/name]`:
- **Parsing**: parses `KEY=value` lines. It ignores blank lines and lines starting with `#`, and strips one level of surrounding quotes. `.env` is the single source of truth.
- **`.env.ps1` generation**: writes `.env.ps1` with a do-not-edit header and one `$env:KEY = '<value>'` line per key, doubling `'` in values. Single-quoted strings prevent `$` expansion.
- **GitHub sync**: unless `-SkipGitHub` is set, checks `gh auth status`. Keys in the local-only set (`RELEASE_NOTES_API_URL`, `FUNCTION_KEY`) are skipped. Keys matching `SECRET|TOKEN|PASSWORD|KEY` are sent with `gh secret set KEY --body -` over stdin, so the value never appears in process arguments or logs. All other keys are sent with `gh variable set KEY --body <value>`. Both commands are upserts, which makes the script idempotent.
- *Alternative:* a bash script. This was rejected because PowerShell is already the scripting language for this change and the generated `.env.ps1`.

### 7. Example file naming and `.gitignore`
The example file is `.env.example.ps1`. The `.ps1` extension lets it be dot-sourced after copying, and it documents keys in the format that is actually loaded. `.gitignore` adds `!.env.example.ps1` after `.env*`. The README tells users to keep `.env` as the source and to run the sync script, instead of editing `.env.ps1` by hand.

### 8. `.env` key normalization
The keys are normalized as follows:
- **GitHub variables used by the workflow:** `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_LOCATION`, `AZURE_RESOURCE_SUFFIX`, plus the five `AZURE_DEVOPS_*` keys.
- **Local-only:** `RELEASE_NOTES_API_URL` and `FUNCTION_KEY`.
- **Legacy keys:** `TENANT_ID`, `CLIENT_ID`, and `CLIENT_SECRET` are no longer used. The README migration note tells users to rename the first two and delete the secret. `BUILD_ID` and `REPOSITORY_ID` are replaced by the setup script's output.

## Risks / Trade-offs

- **The managed identity needs a Basic license.** Adding it uses one of the organization's Basic seats (the first 5 are free). → This is documented in the README. The step can be skipped by not providing a Function App.
- **The first deploy cannot pass integration tests before the identity is granted.** The managed identity only exists after the first Bicep deploy. → The README orders the steps: deploy, run the setup script (which grants the identity), then re-run the workflow. Until then, the integration step fails visibly, as it should.
- **The ADO first-build "changes" behavior is undocumented.** → The script verifies that changes and work items are non-empty, and otherwise pushes a referencing commit and rebuilds (up to two attempts).
- **Agentless job availability.** → `pool: server` with `Delay@1` needs no agent pool. If an organization disables YAML pipelines, the script fails with the REST error message.
- **Cold start or propagation delay after deploy causes flaky failures.** → A health poll with a bounded retry runs before the test step, and the HTTP client uses generous timeouts.
- **Secret classification by name pattern can misclassify.** For example, `FUNCTION_KEY` matches the pattern but is local-only, which is why it is in the skip list. → Misclassification fails safe: a non-secret value stored as a secret still works in the workflow, just masked. The README documents the rule.
- **The local emulator certificate.** This is already handled by the dev container trust hook and the API's loopback validation. The tests talk to the Functions host over HTTP only.

## Migration Plan

1. Rename the legacy keys in `.env` and run the setup script to populate the `AZURE_DEVOPS_*` keys.
2. Run `scripts/Sync-Environment.ps1` to regenerate `.env.ps1` and push the GitHub variables.
3. Merge. The next workflow run executes the integration tests after deployment.

Rollback: revert the workflow step, or set the integration step to `continue-on-error`. The scripts and tests have no effect on the runtime.

## Open Questions

- Whether to also expose the integration run as a manual `workflow_dispatch` input that targets an already-deployed environment without redeploying. This can be added later without changing the specs.
