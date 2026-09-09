using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WSLCC.Core.Services;

namespace WSLCC.App.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    private readonly IWslcLogService _logs;
    private readonly IWslcContainerService _containers;

    public ObservableCollection<string> ContainerNames { get; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string SelectedContainerName { get; set; }

    [ObservableProperty]
    public partial string LogText { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    public LogsViewModel(IWslcLogService logs, IWslcContainerService containers)
    {
        _logs = logs;
        _containers = containers;
        SelectedContainerName = string.Empty;
        LogText = string.Empty;
        ErrorMessage = string.Empty;
    }

    public AsyncRelayCommand RefreshCommand => new(LoadAsync);

    public AsyncRelayCommand LoadLogsCommand => new(LoadLogsAsync);

    private int _requestVersion;

    public async Task LoadAsync()
    {
        IsLoading = true;
        HasError = false;
        try
        {
            var list = await _containers.ListAsync();
            ContainerNames.Clear();
            foreach (var c in list)
                ContainerNames.Add(c.Name);
            if (SelectedContainerName.Length == 0 && ContainerNames.Count > 0)
                SelectedContainerName = ContainerNames[0];
            await LoadLogsAsync();
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

    public async Task LoadLogsAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedContainerName)) return;
        var version = ++_requestVersion;
        IsLoading = true;
        HasError = false;
        try
        {
            var text = await _logs.GetLogsAsync(SelectedContainerName);
            if (version != _requestVersion) return;
            LogText = text;
        }
        catch (Exception ex)
        {
            if (version != _requestVersion) return;
            LogText = string.Empty;
            ShowError(ex);
        }
        finally
        {
            if (version == _requestVersion)
                IsLoading = false;
        }
    }

    partial void OnSelectedContainerNameChanged(string value)
        => _ = LoadLogsAsync();

    private void ShowError(Exception ex)
    {
        ErrorMessage = ex.Message;
        HasError = true;
    }
}