using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WSLCC.Core.Services;

namespace WSLCC.App.ViewModels;

public partial class NetworksViewModel : ObservableObject
{
    private readonly INetworkService _networks;

    public ObservableCollection<NetworkItemViewModel> Networks { get; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasNetworks { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    public NetworksViewModel(INetworkService networks)
    {
        _networks = networks;
        ErrorMessage = string.Empty;
    }

    public AsyncRelayCommand RefreshCommand => new(LoadAsync);

    public async Task LoadAsync()
    {
        IsLoading = true;
        HasError = false;
        try
        {
            var list = await _networks.ListAsync();
            Networks.Clear();
            foreach (var item in list)
                Networks.Add(new NetworkItemViewModel(item));
            HasNetworks = Networks.Count > 0;
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

    public async Task CreateAsync(string name, string? driver, string? subnet, string? gateway, string? ipRange, bool @internal)
    {
        try
        {
            await _networks.CreateAsync(
                name,
                string.IsNullOrWhiteSpace(driver) ? null : driver,
                Array.Empty<string>(),
                Array.Empty<string>(),
                string.IsNullOrWhiteSpace(subnet) ? null : subnet,
                string.IsNullOrWhiteSpace(gateway) ? null : gateway,
                string.IsNullOrWhiteSpace(ipRange) ? null : ipRange,
                @internal);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    public async Task DeleteAsync(NetworkItemViewModel network)
    {
        try
        {
            await _networks.RemoveAsync(network.Source.Name);
            Networks.Remove(network);
            HasNetworks = Networks.Count > 0;
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    public async Task ConnectAsync(string network, string container)
    {
        try
        {
            await _networks.ConnectAsync(network, container, Array.Empty<string>(), null);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    public async Task DisconnectAsync(string network, string container)
    {
        try
        {
            await _networks.DisconnectAsync(network, container, force: true);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    public async Task<string?> InspectAsync(string network)
    {
        try
        {
            return await _networks.InspectAsync(network);
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return null;
        }
    }

    public async Task<string?> PruneAsync()
    {
        try
        {
            var output = await _networks.PruneAsync();
            await LoadAsync();
            return output;
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return null;
        }
    }

    private void ShowError(Exception ex)
    {
        ErrorMessage = ex.Message;
        HasError = true;
    }
}
