using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WSLCC.Core.Services;
using WSLCC_App;

namespace WSLCC.App.ViewModels;

public sealed partial class EventItemViewModel : ObservableObject
{
    public WslcEventEntry Source { get; }

    public EventItemViewModel(WslcEventEntry source) => Source = source;

    public string TimeText => Source.TimeText;

    public string Type => Source.Type;

    public string Action => Source.Action;

    public string Target => Source.Target;

    public string Attributes => Source.Attributes;

    public string Summary => Source.Summary;
}

public partial class EventsViewModel : ObservableObject
{
    private readonly IWslcEventService _events;
    private CancellationTokenSource? _streamCts;

    public ObservableCollection<EventItemViewModel> Events { get; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsStreaming { get; set; }

    [ObservableProperty]
    public partial bool IsSupported { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial bool HasEvents { get; set; }

    public bool Unsupported => !IsSupported;

    public string StreamButtonText => IsStreaming ? "停止监听" : "实时监听";

    [ObservableProperty]
    public partial string SinceText { get; set; }

    [ObservableProperty]
    public partial string UntilText { get; set; }

    [ObservableProperty]
    public partial string FilterTypeText { get; set; }

    [ObservableProperty]
    public partial string FilterContainerText { get; set; }

    partial void OnIsStreamingChanged(bool value) => OnPropertyChanged(nameof(StreamButtonText));

    partial void OnIsSupportedChanged(bool value) => OnPropertyChanged(nameof(Unsupported));

    public EventsViewModel(IWslcEventService events)
    {
        _events = events;
        ErrorMessage = string.Empty;
        SinceText = string.Empty;
        UntilText = string.Empty;
        FilterTypeText = string.Empty;
        FilterContainerText = string.Empty;
    }

    public AsyncRelayCommand RefreshCommand => new(LoadAsync);

    public AsyncRelayCommand ToggleStreamCommand => new(ToggleStreamAsync);

    public async Task InitializeAsync()
    {
        try
        {
            IsSupported = await _events.IsSupportedAsync();
        }
        catch
        {
            IsSupported = false;
        }
        if (IsSupported) await LoadAsync();
    }

    public async Task LoadAsync()
    {
        if (!IsSupported) return;
        IsLoading = true;
        HasError = false;
        try
        {
            var list = await _events.QueryAsync(BuildOptions());
            Events.Clear();
            foreach (var entry in list)
                Events.Add(new EventItemViewModel(entry));
            HasEvents = Events.Count > 0;
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ToggleStreamAsync()
    {
        if (IsStreaming)
        {
            StopStream();
            return;
        }

        IsStreaming = true;
        HasError = false;
        _streamCts = new CancellationTokenSource();
        var progress = new Progress<WslcEventEntry>(entry =>
        {
            Events.Add(new EventItemViewModel(entry));
            HasEvents = true;
        });
        try
        {
            await _events.StreamAsync(BuildOptions(), progress, _streamCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (!_streamCts.IsCancellationRequested) ShowError(ex);
        }
        finally
        {
            IsStreaming = false;
            _streamCts?.Dispose();
            _streamCts = null;
        }
    }

    public void StopStream()
    {
        _streamCts?.Cancel();
    }

    private WslcEventOptions BuildOptions()
    {
        var filters = new List<WslcEventFilter>();
        var type = FilterTypeText?.Trim();
        if (!string.IsNullOrEmpty(type)) filters.Add(new WslcEventFilter("type", [type]));
        var container = FilterContainerText?.Trim();
        if (!string.IsNullOrEmpty(container)) filters.Add(new WslcEventFilter("container", [container]));

        var since = SinceText?.Trim();
        if (string.IsNullOrEmpty(since))
            since = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds().ToString();

        return new WslcEventOptions(
            since,
            string.IsNullOrWhiteSpace(UntilText) ? null : UntilText.Trim(),
            filters.Count > 0 ? filters : null);
    }

    private void ShowError(Exception ex)
    {
        ErrorMessage = ex.Message;
        HasError = true;
    }
}
