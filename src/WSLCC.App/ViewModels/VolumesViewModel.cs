using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WSLCC.Core.Services;

namespace WSLCC.App.ViewModels;

public partial class VolumesViewModel : ObservableObject
{
    private readonly IWslcVolumeService _volumes;

    public ObservableCollection<VolumeItemViewModel> Volumes { get; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool HasVolumes { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    public VolumesViewModel(IWslcVolumeService volumes)
    {
        _volumes = volumes;
        ErrorMessage = string.Empty;
    }

    public AsyncRelayCommand RefreshCommand => new(LoadAsync);

    public async Task LoadAsync()
    {
        IsLoading = true;
        HasError = false;
        try
        {
            var list = await _volumes.ListAsync();
            Volumes.Clear();
            foreach (var item in list)
                Volumes.Add(new VolumeItemViewModel(item));
            HasVolumes = Volumes.Count > 0;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task DeleteAsync(VolumeItemViewModel volume)
    {
        try
        {
            await _volumes.DeleteAsync(volume.Source.Name);
            Volumes.Remove(volume);
            HasVolumes = Volumes.Count > 0;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            HasError = true;
        }
    }
}