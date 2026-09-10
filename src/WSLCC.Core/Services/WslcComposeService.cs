using WSLCC.Core.Cli;
using WSLCC.Core.Models;
using WSLCC.Data;
using YamlDotNet.RepresentationModel;

namespace WSLCC.Core.Services;

public sealed record ComposeServiceDefinition(
    string Name,
    string Image,
    string ContainerName,
    IReadOnlyList<string> Ports,
    IReadOnlyList<string> Environment,
    IReadOnlyList<string> Volumes,
    IReadOnlyList<string> DependsOn,
    IReadOnlyList<string>? Command,
    string? Restart);

public sealed record ComposeProjectInfo(
    string Name,
    string FilePath,
    IReadOnlyList<ComposeServiceDefinition> Services);

public sealed record ComposeDeploymentResult(string Service, string ContainerName, bool Success, string? Error);

public sealed record ComposeServiceStatus(
    string Service,
    string ContainerName,
    string Image,
    string State,
    string Status,
    bool IsRunning)
{
    public bool IsMissing => State.Equals("missing", StringComparison.OrdinalIgnoreCase);
}

public sealed record ComposeProjectStatus(
    string Name,
    string? FilePath,
    IReadOnlyList<ComposeServiceStatus> Services)
{
    public int RunningCount => Services.Count(s => s.IsRunning);
}

public interface IWslcComposeService
{
    Task<ComposeProjectInfo> LoadProjectAsync(string composeFilePath, CancellationToken ct = default);
    Task<IReadOnlyList<ComposeDeploymentResult>> DeployAsync(string composeFilePath, IProgress<string>? progress = null, string? registryMirror = null, CancellationToken ct = default);
    Task<IReadOnlyList<ComposeDeploymentResult>> DeployFromContentAsync(string content, string? composeFilePath, string? overrideProjectName = null, IProgress<string>? progress = null, string? registryMirror = null, CancellationToken ct = default);
    Task<IReadOnlyList<string>> StopAsync(string composeFilePath, IProgress<string>? progress = null, CancellationToken ct = default);
    Task<IReadOnlyList<ComposeProjectStatus>> ListProjectsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> StopProjectAsync(string projectName, IProgress<string>? progress = null, CancellationToken ct = default);
    Task<IReadOnlyList<string>> StartProjectAsync(string projectName, IProgress<string>? progress = null, CancellationToken ct = default);
    Task<IReadOnlyList<string>> DeleteProjectAsync(string projectName, IProgress<string>? progress = null, CancellationToken ct = default);
    Task<IReadOnlyList<string>> RestartProjectAsync(string projectName, IProgress<string>? progress = null, CancellationToken ct = default);
    Task<IReadOnlyList<ComposeDeploymentResult>> RebuildProjectAsync(string projectName, string? registryMirror = null, IProgress<string>? progress = null, CancellationToken ct = default);
    Task MigrateLegacyComposeProjectsAsync(CancellationToken ct = default);
}

public sealed class WslcComposeService : IWslcComposeService
{
    private const string LabelProject = "wslcc.compose.project";
    private const string LabelService = "wslcc.compose.service";

    private readonly IWslcContainerService _containers;
    private readonly IWslcAuditService _audit;
    private readonly ComposeDeploymentRepository _deployments;

    public WslcComposeService(
        IWslcContainerService containers, IWslcAuditService audit, ComposeDeploymentRepository deployments)
    {
        _containers = containers;
        _audit = audit;
        _deployments = deployments;
    }

    public Task<ComposeProjectInfo> LoadProjectAsync(string composeFilePath, CancellationToken ct = default)
    {
        var services = ParseFile(composeFilePath, out var projectName);
        return Task.FromResult(new ComposeProjectInfo(projectName, composeFilePath, services));
    }

