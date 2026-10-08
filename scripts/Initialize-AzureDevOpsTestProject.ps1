#Requires -Version 7.0
<#
.SYNOPSIS
    Idempotently sets up an Azure DevOps project with a build that has commits and work items,
    for the release notes API integration tests.
.DESCRIPTION
    Creates or reuses the project, its default Git repository, a tagged seed work item, a seed commit
    linked to that work item, an agentless YAML pipeline, and a succeeded build whose changes and work
    items are non-empty. Optionally grants the deployed Function App's managed identity read access.
    Writes the resulting AZURE_DEVOPS_* keys to .env. Authenticates with the current Azure CLI sign-in.
.EXAMPLE
    ./scripts/Initialize-AzureDevOpsTestProject.ps1 -Organization my-org
#>
[CmdletBinding()]
param(
    [string]$Organization = $env:AZURE_DEVOPS_ORGANIZATION,
    [string]$Project = ($env:AZURE_DEVOPS_PROJECT ? $env:AZURE_DEVOPS_PROJECT : 'ReleaseNotesIntegration'),
    [string]$FunctionAppName = ($env:AZURE_RESOURCE_SUFFIX ? "func-release-notes-$($env:AZURE_RESOURCE_SUFFIX)" : ''),
    [string]$EnvFile = (Join-Path $PSScriptRoot '..' '.env')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$pipelineName = 'release-notes-integration'
$seedTag = 'release-notes-integration-seed'
$emptyObjectId = '0000000000000000000000000000000000000000'
$apiVersion = 'api-version=7.1'
$previewApiVersion = 'api-version=7.1-preview.1'

if (-not $Organization) {
    throw 'Azure DevOps organization is required. Pass -Organization or set AZURE_DEVOPS_ORGANIZATION.'
}

$token = az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 --query accessToken --output tsv 2>$null
if ($LASTEXITCODE -ne 0 -or -not $token) {
    throw 'Azure CLI is not signed in. Run "az login" and re-run this script.'
}
$headers = @{ Authorization = "Bearer $token" }
$orgUrl = "https://dev.azure.com/$([Uri]::EscapeDataString($Organization))"
$projectUrl = "$orgUrl/$([Uri]::EscapeDataString($Project))"
$vsspsUrl = "https://vssps.dev.azure.com/$([Uri]::EscapeDataString($Organization))"
$vsaexUrl = "https://vsaex.dev.azure.com/$([Uri]::EscapeDataString($Organization))"

function Invoke-Ado {
    param(
        [string]$Method = 'GET',
        [Parameter(Mandatory)][string]$Uri,
        $Body,
        [string]$ContentType = 'application/json',
        [switch]$AllowNotFound
    )
    $request = @{ Method = $Method; Uri = $Uri; Headers = $headers; SkipHttpErrorCheck = $true; StatusCodeVariable = 'status' }
    if ($null -ne $Body) {
        $request.Body = $Body -is [string] ? $Body : ($Body | ConvertTo-Json -Depth 20)
        $request.ContentType = $ContentType
    }
    $response = Invoke-RestMethod @request
    if ($AllowNotFound -and $status -eq 404) { return $null }
    if ($status -ge 400) {
        $detail = $response -is [string] ? $response : ($response | ConvertTo-Json -Depth 5 -Compress)
        throw "Azure DevOps request $Method $Uri failed with HTTP ${status}: $detail"
    }
    $response
}

function Wait-Until {
    param([scriptblock]$Condition, [string]$Description, [int]$TimeoutSeconds = 600)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ($true) {
        $result = & $Condition
        if ($result) { return $result }
        if ((Get-Date) -gt $deadline) { throw "Timed out waiting for $Description." }
        Start-Sleep -Seconds 5
    }
}

function Get-OrCreateProject {
    $existing = Invoke-Ado -Uri "$orgUrl/_apis/projects/$([Uri]::EscapeDataString($Project))?$apiVersion" -AllowNotFound
    if ($existing) {
        Write-Host "Using project $Project ($($existing.id))."
        return $existing
    }

    $process = (Invoke-Ado -Uri "$orgUrl/_apis/process/processes?$apiVersion").value | Where-Object name -eq 'Basic' | Select-Object -First 1
    if (-not $process) { throw 'The Basic process template was not found in the organization.' }
    Write-Host "Creating project $Project."
    $operation = Invoke-Ado -Method POST -Uri "$orgUrl/_apis/projects?$apiVersion" -Body @{
        name         = $Project
        description  = 'Test data for the release notes API integration tests.'
        visibility   = 'private'
        capabilities = @{
            versioncontrol  = @{ sourceControlType = 'Git' }
            processTemplate = @{ templateTypeId = $process.id }
        }
    }
    Wait-Until -Description "project $Project creation" -Condition {
        $state = Invoke-Ado -Uri $operation.url
        if ($state.status -in @('failed', 'cancelled')) { throw "Project creation $($state.status): $($state.detailedMessage)" }
        if ($state.status -eq 'succeeded') { $state }
    } | Out-Null
    Invoke-Ado -Uri "$orgUrl/_apis/projects/$([Uri]::EscapeDataString($Project))?$apiVersion"
}

function Get-OrCreateSeedWorkItem {
    $query = "SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @project AND [System.Tags] CONTAINS '$seedTag' ORDER BY [System.Id]"
    $found = (Invoke-Ado -Method POST -Uri "$projectUrl/_apis/wit/wiql?$apiVersion" -Body @{ query = $query }).workItems
    if ($found) {
        Write-Host "Using seed work item $($found[0].id)."
        return [int]$found[0].id
    }

    $patch = @(
        @{ op = 'add'; path = '/fields/System.Title'; value = 'Release notes integration test seed' }
        @{ op = 'add'; path = '/fields/System.Tags'; value = $seedTag }
    ) | ConvertTo-Json -Depth 5 -AsArray
    $created = Invoke-Ado -Method POST -Uri "$projectUrl/_apis/wit/workitems/`$Task?$apiVersion" -Body $patch -ContentType 'application/json-patch+json'
    Write-Host "Created seed work item $($created.id)."
    [int]$created.id
}

function Get-MainHead([string]$RepositoryId) {
    $refs = (Invoke-Ado -Uri "$projectUrl/_apis/git/repositories/$RepositoryId/refs?filter=heads/main&$apiVersion").value
    $main = $refs | Where-Object name -eq 'refs/heads/main' | Select-Object -First 1
    $main ? $main.objectId : $null
}

function Push-Commit([string]$RepositoryId, [string]$OldObjectId, [string]$Comment, [object[]]$Changes) {
    $push = Invoke-Ado -Method POST -Uri "$projectUrl/_apis/git/repositories/$RepositoryId/pushes?$apiVersion" -Body @{
        refUpdates = @(@{ name = 'refs/heads/main'; oldObjectId = $OldObjectId })
        commits    = @(@{ comment = $Comment; changes = $Changes })
    }
    $commitId = $push.commits[0].commitId
    Write-Host "Pushed commit $commitId."
    $commitId
}

function New-AddChange([string]$Path, [string]$Content) {
    @{ changeType = 'add'; item = @{ path = $Path }; newContent = @{ content = $Content; contentType = 'rawtext' } }
}

function Add-CommitLink([int]$WorkItemId, [string]$ProjectId, [string]$RepositoryId, [string]$CommitId) {
    $url = "vstfs:///Git/Commit/$ProjectId%2F$RepositoryId%2F$CommitId"
    $patch = @(
        @{ op = 'add'; path = '/relations/-'; value = @{ rel = 'ArtifactLink'; url = $url; attributes = @{ name = 'Fixed in Commit' } } }
    ) | ConvertTo-Json -Depth 5 -AsArray

    # Azure Repos may link the commit concurrently from its "#id" mention, which causes revision conflicts.
    foreach ($attempt in 1..5) {
        $workItem = Invoke-Ado -Uri "$projectUrl/_apis/wit/workitems/$WorkItemId`?`$expand=relations&$apiVersion"
        $relations = $workItem.PSObject.Properties['relations'] ? @($workItem.relations) : @()
        if ($relations | Where-Object { $_.rel -eq 'ArtifactLink' -and $_.url -ieq $url }) { return }
        try {
            Invoke-Ado -Method PATCH -Uri "$projectUrl/_apis/wit/workitems/$WorkItemId`?$apiVersion" -Body $patch -ContentType 'application/json-patch+json' | Out-Null
            Write-Host "Linked work item $WorkItemId to commit $CommitId."
            return
        }
        catch {
            if ($_.Exception.Message -notmatch 'HTTP (409|400)' -or $attempt -eq 5) { throw }
            Start-Sleep -Seconds 3
        }
    }
}

function Initialize-Repository($ProjectInfo, $Repository, [int]$WorkItemId) {
    $head = Get-MainHead $Repository.id
    if (-not $head) {
        $pipelineYaml = @'
# Agentless pipeline: no hosted agent parallelism required.
trigger: none
jobs:
- job: seed
  pool: server
  steps:
  - task: Delay@1
    inputs:
      delayForMinutes: '0'
'@
        $head = Push-Commit $Repository.id $emptyObjectId "Seed release notes integration repository #$WorkItemId" @(
            New-AddChange '/README.md' "# $Project`n`nTest data for the release notes API integration tests.`n"
            New-AddChange '/azure-pipelines.yml' $pipelineYaml
        )
    }
    Add-CommitLink $WorkItemId $ProjectInfo.id $Repository.id $head
}

function Get-OrCreatePipeline($Repository) {
    $existing = (Invoke-Ado -Uri "$projectUrl/_apis/pipelines?$apiVersion").value | Where-Object name -eq $pipelineName | Select-Object -First 1
    if ($existing) {
        Write-Host "Using pipeline $pipelineName ($($existing.id))."
        return $existing
    }
    $created = Invoke-Ado -Method POST -Uri "$projectUrl/_apis/pipelines?$apiVersion" -Body @{
        name          = $pipelineName
        folder        = '\'
        configuration = @{
            type       = 'yaml'
            path       = '/azure-pipelines.yml'
            repository = @{ id = $Repository.id; name = $Repository.name; type = 'azureReposGit' }
        }
    }
    Write-Host "Created pipeline $pipelineName ($($created.id))."
    $created
}

function Test-QualifyingBuild([int]$BuildId) {
    $changes = Invoke-Ado -Uri "$projectUrl/_apis/build/builds/$BuildId/changes?$apiVersion"
    $workItems = Invoke-Ado -Uri "$projectUrl/_apis/build/builds/$BuildId/workitems?$apiVersion"
    $changes.count -gt 0 -and $workItems.count -gt 0
}

function Get-OrCreateBuild($ProjectInfo, $Repository, $Pipeline, [int]$WorkItemId) {
    $builds = @((Invoke-Ado -Uri "$projectUrl/_apis/build/builds?definitions=$($Pipeline.id)&statusFilter=completed&resultFilter=succeeded&queryOrder=finishTimeDescending&`$top=10&$apiVersion").value)
    foreach ($build in $builds) {
        if (Test-QualifyingBuild $build.id) {
            Write-Host "Using build $($build.id)."
            return [int]$build.id
        }
    }

    # A rebuild of an already-built commit reports no changes, so push a new commit first.
    $needsCommit = $builds.Count -gt 0
    foreach ($attempt in 1..2) {
        if ($needsCommit) {
            $head = Get-MainHead $Repository.id
            $commitId = Push-Commit $Repository.id $head "Release notes integration change #$WorkItemId" @(
                New-AddChange "/changes/$((Get-Date).ToUniversalTime().ToString('yyyyMMddHHmmss')).md" "Integration test change for #$WorkItemId.`n"
            )
            Add-CommitLink $WorkItemId $ProjectInfo.id $Repository.id $commitId
        }
        $needsCommit = $true

        $run = Invoke-Ado -Method POST -Uri "$projectUrl/_apis/pipelines/$($Pipeline.id)/runs?$apiVersion" -Body @{
            resources = @{ repositories = @{ self = @{ refName = 'refs/heads/main' } } }
        }
        Write-Host "Queued build $($run.id); waiting for completion."
        $completed = Wait-Until -Description "build $($run.id)" -Condition {
            $state = Invoke-Ado -Uri "$projectUrl/_apis/pipelines/$($Pipeline.id)/runs/$($run.id)?$apiVersion"
            if ($state.state -eq 'completed') { $state }
        }
        if ($completed.result -ne 'succeeded') {
            throw "Build $($run.id) finished with result '$($completed.result)'. See $projectUrl/_build/results?buildId=$($run.id)."
        }
        if (Test-QualifyingBuild $run.id) {
            Write-Host "Build $($run.id) has commits and work items."
            return [int]$run.id
        }
        Write-Warning "Build $($run.id) has no associated commits or work items (attempt $attempt of 2)."
    }
    throw 'Could not produce a succeeded build with associated commits and work items.'
}

function Grant-FunctionAppAccess($ProjectInfo) {
    if (-not $FunctionAppName) {
        Write-Warning 'No Function App name (pass -FunctionAppName or set AZURE_RESOURCE_SUFFIX); skipping managed identity access.'
        return
    }
    $principalId = az resource list --name $FunctionAppName --resource-type Microsoft.Web/sites --query '[0].identity.principalId' --output tsv 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $principalId) {
        Write-Warning "Function App '$FunctionAppName' or its managed identity was not found in the current subscription; skipping managed identity access."
        return
    }

    $servicePrincipal = Invoke-Ado -Method POST -Uri "$vsspsUrl/_apis/graph/serviceprincipals?$previewApiVersion" -Body @{ originId = $principalId }
    $storageKey = (Invoke-Ado -Uri "$vsspsUrl/_apis/graph/storagekeys/$($servicePrincipal.descriptor)?$previewApiVersion").value

    $entitlement = Invoke-Ado -Uri "$vsaexUrl/_apis/serviceprincipalentitlements/$storageKey`?$previewApiVersion" -AllowNotFound
    $licenseType = ($entitlement -and $entitlement.PSObject.Properties['accessLevel']) ? $entitlement.accessLevel.accountLicenseType : $null
    if ($licenseType -notin @('express', 'advanced')) {
        $result = Invoke-Ado -Method POST -Uri "$vsaexUrl/_apis/serviceprincipalentitlements?$previewApiVersion" -Body @{
            accessLevel      = @{ accountLicenseType = 'express'; licensingSource = 'account' }
            servicePrincipal = @{ origin = 'aad'; originId = $principalId; subjectKind = 'servicePrincipal' }
        }
        if ($result.PSObject.Properties['isSuccess'] -and -not $result.isSuccess) {
            throw "Failed to grant Basic access to '$FunctionAppName': $($result | ConvertTo-Json -Depth 6 -Compress)"
        }
        Write-Host "Granted Basic access to the '$FunctionAppName' managed identity."
    }

    $scope = (Invoke-Ado -Uri "$vsspsUrl/_apis/graph/descriptors/$($ProjectInfo.id)?$previewApiVersion").value
    $readers = (Invoke-Ado -Uri "$vsspsUrl/_apis/graph/groups?scopeDescriptor=$scope&$previewApiVersion").value | Where-Object displayName -eq 'Readers' | Select-Object -First 1
    if (-not $readers) { throw "Readers group not found in project $Project." }
    $membershipUrl = "$vsspsUrl/_apis/graph/memberships/$($servicePrincipal.descriptor)/$($readers.descriptor)?$previewApiVersion"
    if (-not (Invoke-Ado -Uri $membershipUrl -AllowNotFound)) {
        Invoke-Ado -Method PUT -Uri $membershipUrl | Out-Null
        Write-Host "Added the '$FunctionAppName' managed identity to $Project Readers."
    }
    else {
        Write-Host "The '$FunctionAppName' managed identity already has access to $Project."
    }
}

