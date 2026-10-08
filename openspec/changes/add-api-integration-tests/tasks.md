# Tasks

## 1. Environment configuration and sync

- [x] 1.1 Add `!.env.example.ps1` after `.env*` in `.gitignore`. Verify with `git check-ignore -v .env .env.ps1 .env.example.ps1`: the first two are ignored and the example is not.
- [x] 1.2 Create `.env.example.ps1` with placeholder `$env:` assignments and one-line comments for these keys: `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_LOCATION`, `AZURE_RESOURCE_SUFFIX`, the five `AZURE_DEVOPS_*` keys, `RELEASE_NOTES_API_URL`, and `FUNCTION_KEY`. Verify that `pwsh -c ". ./.env.example.ps1"` runs without errors and that no real values appear.
- [x] 1.3 Implement `scripts/Sync-Environment.ps1` (parameters `-EnvFile`, `-SkipGitHub`, `-Repo`). It parses `.env` and writes `.env.ps1` with single-quote escaping. Verify with a temporary `.env` containing `'`, `$`, `=`, comments, and blank lines: dot-sourcing the generated file yields exact values.
- [ ] 1.4 Add the GitHub sync to `scripts/Sync-Environment.ps1`:
  - a `gh auth status` precheck;
  - skip the local-only keys;
  - send secret-pattern keys through `gh secret set --body -` on stdin;
  - send all other keys through `gh variable set`.

  Verify with `-Repo` pointing at a test repo or the real repo: `gh variable list` and `gh secret list` show the expected names, no secret value appears in the output, and a second run succeeds unchanged.
- [x] 1.5 Migrate the local `.env` from the legacy keys (`TENANT_ID` → `AZURE_TENANT_ID`, `CLIENT_ID` → `AZURE_CLIENT_ID`, remove `CLIENT_SECRET`/`BUILD_ID`/`REPOSITORY_ID`), then run `scripts/Sync-Environment.ps1 -SkipGitHub`. Verify that `.env.ps1` is regenerated with the new keys.

## 2. Azure DevOps test project setup script

- [x] 2.1 Create `scripts/Initialize-AzureDevOpsTestProject.ps1` with:
  - parameters (`-Organization`, `-Project`, `-FunctionAppName`, `-EnvFile`) that default from the environment;
  - an `az account get-access-token` precheck that fails before any change when the user is not signed in;
  - a shared REST helper.

  Verify that running after `az logout` exits non-zero with an `az login` instruction.
- [x] 2.2 Implement get-or-create for the project (including polling the create operation), the default repository, and the tagged seed `Task` work item. Verify that two consecutive runs produce one project and one tagged work item, checked with a WIQL count.
- [x] 2.3 Implement the seed push: `README.md` plus an agentless `azure-pipelines.yml` (`pool: server`, `Delay@1`), a commit message referencing `#<workItemId>`, and an idempotent `ArtifactLink` from the work item to the commit. Verify that the repo has `main` with the files and that the work item shows a single commit link after two runs.
- [x] 2.4 Implement get-or-create for the pipeline, plus the qualifying-build search:
  - look for a succeeded build with non-empty `changes` and `workitems`;
  - otherwise push a referencing commit, queue a run, and poll until it completes (at most 2 attempts).

  Verify that the first run produces a qualifying build and that a second run reuses the same build ID without queuing.
- [x] 2.5 Implement the managed-identity grant: resolve the Function App principal, upsert the Graph service principal, ensure the Basic entitlement, and add the identity to project Readers after a membership check. Warn and skip when the app cannot be resolved. Verify:
  - against the deployed app, the identity appears in the org users list and in Readers;
  - a re-run makes no changes;
  - with a bogus `-FunctionAppName`, the script warns and exits 0.
