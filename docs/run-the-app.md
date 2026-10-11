# Run the app

The dev container includes the .NET SDK, Azure Functions Core Tools, and Static Web Apps CLI. From the repository root, restore the solution and create the Functions host's local settings file:

```bash
dotnet restore ReleaseNotes.slnx
cp src/Api/local.settings.example.json src/Api/local.settings.json
```

Sign in to Azure CLI so the API can use your developer identity when calling Azure DevOps:

```bash
az login
```

Start each process in its own terminal from the repository root:

```bash
dotnet run --project src/Client --launch-profile http
```

```bash
cd src/Api && dotnet run
```

```bash
swa start http://localhost:5017 --api-devserver-url http://localhost:7071
```

Open `http://localhost:4280`. The Static Web Apps CLI serves the local app and routes `/api` requests to Functions. The API uses Azure CLI credentials in Development; in Azure it continues to use managed identity. Compiling release notes requires an Azure DevOps identity with access to the target organization and project. Application Insights is optional for local runs.