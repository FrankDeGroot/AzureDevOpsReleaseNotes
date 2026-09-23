using Shared;

namespace Api.Services;

public sealed class ReleaseNoteCompiler(AzureDevOpsClient devOpsClient, IReleaseNoteStore store)
{
    public async Task<ReleaseNoteDocument> CompileAsync(CompileRequest request, CancellationToken cancellationToken)
    {
        var build = await devOpsClient.GetBuildAsync(request, cancellationToken);
        var commits = await devOpsClient.GetChangesAsync(request, cancellationToken);
        var workItems = await devOpsClient.GetWorkItemsAsync(request, cancellationToken);
        var document = new ReleaseNoteDocument
        {
            Id = $"{request.ProjectId}:{build.Id}",
            Project = request.Project,
            ProjectId = request.ProjectId,
            RepositoryId = request.RepositoryId,
            BuildId = build.Id,
            BuildNumber = build.BuildNumber,
            SourceBranch = build.SourceBranch,
            BuildDate = build.BuildDate,
            BuildUri = build.Uri,
            Commits = commits.ToList(),
            WorkItems = workItems.ToList()
        };
        return await store.UpsertAsync(document, cancellationToken);
    }
}