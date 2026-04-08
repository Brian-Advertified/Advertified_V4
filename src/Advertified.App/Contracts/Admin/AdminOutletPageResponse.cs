namespace Advertified.App.Contracts.Admin;

public sealed class AdminOutletPageResponse
{
    public IReadOnlyList<AdminOutletResponse> Items { get; set; } = Array.Empty<AdminOutletResponse>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int IssueCount { get; set; }
    public int StrongCount { get; set; }
    public bool IssuesOnly { get; set; }
    public string SortBy { get; set; } = "priority";
}
