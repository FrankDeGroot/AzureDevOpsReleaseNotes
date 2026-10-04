      $payload = @{
        organization = $env:AZURE_DEVOPS_ORGANIZATION
        project = $env:AZURE_DEVOPS_PROJECT
        projectId = "$env:AZURE_DEVOPS_PROJECT_ID"
        buildId = [int]$env:BUILD_ID
        repositoryId = "$env:REPOSITORY_ID"
      } | ConvertTo-Json

      Invoke-RestMethod -Method Post `
      -Uri "$($env:RELEASE_NOTES_API_URL)?code=$env:FUNCTION_KEY" `
      -Body $payload `
      -ContentType 'application/json'
