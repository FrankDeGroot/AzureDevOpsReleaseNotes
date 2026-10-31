# Environment configuration

Keep all settings in a `.env` file (`KEY=value` per line) at the repository root. [.env.example.ps1](../.env.example.ps1) lists every supported key with placeholder values. `.env` and `.env.ps1` are gitignored. After editing `.env`, regenerate `.env.ps1` and push the GitHub Actions configuration:

```powershell
./scripts/Sync-Environment.ps1             # writes .env.ps1 and sets GitHub variables and secrets
./scripts/Sync-Environment.ps1 -SkipGitHub # writes .env.ps1 only
. ./.env.ps1                               # load the values into the current PowerShell session
```

[scripts/Sync-Environment.ps1](../scripts/Sync-Environment.ps1) requires `gh auth login` only when syncing GitHub. It sets keys containing `SECRET`, `TOKEN`, `PASSWORD`, or `KEY` as secrets and other keys as variables. It skips empty keys and local-only `RELEASE_NOTES_API_URL`, `RELEASE_NOTES_API_TOKEN`, and obsolete `FUNCTION_KEY`. Use `-Repo owner/name` for a different repository. Old Function keys are ignored and no longer authorize compilation.

`AZURE_TENANT_ID` identifies the tenant. `AZURE_CLIENT_ID` identifies the federated deployment identity. `RELEASE_NOTES_AUTH_CLIENT_ID` identifies the separate API registration, and `RELEASE_NOTES_AUTH_AUDIENCE` is its accepted audience (typically `api://<API-client-id>`). Configure caller application permissions/consent before deployed tests. CI mints short-lived bearer tokens at runtime; do not store them as GitHub settings or expose them to the client.

Local Development compilation needs no inbound token or hosted Easy Auth settings. Its outbound Azure DevOps access still needs `az login`; live tests need the `AZURE_DEVOPS_*` values and ready/trusted Cosmos. See [Local emulators](local-emulators.md) for credential-free bootstrap and [Integration tests](integration-tests.md) for the live path.

If your `.env` uses the older keys, rename `TENANT_ID` and `CLIENT_ID` to `AZURE_TENANT_ID` and `AZURE_CLIENT_ID`, and remove `CLIENT_SECRET`, `BUILD_ID`, and `REPOSITORY_ID`. The Azure DevOps setup script writes the replacement `AZURE_DEVOPS_*` keys. `RELEASE_NOTES_API_URL` is now the API base URL (for example `http://localhost:7071/api`), not the compile endpoint.