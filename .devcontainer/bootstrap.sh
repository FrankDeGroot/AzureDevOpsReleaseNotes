#!/usr/bin/env bash
set -euo pipefail
trap 'printf "Dev-container bootstrap failed. Resolve the reported step and rerun: bash .devcontainer/bootstrap.sh\n" >&2' ERR

cd "$(dirname "${BASH_SOURCE[0]}")/.."
build_directory="${TMPDIR:-/tmp}/client-browser-tests-build/"

dotnet --version
pwsh --version
az version --query '"azure-cli"' --output tsv
gh --version
npm install -g @fission-ai/openspec@1.14.1
dotnet restore ReleaseNotes.slnx
dotnet build tests/Client.BrowserTests -p:BaseOutputPath="$build_directory"
installer="${build_directory}Debug/net10.0/playwright.ps1"
sudo -n pwsh -NoProfile "$installer" install-deps chromium
pwsh -NoProfile "$installer" install chromium
printf 'Ready. Run: dotnet run --project src/Api\n'