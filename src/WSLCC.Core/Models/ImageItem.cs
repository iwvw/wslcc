namespace WSLCC.Core.Models;

public sealed record ImageItem(
    string Repository,
    string Tag,
    string Id,
    string Size,
    string? CreatedAt)
{
    public string FullName => string.IsNullOrEmpty(Tag) || Tag == "<none>" ? Repository : $"{Repository}:{Tag}";
}