- [x] 2.6 Implement the `.env` upsert of the five `AZURE_DEVOPS_*` keys that preserves other lines. Verify by diffing `.env` before and after a run: only those keys change.
- [x] 2.7 Delete `pipelines/azure-pipelines.yml` (and the empty `pipelines/` folder). Replace README section "4. Configure Azure DevOps Pipeline" with setup-script instructions: prerequisites, the Basic license note, and the order deploy → run script → re-run workflow. Verify that `grep -rn "azure-pipelines.yml" README.md .github` returns nothing.

## 3. Integration test project

- [x] 3.1 Create the xUnit project `tests/Api.IntegrationTests` (net10.0) that references `src/Shared`, and add it to `ReleaseNotes.slnx`. Verify that `dotnet build ReleaseNotes.slnx` succeeds.
- [x] 3.2 Add a settings helper that reads `RELEASE_NOTES_API_URL` (default `http://localhost:7071/api`), `FUNCTION_KEY`, and the `AZURE_DEVOPS_*` variables. It fails with one message listing the missing keys and naming the setup script. Add an `HttpClient` factory that sets the `x-functions-key` header and a timeout of about 100 s. Verify that running with `AZURE_DEVOPS_BUILD_ID` unset fails with that message.
- [x] 3.3 Add an `IAsyncLifetime` fixture that compiles the configured build once, plus `[Trait("Category","Integration")]` tests for:
  - the compile response (ID, build number, at least one commit, at least one work item);
  - `GET releases/{id}?projectId=` read-back;
  - `GET releases?project=` containing exactly one record with the ID after a second compile;
  - `GET health` returning 200 with both checks healthy.

  Verify that the tests compile.
- [x] 3.4 Run locally with the dev-container Cosmos emulator: `az login`, `cd src/Api && func start` (or `dotnet run`), then `. ./.env.ps1`, then `dotnet test tests/Api.IntegrationTests --filter Category=Integration`. Verify that all tests pass and that the record is visible in Cosmos Data Explorer at `http://localhost:1234`.
- [x] 3.5 Add a README section "Integration tests" that covers local prerequisites, the run commands, and the configuration variables. Verify that the commands work as written from a fresh terminal.

## 4. GitHub Actions integration

- [x] 4.1 Change the pre-deploy step to `dotnet test --configuration Release --filter "Category!=Integration"`. Verify locally that the same command runs no integration tests (the test count equals the `Api.Tests` count).
- [x] 4.2 Expose `function_app_default_hostname` from the deploy step's outputs. After the Functions deploy, add a step that:
  - gets and masks `functionKeys.default` with `az functionapp keys list`;
  - polls `GET /api/releases` (readiness; also creates the Cosmos container) with a bounded retry;
  - runs `dotnet test tests/Api.IntegrationTests -c Release --filter Category=Integration` with `RELEASE_NOTES_API_URL`, `FUNCTION_KEY`, and the `vars.AZURE_DEVOPS_*` values in `env`.

  Verify with an `actionlint` run (or a YAML lint) and by inspecting the workflow.
- [x] 4.3 Update the README "Deployment Setup" section:
  - list the new repository variables;
  - reference `scripts/Sync-Environment.ps1` as the way to set them;
  - describe the post-deploy integration step.

  Verify that every `vars.*` in the workflow is documented, either in the README or in `.env.example.ps1`.

## 5. End-to-end verification

- [ ] 5.1 Run `scripts/Sync-Environment.ps1` to push the variables, then trigger the workflow with `workflow_dispatch`. Verify that the pre-deploy tests exclude integration tests, that the integration step passes against the deployed Function App, and that no function key or secret appears unmasked in the logs.
- [ ] 5.2 Re-run `scripts/Initialize-AzureDevOpsTestProject.ps1` and `scripts/Sync-Environment.ps1` once more. Verify that both are no-ops (no new ADO resources, no build queued, no `.env` diff) and that `openspec validate add-api-integration-tests --strict` passes.

## Workflow follow-up

- Archive this change after `add-bicep-github-deployment` is archived, so that the workflow requirements land in order.
