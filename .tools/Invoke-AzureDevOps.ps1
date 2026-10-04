Invoke-RestMethod `
    -Method Get `
    -Uri "https://dev.azure.com/$env:AZURE_DEVOPS_ORGANIZATION/_apis/projects?api-version=7.1-preview.1" `
    -Headers @{ Authorization = "Bearer $env:ACCESS_TOKEN" }
