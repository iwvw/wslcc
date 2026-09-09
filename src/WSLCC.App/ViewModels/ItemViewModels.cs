using CommunityToolkit.Mvvm.ComponentModel;
using WSLCC.Core.Models;

namespace WSLCC.App.ViewModels;

public sealed partial class VolumeItemViewModel : ObservableObject
{
    public VolumeItem Source { get; }

    public string Name => Source.Name;

    public string Driver => Source.Driver;

    public string Mountpoint => string.IsNullOrEmpty(Source.Mountpoint) ? "-" : Source.Mountpoint;

    public string Scope => string.IsNullOrEmpty(Source.Scope) ? "-" : Source.Scope;

    public VolumeItemViewModel(VolumeItem source) => Source = source;
}

public sealed partial class ImageItemViewModel : ObservableObject
{
    public ImageItem Source { get; }

    public string Name => Source.FullName;

    public string Id => Source.Id;

    public string Size => Source.Size;

    public string CreatedAt => string.IsNullOrEmpty(Source.CreatedAt) ? "-" : Source.CreatedAt;

    public ImageItemViewModel(ImageItem source) => Source = source;
}

public sealed partial class ContainerItemViewModel : ObservableObject
{
    private ContainerStats? _stats;

    public ContainerItem Source { get; }

    public string Name => Source.Name;

    public string Image => Source.Image;

    public string State => Source.State;

    public string Status => Source.Status;

    public string Ports => string.IsNullOrEmpty(Source.Ports) ? "-" : Source.Ports;

    public bool IsRunning => Source.State.Equals("running", StringComparison.OrdinalIgnoreCase);

    public string StateGlyph => IsRunning ? "\uE7BA" : "\uE769";

    public string ResourceText
    {
        get
        {
            if (_stats is null) return "暂无资源数据";
            return $"CPU {_stats.CpuPerc} · 内存 {_stats.MemUsage}（{_stats.MemPerc}）";
        }
    }

    public string? WebUrl
    {
        get
        {
            foreach (var part in Source.Ports.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var binding = part.Split("->", 2)[0].Trim();
                var hostPort = binding.Contains('/') ? binding.Split('/')[0] : binding;
                if (!hostPort.Contains(':')) continue;
                var segments = hostPort.Split(':');
                var port = segments[^1];
                var host = segments.Length >= 2 && segments[0].Length > 0 ? segments[0] : "localhost";
                if (int.TryParse(port, out _)) return $"http://{host}:{port}";
            }
            return null;
        }
    }

    public bool HasWebUrl => WebUrl is not null;

    public ContainerItemViewModel(ContainerItem source, ContainerStats? stats = null)
    {
        Source = source;
        _stats = stats;
    }

    public void UpdateStats(ContainerStats? stats)
    {
        _stats = stats;
        OnPropertyChanged(nameof(ResourceText));
    }
}