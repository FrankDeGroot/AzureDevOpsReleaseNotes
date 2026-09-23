namespace Shared;

public sealed class ReleaseNoteDocument
{
    public string Id { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string RepositoryId { get; set; } = string.Empty;
    public int BuildId { get; set; }
    public string BuildNumber { get; set; } = string.Empty;
    public string SourceBranch { get; set; } = string.Empty;
    public DateTime BuildDate { get; set; }
    public string BuildUri { get; set; } = string.Empty;
    public List<WorkItemSummary> WorkItems { get; set; } = [];
    public List<CommitSummary> Commits { get; set; } = [];
}

public sealed class WorkItemSummary
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed class CommitSummary
{
    public string Id { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Url { get; set; } = string.Empty;
}

public sealed class CompileRequest
{
    public string Organization { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public int BuildId { get; set; }
    public string RepositoryId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
}