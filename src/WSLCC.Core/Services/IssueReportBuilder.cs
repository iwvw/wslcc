using System.Text;

namespace WSLCC.Core.Services;

public sealed record IssueReportContext(
    string AppVersion,
    string? WslcVersion,
    IReadOnlyList<string> TunInterfaces,
    IReadOnlyList<string> RecentAudit,
    string? AppLogTail);

public static class IssueReportBuilder
{
    public const string RepoUrl = "https://github.com/iwvw/wslcc";

    private const int MaxTitleLength = 80;
    private const int MaxContextLength = 400;
    private const int MaxDetailLength = 1500;
    private const int MaxBodyLength = 4000;

    public static string BuildUrl(string context, string? detail, IssueReportContext info)
    {
        var title = $"[Bug] {Shorten(context, MaxTitleLength)}";
        var body = BuildIssueBody(context, detail, info);
        return $"{RepoUrl}/issues/new?title={Uri.EscapeDataString(title)}&body={Uri.EscapeDataString(body)}";
    }

    public static string BuildIssueBody(string context, string? detail, IssueReportContext info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## 问题描述");
        sb.AppendLine();
        sb.AppendLine(Shorten(context, MaxContextLength));
        sb.AppendLine();
        sb.AppendLine("## 复现步骤");
        sb.AppendLine();
        sb.AppendLine("1. ");
        sb.AppendLine("2. ");
        sb.AppendLine();
        sb.AppendLine("## 错误信息");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine(string.IsNullOrWhiteSpace(detail) ? "(无)" : Shorten(detail, MaxDetailLength));
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## 环境信息");
        sb.AppendLine();
        sb.AppendLine("```");
        sb.AppendLine(BuildEnvironment(info));
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("<!-- 诊断信息请点击界面上的“复制诊断信息”，粘贴到下方 -->");
        var body = sb.ToString();
        if (body.Length > MaxBodyLength)
            body = body[..MaxBodyLength] + "\n\n...（内容过长已截断）";
        return body;
    }

    public static string BuildDiagnostics(string context, string? detail, IssueReportContext info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("### 问题描述");
        sb.AppendLine(Shorten(context, MaxContextLength));
        sb.AppendLine();
        sb.AppendLine("### 错误信息");
        sb.AppendLine("```");
        sb.AppendLine(string.IsNullOrWhiteSpace(detail) ? "(无)" : detail);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("### 环境信息");
        sb.AppendLine("```");
        sb.AppendLine(BuildEnvironment(info));
        sb.AppendLine("```");
        if (info.RecentAudit.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### 近期操作记录");
            sb.AppendLine("```");
            foreach (var line in info.RecentAudit) sb.AppendLine(line);
            sb.AppendLine("```");
        }
        if (!string.IsNullOrWhiteSpace(info.AppLogTail))
        {
            sb.AppendLine();
            sb.AppendLine("### 应用日志（末尾）");
            sb.AppendLine("```");
            sb.AppendLine(info.AppLogTail);
            sb.AppendLine("```");
        }
        return sb.ToString().TrimEnd();
    }

    private static string BuildEnvironment(IssueReportContext info)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"WSLCC: {info.AppVersion}");
        sb.AppendLine($"OS: {Environment.OSVersion.VersionString} ({Environment.OSVersion.Platform})");
        sb.AppendLine($".NET: {Environment.Version}");
        sb.AppendLine($"wslc: {info.WslcVersion ?? "(未检测到)"}");
        sb.AppendLine(info.TunInterfaces.Count > 0
            ? $"TUN 网卡: {string.Join(", ", info.TunInterfaces)}"
            : "TUN 网卡: 无");
        sb.AppendLine($"时间: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        return sb.ToString().TrimEnd();
    }

    private static string Shorten(string text, int max)
        => string.IsNullOrEmpty(text) || text.Length <= max ? text : text[..max] + "…";
}
