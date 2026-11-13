# Local Emulators

Run **Dev Containers: Rebuild Container** to start Azurite alongside the workspace. Docker Compose must be available on the host. The workspace shares Azurite's network namespace so the Functions host can reach the Blob, Queue, and Table endpoints at `localhost`.

The container configures `AzureWebJobsStorage=UseDevelopmentStorage=true` for the Functions host and `Storage__ConnectionString=UseDevelopmentStorage=true` with `Storage__TableName=releases` for release-note storage. The same release storage settings are in [src/Api/local.settings.json](../src/Api/local.settings.json). Azurite does not require certificate trust or Azure credentials.

The API submits work to the `CompilationQueueName` setting (default `release-note-compilation`) through the same `AzureWebJobsStorage` setting; the Functions host moves messages that fail five total dequeue attempts to the matching `<queue-name>-poison` queue. Production Bicep fixes the setting to `release-note-compilation`; local lifecycle tests can override it with a unique test-owned queue name. The API creates the work queue when needed, and messages do not expire while waiting for processing. Both queues are stored in the Azurite Queue service and persist with the `azurite-data` volume.

- Azurite Blob, Queue, and Table endpoints: `http://localhost:10000`, `http://localhost:10001`, and `http://localhost:10002`.

Azurite persists data in the `azurite-data` Docker volume. Rebuilding the container preserves release records; removing the volume resets local data. The hosted release table starts empty, and local emulator records are never copied to Azure.

[Run the app](run-the-app.md)