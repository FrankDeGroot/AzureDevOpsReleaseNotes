# Deployment

The application deploys to **Azure Static Web Apps** via the GitHub Actions workflow located at [.github/workflows/azure-static-web-apps.yml](../.github/workflows/azure-static-web-apps.yml).

## Compilation queues

Bicep provisions `release-note-compilation` and `release-note-compilation-poison` in the existing Functions StorageV2 account. The Function App uses its system-assigned identity and `AzureWebJobsStorage__queueServiceUri`; the existing Storage Queue Data Contributor assignment grants access without storage account keys. Work messages do not expire, are removed after successful persistence, and move to the poison queue after five failed dequeue attempts. The queues and their messages are retained across deployments and rollbacks.

Monitor the approximate work-queue depth and the insertion time of its oldest visible message, along with poison-queue depth and correlated worker errors. Use Azure Storage Explorer or equivalent approved tooling to peek at poison messages without consuming them. After fixing the cause, validate and replay messages selectively through the authorized compile endpoint or queue tooling; keep the poison copy until the release is confirmed in Table Storage. Do not automate poison consumption or purge queues during deployment.

Before a rollback to a package without the queue trigger, record the work and poison queue depths and oldest message age. Keep both queues, the storage account, and the role assignment. After restoring a queue-capable package, reconcile the accepted-but-unprocessed work explicitly; do not assume the synchronous package consumed the backlog or automatically replay it.

[Architecture](architecture.md)