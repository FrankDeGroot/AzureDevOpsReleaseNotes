# Deployment

One Linux App Service web app hosts the existing Blazor WebAssembly viewer and API. [App Service CI/CD](../.github/workflows/app-service.yml) deploys one combined artifact using federated Azure authentication. The viewer/read APIs remain public; compile calls require a tenant/API-audience Entra token through Easy Auth. See [setup](deployment-setup.md), [OIDC prerequisites](configure-azure-oidc-for-github-actions.md), and [workflow details](deploy-with-github-actions.md).

## Cost and prerequisites

The dedicated plan defaults to B1 and incurs a standing charge. Confirm .NET 10 runtime and chosen SKU availability and estimated pricing in the target region before provisioning. Preserve the existing Cosmos free-tier/serverless settings, account name, database/container, and monitoring. Preparing API registration, consent, pipeline caller federation, and the new outbound managed-identity grant is required before cutover.

## Cutover Runbook

These are operator-run actions, not automatic cleanup or authorization to mutate Azure from a coding session.

1. Inventory the deployed SWA/link, Function App, FC1 plan, Functions-only storage and associated roles, Cosmos account, monitoring, caller URLs/keys, and managed-identity grants. Confirm ownership and shared dependencies. Save the previous workflow/artifact reference and define an explicit rollback window; default to seven days after successful caller cutover.
2. Provision the new `app-release-notes-<suffix>` and `asp-release-notes-web-<suffix>` alongside old hosts using incremental Bicep deployment. Inspect a deployment what-if before executing when available. Do not rename or replace Cosmos or shared monitoring. Removing legacy definitions from Bicep does not delete deployed resources.
3. Grant the new web app identity Azure DevOps Basic/Readers access with `Initialize-AzureDevOpsTestProject.ps1 -WebAppName <new-app>`. The new identity does not inherit the Function App's grant. Keep both identities' access during rollback eligibility.
4. Deploy the combined artifact. Verify public root/assets, direct release-detail navigation and refresh, public reads, API and missing-asset 404s, dependency health, authenticated compile/read-back/idempotency, anonymous/key/spoofed-header rejection, invalid-token rejection, and live browser workflows. Stop on failure; old hosts remain available.
5. Change each real pipeline caller to the new `https://<webAppDefaultHostname>/api/releases/compile` URL with an API-audience bearer token. Remove its old query/header key only after the new flow succeeds. Verify the caller's real compile result and viewer data before starting the rollback window.
6. During the window, roll back by restoring old caller URLs/Function-key configuration and the previous deployment reference. Shared Cosmos data stays in place; no database restore is necessary. Record which caller moved and verify its original path still works.
7. After the agreed window and explicit retirement approval, remove only inventoried legacy SWA/link, Function App, old FC1 plan, and exclusively owned Functions storage/roles. Check each resource ID and dependency before deletion. Revoke obsolete identity grants and credentials. Never delete the resource group, Cosmos account/data, or monitoring still used by the new app. Re-run new-host smoke checks afterward.

After retirement, rollback requires reprovisioning the old hosts; changing a URL alone is no longer sufficient. Never use complete-mode deployment, blanket resource-group deletion, or development Compose volume deletion for hosting retirement.

## Local Verification Gate

A clean container rebuild, bootstrap, unit/host tests, Release publish, and stub-browser checks precede cloud cutover. Live local tests additionally need trusted Cosmos and developer Azure DevOps credentials. Tests against an existing warm container do not prove a clean rebuild; record unavailable host Docker access or missing external credentials as blocked, not passed.

[Architecture](architecture.md)