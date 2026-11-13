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

## Submit and poll for a release

Set `FUNCTION_KEY` to a local Functions key and replace the sample organization, project, repository, and build ID with a real build the configured identity can read. A fresh build ID should not already have a release record.

```bash
API_ORIGIN="http://localhost:7071"
API_BASE_URL="$API_ORIGIN/api"
response_file="$(mktemp)"
trap 'rm -f "$response_file"' EXIT

acknowledgment=$(curl --fail-with-body --silent --show-error \
	-X POST "$API_BASE_URL/releases/compile?code=$FUNCTION_KEY" \
	-H 'Content-Type: application/json' \
	-d '{"organization":"contoso","project":"release-notes","projectId":"project-id","repositoryId":"repository-id","buildId":42}')
release_url=$(jq -er '.releaseUrl' <<< "$acknowledgment")
deadline=$((SECONDS + 120))
available=false

while (( SECONDS < deadline )); do
	remaining=$((deadline - SECONDS))
	if ! status=$(curl --silent --show-error --max-time "$remaining" \
			-o "$response_file" -w '%{http_code}' "$API_ORIGIN$release_url"); then
		cat "$response_file" >&2
		exit 1
	fi

	case "$status" in
		200) cat "$response_file"; available=true; break ;;
		404) sleep 1 ;;
		*) cat "$response_file" >&2; exit 1 ;;
	esac
done

if [[ "$available" != true ]]; then
	printf 'Timed out waiting for release %s; inspect processing and poison-queue logs.\n' \
		"$(jq -r '.id' <<< "$acknowledgment")" >&2
	cat "$response_file" >&2
	exit 1
fi
```

The acknowledgment contains `id`, `projectId`, and `releaseUrl`; its `Location` header matches that origin-relative URL. HTTP 202 means the message was accepted, not that compilation succeeded. Poll only 404 responses and stop after 120 seconds; treat other HTTP responses or invalid JSON as failures. If the release already exists when recompilation starts, GET can return that older record immediately, so use a fresh build ID when verifying background processing.