function Set-EnvValues([string]$Path, [System.Collections.Specialized.OrderedDictionary]$Values) {
    $lines = [System.Collections.Generic.List[string]]::new()
    if (Test-Path $Path) { $lines.AddRange([string[]](Get-Content -Path $Path)) }
    foreach ($key in $Values.Keys) {
        $pattern = "^\s*$([regex]::Escape($key))\s*="
        $index = -1
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match $pattern) { $index = $i; break }
        }
        $entry = "$key=$($Values[$key])"
        if ($index -ge 0) { $lines[$index] = $entry } else { $lines.Add($entry) }
    }
    Set-Content -Path $Path -Value $lines -Encoding utf8
    Write-Host "Updated $Path."
}

$projectInfo = Get-OrCreateProject
$repository = Invoke-Ado -Uri "$projectUrl/_apis/git/repositories/$([Uri]::EscapeDataString($Project))?$apiVersion"
$workItemId = Get-OrCreateSeedWorkItem
Initialize-Repository $projectInfo $repository $workItemId
$pipeline = Get-OrCreatePipeline $repository
$buildId = Get-OrCreateBuild $projectInfo $repository $pipeline $workItemId
Grant-FunctionAppAccess $projectInfo

Set-EnvValues $EnvFile ([ordered]@{
        AZURE_DEVOPS_ORGANIZATION  = $Organization
        AZURE_DEVOPS_PROJECT       = $Project
        AZURE_DEVOPS_PROJECT_ID    = $projectInfo.id
        AZURE_DEVOPS_REPOSITORY_ID = $repository.id
        AZURE_DEVOPS_BUILD_ID      = $buildId
    })
Write-Host 'Run scripts/Sync-Environment.ps1 to regenerate .env.ps1 and update GitHub variables.'
