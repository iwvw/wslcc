using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WSLCC.Core.Services;
using WSLCC.Data;

namespace WSLCC.App.ViewModels;

public sealed partial class AuditLogItemViewModel : ObservableObject
{
    public AuditLogEntry Source { get; }

    public AuditLogItemViewModel(AuditLogEntry source) => Source = source;

    public string TimeText => Source.Timestamp.LocalDateTime.ToString("MM-dd HH:mm:ss");

    public string Category => Source.Category;

    public string Action => Source.Action;

    public string Detail => Source.Detail ?? string.Empty;

    public string Result => Source.Result;

    public string DurationText => Source.DurationMs?.ToString() ?? string.Empty;
}

public sealed partial class PullHistoryItemViewModel : ObservableObject
{
    public PullHistoryEntry Source { get; }

    public PullHistoryItemViewModel(PullHistoryEntry source) => Source = source;

    public string TimeText => Source.StartedAt.LocalDateTime.ToString("MM-dd HH:mm:ss");

    public string ImageRef => Source.ImageRef;

    public string Status => Source.Status;

    public string Error => Source.Error ?? string.Empty;
}

public partial class ActivityViewModel : ObservableObject
{
    private readonly WslcHost _host;

    public ObservableCollection<AuditLogItemViewModel> AuditEntries { get; } = new();

    public ObservableCollection<PullHistoryItemViewModel> PullEntries { get; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    public ActivityViewModel(WslcHost host)
    {
        _host = host;
        ErrorMessage = string.Empty;
    }

    public AsyncRelayCommand RefreshCommand => new(LoadAsync);

    public AsyncRelayCommand ClearAuditCommand => new(ClearAuditAsync);

    public async Task LoadAsync()
    {
        IsLoading = true;
        HasError = false;
        try
        {
            var audits = await _host.Audit.QueryAsync(200);
            AuditEntries.Clear();
            foreach (var entry in audits)
                AuditEntries.Add(new AuditLogItemViewModel(entry));

            var pulls = await _host.History.QueryPullsAsync(100);
            PullEntries.Clear();
            foreach (var entry in pulls)
                PullEntries.Add(new PullHistoryItemViewModel(entry));
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

    public async Task ClearAuditAsync()
    {
        try
        {
            await _host.Audit.ClearAsync();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ShowError(Exception ex)
    {
        ErrorMessage = ex.Message;
        HasError = true;
    }
}