    public async Task MigrateLegacyComposeProjectsAsync(CancellationToken ct = default)
    {
        var legacy = WslcAppData.LegacyComposeDirectory();
        if (!Directory.Exists(legacy))
            return;

        var runningProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var containers = await _containers.ListAsync(ct).ConfigureAwait(false);
            var entries = await _deployments.QueryAsync().ConfigureAwait(false);
            foreach (var group in entries.GroupBy(e => e.ProjectName, StringComparer.OrdinalIgnoreCase))
            {
                var hasRunning = group.Select(e => e.ContainerName)
                    .Intersect(containers.Where(c => c.State.Equals("running", StringComparison.OrdinalIgnoreCase)).Select(c => c.Name),
                        StringComparer.OrdinalIgnoreCase)
                    .Any();
                if (hasRunning)
                    runningProjects.Add(group.Key);
            }
        }
        catch
        {
        }

        foreach (var projDir in Directory.GetDirectories(legacy))
        {
            var name = Path.GetFileName(projDir);
            var target = Path.Combine(WslcAppData.ResolveDirectory(), "compose", name);
            if (Directory.Exists(target) || runningProjects.Contains(name))
                continue;
            try
            {
                Directory.Move(projDir, target);
                var filePath = Path.Combine(target, "docker-compose.yml");
                if (File.Exists(filePath))
                    await SafeRecordAsync(() => _deployments.UpdateComposeFilePathAsync(name, filePath)).ConfigureAwait(false);
            }
            catch
            {
            }
        }
    }

    public async Task<IReadOnlyList<ComposeDeploymentResult>> DeployAsync(
        string composeFilePath, IProgress<string>? progress = null, string? registryMirror = null, CancellationToken ct = default)
    {
        if (!File.Exists(composeFilePath))
            throw new FileNotFoundException("Compose 文件不存在。", composeFilePath);

        var content = await File.ReadAllTextAsync(composeFilePath, ct).ConfigureAwait(false);
        var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(composeFilePath))!;
        var services = ParseContent(content, out var projectName, baseDirectory);
        var persistentPath = PersistComposeFile(projectName, content);

        var ordered = TopologicalSort(services);
        var results = new List<ComposeDeploymentResult>();

        foreach (var service in ordered)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"部署 {service.ContainerName}（{ApplyMirror(service.Image, registryMirror)}）...");
            var started = DateTimeOffset.Now;
            try
            {
                var options = ToCreateOptions(service, baseDirectory, projectName, registryMirror);
                await _containers.RunAsync(options, progress, ct).ConfigureAwait(false);
                await SafeRecordAsync(() => _deployments.AddAsync(projectName, service.Name, service.ContainerName, persistentPath)).ConfigureAwait(false);
                await SafeRecordAsync(() => _audit.RecordAsync("compose", "deploy", $"{projectName}/{service.Name}", true, durationMs: ElapsedMs(started))).ConfigureAwait(false);
                var restartNote = string.IsNullOrEmpty(service.Restart) ? string.Empty : $"（restart 策略 '{service.Restart}' 当前 wslc 不支持，未应用）";
                results.Add(new ComposeDeploymentResult(service.Name, service.ContainerName, true, restartNote));
                progress?.Report($"已部署 {service.ContainerName} {restartNote}".Trim());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await SafeRecordAsync(() => _audit.RecordAsync("compose", "deploy", $"{projectName}/{service.Name}", false, ex.Message, ElapsedMs(started))).ConfigureAwait(false);
                results.Add(new ComposeDeploymentResult(service.Name, service.ContainerName, false, ex.Message));
            }
        }
        return results;
    }

    public async Task<IReadOnlyList<ComposeDeploymentResult>> DeployFromContentAsync(
        string content, string? composeFilePath, string? overrideProjectName = null, IProgress<string>? progress = null, string? registryMirror = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidDataException("Compose 内容为空。");

        var baseDirectory = string.IsNullOrEmpty(composeFilePath)
            ? Path.GetTempPath()
            : Path.GetDirectoryName(Path.GetFullPath(composeFilePath))!;
        var services = ParseContent(content, out var projectName, baseDirectory);
        if (!string.IsNullOrWhiteSpace(overrideProjectName))
            projectName = SanitizeProjectName(overrideProjectName.Trim());
        composeFilePath = PersistComposeFile(projectName, content);

        var ordered = TopologicalSort(services);
        var results = new List<ComposeDeploymentResult>();

        foreach (var service in ordered)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"部署 {service.ContainerName}（{ApplyMirror(service.Image, registryMirror)}）...");
            var started = DateTimeOffset.Now;
            try
            {
                var options = ToCreateOptions(service, baseDirectory, projectName, registryMirror);
                await _containers.RunAsync(options, progress, ct).ConfigureAwait(false);
                await SafeRecordAsync(() => _deployments.AddAsync(projectName, service.Name, service.ContainerName, composeFilePath)).ConfigureAwait(false);
                await SafeRecordAsync(() => _audit.RecordAsync("compose", "deploy", $"{projectName}/{service.Name}", true, durationMs: ElapsedMs(started))).ConfigureAwait(false);
                var restartNote = string.IsNullOrEmpty(service.Restart) ? string.Empty : $"（restart 策略 '{service.Restart}' 当前 wslc 不支持，未应用）";
                results.Add(new ComposeDeploymentResult(service.Name, service.ContainerName, true, restartNote));
                progress?.Report($"已部署 {service.ContainerName} {restartNote}".Trim());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await SafeRecordAsync(() => _audit.RecordAsync("compose", "deploy", $"{projectName}/{service.Name}", false, ex.Message, ElapsedMs(started))).ConfigureAwait(false);
                results.Add(new ComposeDeploymentResult(service.Name, service.ContainerName, false, ex.Message));
            }
        }
        return results;
    }

    private static string PersistComposeFile(string projectName, string content)
    {
        projectName = SanitizeProjectName(projectName);
        var dir = Path.Combine(WslcAppData.ResolveDirectory(), "compose", projectName);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "docker-compose.yml");
        File.WriteAllText(path, content);
        return path;
    }

    private static string SanitizeProjectName(string projectName)
    {
        var name = (projectName ?? string.Empty).Trim();
        if (name.Length == 0 || name.Any(c => !char.IsLetterOrDigit(c) && c is not '-' and not '_'))
            throw new InvalidDataException($"项目名「{projectName}」含非法字符，仅允许字母、数字、- 和 _。");
        return name;
    }

    public async Task<IReadOnlyList<ComposeDeploymentResult>> RebuildProjectAsync(
        string projectName, string? registryMirror = null, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var entries = await ProjectEntriesAsync(projectName).ConfigureAwait(false);
        var results = new List<ComposeDeploymentResult>();

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(entry.ComposeFilePath) || !File.Exists(entry.ComposeFilePath))
            {
                results.Add(new ComposeDeploymentResult(entry.ServiceName, entry.ContainerName, false, "compose 文件缺失，无法重建"));
                continue;
            }

            progress?.Report($"强制重建 {entry.ContainerName} ...");
            try
            {
                await _containers.StopAsync(entry.ContainerName, ct).ConfigureAwait(false);
            }
            catch
            {
            }
            try
            {
                await _containers.RemoveAsync(entry.ContainerName, force: true, ct).ConfigureAwait(false);
            }
            catch
            {
            }

            var services = ParseFile(entry.ComposeFilePath, out _);
            var service = services.FirstOrDefault(s => s.Name == entry.ServiceName && s.ContainerName == entry.ContainerName)
                ?? services.FirstOrDefault(s => s.Name == entry.ServiceName)
                ?? services.FirstOrDefault(s => s.ContainerName == entry.ContainerName);
            if (service is null)
            {
                results.Add(new ComposeDeploymentResult(entry.ServiceName, entry.ContainerName, false, "compose 文件中找不到该服务"));
                continue;
            }

            var directory = Path.GetDirectoryName(Path.GetFullPath(entry.ComposeFilePath))!;
            var started = DateTimeOffset.Now;
            try
            {
                var options = ToCreateOptions(service, directory, projectName, registryMirror);
                await _containers.RunAsync(options, progress, ct).ConfigureAwait(false);
                await SafeRecordAsync(() => _audit.RecordAsync("compose", "rebuild", $"{projectName}/{service.Name}", true, durationMs: ElapsedMs(started))).ConfigureAwait(false);
                results.Add(new ComposeDeploymentResult(service.Name, service.ContainerName, true, null));
                progress?.Report($"已重建 {entry.ContainerName}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await SafeRecordAsync(() => _audit.RecordAsync("compose", "rebuild", $"{projectName}/{service.Name}", false, ex.Message, ElapsedMs(started))).ConfigureAwait(false);
                results.Add(new ComposeDeploymentResult(service.Name, entry.ContainerName, false, ex.Message));
            }
        }
        return results;
    }

    public async Task<IReadOnlyList<string>> StopAsync(
        string composeFilePath, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var services = ParseFile(composeFilePath, out var projectName);
        var entries = await ProjectEntriesAsync(projectName).ConfigureAwait(false);
        var targets = entries.Count > 0
            ? entries.Select(e => e.ContainerName).ToList()
            : services.Select(s => s.ContainerName).ToList();
        var stopped = new List<string>();
        foreach (var name in targets)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"下线 {name} ...");
            stopped.AddRange(await StopContainerAsync(name, progress, ct).ConfigureAwait(false));
        }
        await _deployments.ClearProjectAsync(projectName).ConfigureAwait(false);
        return stopped;
    }

    public async Task<IReadOnlyList<ComposeProjectStatus>> ListProjectsAsync(CancellationToken ct = default)
    {
        var projectNames = await _deployments.GetProjectNamesAsync().ConfigureAwait(false);
        if (projectNames.Count == 0) return Array.Empty<ComposeProjectStatus>();

        var containers = await _containers.ListAsync().ConfigureAwait(false);
        var byName = containers.ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase);

        var allEntries = await _deployments.QueryAsync().ConfigureAwait(false);
        var projects = new List<ComposeProjectStatus>();
        foreach (var project in projectNames)
        {
            var entries = allEntries.Where(e => e.ProjectName == project).ToList();
            var services = entries.Select(e =>
            {
                var container = byName.TryGetValue(e.ContainerName, out var c) ? c : null;
                return new ComposeServiceStatus(
                    e.ServiceName,
                    e.ContainerName,
                    container?.Image ?? "-",
                    container?.State ?? "missing",
                    container?.Status ?? "容器不存在（已被手动删除）",
                    container?.State.Equals("running", StringComparison.OrdinalIgnoreCase) == true);
            }).ToList();
            var filePath = entries.Select(e => e.ComposeFilePath).FirstOrDefault(f => !string.IsNullOrEmpty(f));
            projects.Add(new ComposeProjectStatus(project, filePath, services));
        }
        return projects;
    }

    public async Task<IReadOnlyList<string>> StopProjectAsync(
        string projectName, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var entries = await ProjectEntriesAsync(projectName).ConfigureAwait(false);
        var stopped = new List<string>();
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"停止 {entry.ContainerName} ...");
            try
            {
                await _containers.StopAsync(entry.ContainerName, ct).ConfigureAwait(false);
                stopped.Add(entry.ContainerName);
                progress?.Report($"已停止 {entry.ContainerName}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (IsContainerMissing(ex))
                {
                    stopped.Add(entry.ContainerName);
                    progress?.Report($"已停止 {entry.ContainerName}（容器不存在/未运行）");
                }
                else
                {
                    progress?.Report($"停止 {entry.ContainerName} 失败：{ex.Message}");
                }
            }
        }
        return stopped;
    }

    public async Task<IReadOnlyList<string>> DeleteProjectAsync(
        string projectName, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var entries = await ProjectEntriesAsync(projectName).ConfigureAwait(false);
        var deleted = new List<string>();
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"删除 {entry.ContainerName} ...");
            try
            {
                await _containers.StopAsync(entry.ContainerName, ct).ConfigureAwait(false);
            }
            catch
            {
            }
            try
            {
                await _containers.RemoveAsync(entry.ContainerName, force: true, ct).ConfigureAwait(false);
                deleted.Add(entry.ContainerName);
                progress?.Report($"已删除 {entry.ContainerName}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (IsContainerMissing(ex))
                {
                    deleted.Add(entry.ContainerName);
                    progress?.Report($"已删除 {entry.ContainerName}（容器不存在）");
                }
                else
                {
                    progress?.Report($"删除 {entry.ContainerName} 失败：{ex.Message}");
                }
            }
        }
        await _deployments.ClearProjectAsync(projectName).ConfigureAwait(false);
        return deleted;
    }

    public async Task<IReadOnlyList<string>> StartProjectAsync(
        string projectName, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var entries = await ProjectEntriesAsync(projectName).ConfigureAwait(false);
        var started = new List<string>();
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"启动 {entry.ContainerName} ...");
            try
            {
                await _containers.StartAsync(entry.ContainerName, ct).ConfigureAwait(false);
                started.Add(entry.ContainerName);
                progress?.Report($"已启动 {entry.ContainerName}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (IsContainerMissing(ex))
                {
                    progress?.Report($"跳过 {entry.ContainerName}（容器不存在）");
                }
                else
                {
                    progress?.Report($"启动 {entry.ContainerName} 失败：{ex.Message}");
                }
            }
        }
        return started;
    }

    public async Task<IReadOnlyList<string>> RestartProjectAsync(
        string projectName, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var entries = await ProjectEntriesAsync(projectName).ConfigureAwait(false);
        var restarted = new List<string>();
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report($"重启 {entry.ContainerName} ...");
            try
            {
                await _containers.RestartAsync(entry.ContainerName, ct).ConfigureAwait(false);
                restarted.Add(entry.ContainerName);
                progress?.Report($"已重启 {entry.ContainerName}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException && IsContainerMissing(ex))
            {
                progress?.Report($"{entry.ContainerName} 未运行，改为启动 ...");
                try
                {
                    await _containers.StartAsync(entry.ContainerName, ct).ConfigureAwait(false);
                    restarted.Add(entry.ContainerName);
                    progress?.Report($"已启动 {entry.ContainerName}");
                }
                catch (Exception ex2) when (ex2 is not OperationCanceledException)
                {
                    if (IsContainerMissing(ex2))
                    {
                        progress?.Report($"跳过 {entry.ContainerName}（容器不存在）");
                    }
                    else
                    {
                        progress?.Report($"启动 {entry.ContainerName} 失败：{ex2.Message}");
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                progress?.Report($"重启 {entry.ContainerName} 失败：{ex.Message}");
            }
        }
        return restarted;
    }

    private async Task<IReadOnlyList<ComposeDeploymentEntry>> ProjectEntriesAsync(string projectName)
        => (await _deployments.QueryAsync().ConfigureAwait(false))
            .Where(e => e.ProjectName == projectName)
            .ToList();

    private static bool IsContainerMissing(Exception ex)
    {
        var message = ex.Message;
        return message.Contains("CONTAINER_NOT_FOUND", StringComparison.OrdinalIgnoreCase)
            || message.Contains("CONTAINER_NOT_RUNNING", StringComparison.OrdinalIgnoreCase)
            || message.Contains("找不到容器", StringComparison.OrdinalIgnoreCase)
            || message.Contains("容器不存在", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<string>> StopContainerAsync(string name, IProgress<string>? progress, CancellationToken ct)
    {
        try
        {
            await _containers.StopAsync(name, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
        }
        try
        {
            await _containers.RemoveAsync(name, force: true, ct).ConfigureAwait(false);
            progress?.Report($"已删除 {name}");
            await SafeRecordAsync(() => _audit.RecordAsync("compose", "down", name, true)).ConfigureAwait(false);
            return new[] { name };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            progress?.Report($"删除 {name} 失败：{ex.Message}");
            await SafeRecordAsync(() => _audit.RecordAsync("compose", "down", name, false, ex.Message)).ConfigureAwait(false);
            return Array.Empty<string>();
        }
    }

    private IReadOnlyList<ComposeServiceDefinition> ParseContent(string content, out string projectName, string baseDirectory)
    {
        var yaml = new YamlStream();
        using var reader = new StringReader(content);
        yaml.Load(reader);

        if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
            throw new InvalidDataException("Compose 内容格式无效。");

        var services = ParseServices(root);
        var directoryName = Path.GetFileName(baseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var inTemp = baseDirectory.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase);
        projectName = GetScalar(root, "name")
            ?? (inTemp && services.Count > 0 ? $"{services[0].Name}-compose" : directoryName);
        if (string.IsNullOrEmpty(projectName) || projectName.Equals("wslcc-compose", StringComparison.OrdinalIgnoreCase))
            projectName = "compose";

        return services;
    }

    private IReadOnlyList<ComposeServiceDefinition> ParseFile(string composeFilePath, out string projectName)
    {
        if (!File.Exists(composeFilePath))
            throw new FileNotFoundException("Compose 文件不存在。", composeFilePath);

        var yaml = new YamlStream();
        using var reader = new StreamReader(composeFilePath);
        yaml.Load(reader);

        if (yaml.Documents.Count == 0 || yaml.Documents[0].RootNode is not YamlMappingNode root)
            throw new InvalidDataException("Compose 文件格式无效。");

        projectName = GetScalar(root, "name")
            ?? Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(composeFilePath))!)
            ?? "compose";

        return ParseServices(root);
    }

    private static IReadOnlyList<ComposeServiceDefinition> ParseServices(YamlMappingNode root)
    {
        if (!root.Children.TryGetValue(new YamlScalarNode("services"), out var servicesNode)
            || servicesNode is not YamlMappingNode services)
            throw new InvalidDataException("Compose 缺少 services 定义。");

        var result = new List<ComposeServiceDefinition>();
        foreach (var entry in services.Children)
        {
            if (entry.Key is not YamlScalarNode nameNode || entry.Value is not YamlMappingNode svc)
                continue;
            var name = nameNode.Value!;
            var image = GetScalar(svc, "image")
                ?? throw new InvalidDataException($"服务 {name} 缺少 image 定义。");
            var containerName = GetScalar(svc, "container_name") ?? name;
            var ports = GetStringList(svc, "ports");
            var environment = GetEnvironment(svc);
            var volumes = GetStringList(svc, "volumes");
            var dependsOn = GetStringList(svc, "depends_on");
            var command = GetCommand(svc);
            var restart = GetScalar(svc, "restart");

            result.Add(new ComposeServiceDefinition(
                name, image, containerName, ports, environment, volumes, dependsOn, command, restart));
        }
        return result;
    }

    private static IReadOnlyList<ComposeServiceDefinition> TopologicalSort(IReadOnlyList<ComposeServiceDefinition> services)
    {
        var byName = new Dictionary<string, ComposeServiceDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var service in services)
        {
            byName.TryAdd(service.ContainerName, service);
            byName.TryAdd(service.Name, service);
        }
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<ComposeServiceDefinition>();

        void Visit(ComposeServiceDefinition service)
        {
            if (visited.Contains(service.ContainerName)) return;
            if (!visiting.Add(service.ContainerName))
                throw new InvalidDataException($"服务依赖存在循环：{service.ContainerName}。");
            foreach (var dep in service.DependsOn)
            {
                if (byName.TryGetValue(dep, out var depService))
                    Visit(depService);
            }
            visiting.Remove(service.ContainerName);
            visited.Add(service.ContainerName);
            ordered.Add(service);
        }

        foreach (var service in services)
            Visit(service);

        return ordered;
    }

    private static ContainerCreateOptions ToCreateOptions(
        ComposeServiceDefinition service, string baseDirectory, string projectName, string? registryMirror)
    {
        var volumes = new List<string>();
        foreach (var volume in service.Volumes)
        {
            var parts = volume.Split(':');
            if (parts.Length >= 2 && parts[0].StartsWith(".", StringComparison.Ordinal))
            {
                var source = Path.GetFullPath(Path.Combine(baseDirectory, parts[0]));
                var container = parts[1];
                var mode = parts.Length > 2 ? $":{parts[2]}" : string.Empty;
                volumes.Add($"{source}:{container}{mode}");
            }
            else
            {
                volumes.Add(volume);
            }
        }

        return new ContainerCreateOptions(
            service.ContainerName,
            ApplyMirror(service.Image, registryMirror),
            service.Ports,
            service.Environment,
            volumes,
            AutoRemove: false,
            Detached: true,
            Command: service.Command,
            Labels: new Dictionary<string, string>
            {
                [LabelProject] = projectName,
                [LabelService] = service.Name,
            });
    }

    private static string ApplyMirror(string image, string? registryMirror)
    {
        if (string.IsNullOrEmpty(registryMirror)) return image;
        var first = image.Split('/')[0];
        var colon = first.LastIndexOf(':');
        var registry = colon > 0 ? first[..colon] : first;
        var hasRegistry = registry.Contains('.', StringComparison.Ordinal)
            || registry.Contains(':')
            || registry == "localhost"
            || registry is "docker.io" or "index.docker.io";
        if (hasRegistry) return image;
        return $"{registryMirror.TrimEnd('/')}/{image}";
    }

    private static async Task SafeRecordAsync(Func<Task> record)
    {
        try
        {
            await record().ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static long ElapsedMs(DateTimeOffset started)
        => (long)(DateTimeOffset.Now - started).TotalMilliseconds;

    private static string? GetScalar(YamlMappingNode map, string key)
    {
        if (!map.Children.TryGetValue(new YamlScalarNode(key), out var node)) return null;
        return node is YamlScalarNode scalar ? scalar.Value : null;
    }

    private static IReadOnlyList<string> GetStringList(YamlMappingNode map, string key)
    {
        if (!map.Children.TryGetValue(new YamlScalarNode(key), out var node)) return Array.Empty<string>();
        if (node is YamlScalarNode scalar && !string.IsNullOrEmpty(scalar.Value))
            return new[] { scalar.Value! };
        if (node is not YamlSequenceNode sequence) return Array.Empty<string>();
        var list = new List<string>();
        foreach (var item in sequence.Children)
        {
            if (item is YamlScalarNode s && !string.IsNullOrEmpty(s.Value))
                list.Add(s.Value!);
            else if (item is YamlMappingNode m && m.Children.Count == 1)
            {
                if (m.Children.Keys.FirstOrDefault() is YamlScalarNode depKey)
                    list.Add(depKey.Value!);
            }
        }
        return list;
    }

    private static IReadOnlyList<string> GetEnvironment(YamlMappingNode svc)
    {
        if (!svc.Children.TryGetValue(new YamlScalarNode("environment"), out var node)) return Array.Empty<string>();
        var result = new List<string>();
        if (node is YamlSequenceNode sequence)
        {
            foreach (var item in sequence.Children)
            {
                if (item is YamlScalarNode s && !string.IsNullOrEmpty(s.Value))
                    result.Add(s.Value!);
            }
        }
        else if (node is YamlMappingNode mapping)
        {
            foreach (var pair in mapping.Children)
            {
                if (pair.Key is YamlScalarNode k)
                    result.Add(pair.Value is YamlScalarNode v ? $"{k.Value}={v.Value}" : $"{k.Value}");
            }
        }
        return result;
    }

    private static IReadOnlyList<string>? GetCommand(YamlMappingNode svc)
    {
        if (!svc.Children.TryGetValue(new YamlScalarNode("command"), out var node)) return null;
        if (node is YamlScalarNode scalar && !string.IsNullOrEmpty(scalar.Value))
            return new[] { scalar.Value! };
        if (node is YamlSequenceNode sequence)
        {
            var list = new List<string>();
            foreach (var item in sequence.Children)
            {
                if (item is YamlScalarNode s && !string.IsNullOrEmpty(s.Value))
                    list.Add(s.Value!);
            }
            return list;
        }
        return null;
    }
}