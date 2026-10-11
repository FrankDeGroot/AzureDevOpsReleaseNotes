# Environment configuration

Keep all settings in a `.env` file (`KEY=value` per line) at the repository root. [.env.example.ps1](../.env.example.ps1) lists every supported key with placeholder values. `.env` and `.env.ps1` are gitignored. After editing `.env`, regenerate `.env.ps1` and push the GitHub Actions configuration:

```powershell
./scripts/Sync-Environment.ps1             # writes .env.ps1 and sets GitHub variables and secrets
./scripts/Sync-Environment.ps1 -SkipGitHub # writes .env.ps1 only
. ./.env.ps1                               # load the values into the current PowerShell session
```

[scripts/Sync-Environment.ps1](../scripts/Sync-Environment.ps1) requires `gh auth login`. It sets keys whose names contain `SECRET`, `TOKEN`, `PASSWORD`, or `KEY` as repository secrets and all other keys as repository variables. It skips empty keys and the local-only keys `RELEASE_NOTES_API_URL` and `FUNCTION_KEY`. Use `-Repo owner/name` to target a different repository.

If your `.env` uses the older keys, rename `TENANT_ID` and `CLIENT_ID` to `AZURE_TENANT_ID` and `AZURE_CLIENT_ID`, and remove `CLIENT_SECRET`, `BUILD_ID`, and `REPOSITORY_ID`. The Azure DevOps setup script writes the replacement `AZURE_DEVOPS_*` keys. `RELEASE_NOTES_API_URL` is now the API base URL (for example `http://localhost:7071/api`), not the compile endpoint.