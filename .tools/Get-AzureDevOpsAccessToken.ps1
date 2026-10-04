$body = @{
    client_id     = $env:CLIENT_ID
    client_secret = $env:CLIENT_SECRET
    scope         = "499b84ac-1321-427f-aa17-267ca6975798/.default"
    grant_type    = "client_credentials"
}

$tokenResponse = Invoke-RestMethod `
    -Method Post `
    -Uri "https://login.microsoftonline.com/$env:TENANT_ID/oauth2/v2.0/token" `
    -Body $body

$env:ACCESS_TOKEN = $tokenResponse.access_token