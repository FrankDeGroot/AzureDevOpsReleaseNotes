# Local Emulators

Run **Dev Containers: Rebuild Container** to start Azurite and the Linux Cosmos DB NoSQL emulator alongside the workspace. Docker Compose must be available on the host. The Cosmos emulator is a preview and supports gateway mode, which the API already uses. Run the application from the dev container so it can reach both emulators.

The container configures `AzureWebJobsStorage=UseDevelopmentStorage=true` and the API's Cosmos DB settings. A startup hook trusts the Cosmos emulator's HTTPS certificate on every start. The key in [.devcontainer/cosmos-emulator.key](../.devcontainer/cosmos-emulator.key) is public and for local development only.

- Azurite Blob, Queue, and Table endpoints: `http://localhost:10000`, `http://localhost:10001`, and `http://localhost:10002`.
- Cosmos DB endpoint: `https://localhost:8081`.
- Cosmos Data Explorer: `http://localhost:1234`.

Both emulators persist data in Docker named volumes. Rebuilding the container preserves this data; removing the Compose volumes resets it.

[Run the app](run-the-app.md)