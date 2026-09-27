namespace WSLCC.Core.Models;

public sealed record ImageItem(
    string Repository,
    string Tag,
    string Id,
    string Size,
    string? CreatedAt,
    string? CreatedSince = null,
    string? Digest = null)
{
    public string FullName => string.IsNullOrEmpty(Tag) || Tag == "<none>" ? Repository : $"{Repository}:{Tag}";

    public string DisplayCreated => string.IsNullOrEmpty(CreatedSince) ? CreatedAt ?? "" : CreatedSince;

    public string DisplayDigest => string.IsNullOrEmpty(Digest) || Digest == "<none>" ? "" : Digest;
}
