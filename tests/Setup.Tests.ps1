#Requires -Version 7.0
$ErrorActionPreference = 'Stop'
$temporaryDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path $temporaryDirectory | Out-Null
$mockCalls = [System.Collections.Generic.List[string]]::new()
function gh {
    $mockCalls.Add(($args -join ' '))
    $global:LASTEXITCODE = 0
}
try {
    $envFile = Join-Path $temporaryDirectory '.env'
    Set-Content $envFile @(
        'RELEASE_NOTES_API_URL=http://localhost:7071/api'
        'RELEASE_NOTES_API_TOKEN=synthetic-local-token'
        'FUNCTION_KEY=synthetic-obsolete-key'
        'AZURE_DEVOPS_BUILD_ID=42'
        'CLIENT_SECRET=synthetic-client-secret'
    )
    $output = & (Join-Path $PSScriptRoot '../scripts/Sync-Environment.ps1') -EnvFile $envFile -Repo 'test/repository' *>&1 | Out-String
    if ($mockCalls.Count -ne 3) { throw "Expected auth and two remote updates, got $($mockCalls.Count)." }
    if (-not ($mockCalls | Where-Object { $_ -like 'variable set AZURE_DEVOPS_BUILD_ID*' })) { throw 'Variable sync missing.' }
    if (-not ($mockCalls | Where-Object { $_ -like 'secret set CLIENT_SECRET*' })) { throw 'Secret sync missing.' }
    if ($mockCalls -match 'RELEASE_NOTES_API|FUNCTION_KEY') { throw 'A local-only key was sent to GitHub.' }
    if ($output -match 'synthetic-') { throw 'A credential value was printed.' }
    $generated = Get-Content (Join-Path $temporaryDirectory '.env.ps1') -Raw
    if (-not $generated.Contains('synthetic-local-token')) { throw 'Local token was not retained locally.' }
    $mockCalls.Clear()
    & (Join-Path $PSScriptRoot '../scripts/Sync-Environment.ps1') -EnvFile $envFile -SkipGitHub | Out-Null
    if ($mockCalls.Count -ne 0) { throw 'Local-only mode called GitHub.' }
    Write-Host 'Environment sync checks passed (no live GitHub calls).'
}
finally {
    Remove-Item $temporaryDirectory -Recurse -Force
}

$parseErrors = $null
$tokens = $null
$setupAst = [System.Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot '../scripts/Initialize-AzureDevOpsTestProject.ps1'), [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count) { throw 'Azure DevOps setup script has parser errors.' }
$grant = $setupAst.Find({ param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Grant-WebAppAccess' }, $true)
if (-not $grant) { throw 'Web app identity grant helper was not found.' }
. ([scriptblock]::Create($grant.Extent.Text))

$WebAppName = 'app-release-notes-test'
$Project = 'test-project'
$vsspsUrl = 'https://example.test/graph'
$vsaexUrl = 'https://example.test/entitlements'
$previewApiVersion = 'api-version=test'
$identityAvailable = $true
$membershipPresent = $false
$adoCalls = [System.Collections.Generic.List[string]]::new()
function az {
    $global:LASTEXITCODE = 0
    if ($identityAvailable) { '11111111-1111-1111-1111-111111111111' }
}
function Invoke-Ado {
    param($Method = 'GET', $Uri, $Body, [switch]$AllowNotFound)
    $adoCalls.Add("$Method $Uri")
    if ($Uri -match '/serviceprincipals\?') { return [pscustomobject]@{ descriptor = 'principal' } }
    if ($Uri -match '/storagekeys/') { return [pscustomobject]@{ value = 'storage-key' } }
    if ($Uri -match '/serviceprincipalentitlements/') { return [pscustomobject]@{ accessLevel = [pscustomobject]@{ accountLicenseType = 'express' } } }
    if ($Uri -match '/descriptors/') { return [pscustomobject]@{ value = 'scope' } }
    if ($Uri -match '/groups\?') { return [pscustomobject]@{ value = @([pscustomobject]@{ displayName = 'Readers'; descriptor = 'readers' }) } }
    if ($Uri -match '/memberships/') {
        if ($Method -eq 'PUT') { $script:membershipPresent = $true; return @{} }
        if ($membershipPresent) { return @{} }
        return $null
    }
    throw "Unexpected mocked request: $Method $Uri"
}

Grant-WebAppAccess ([pscustomobject]@{ id = 'project-id' })
if (@($adoCalls | Where-Object { $_ -like 'PUT *' }).Count -ne 1) { throw 'Initial Readers grant was not performed.' }
$adoCalls.Clear()
Grant-WebAppAccess ([pscustomobject]@{ id = 'project-id' })
if ($adoCalls | Where-Object { $_ -like 'PUT *' }) { throw 'Repeated Readers grant was not idempotent.' }
$identityAvailable = $false
$adoCalls.Clear()
$missing = Grant-WebAppAccess ([pscustomobject]@{ id = 'project-id' }) 3>&1 | Out-String
if ($adoCalls.Count -ne 0 -or $missing -notmatch 'was not found') { throw 'Missing identity handling failed.' }
Write-Host 'Web app identity checks passed (no live Azure or Azure DevOps calls).'