using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WSLCC.Core.Services;

namespace WSLCC.App.ViewModels;

public partial class ImagesViewModel : ObservableObject
{
    private readonly IWslcImageService _images;

    public ObservableCollection<ImageItemViewModel> Images { get; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsPulling { get; set; }

    [ObservableProperty]
    public partial string PullText { get; set; }

    [ObservableProperty]
    public partial double PullProgress { get; set; }

    [ObservableProperty]
    public partial bool IsPullIndeterminate { get; set; }

    [ObservableProperty]
    public partial string PullImageText { get; set; }

    [ObservableProperty]
    public partial ImageItemViewModel? SelectedImage { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; }

    [ObservableProperty]
    public partial bool HasError { get; set; }

    [ObservableProperty]
    public partial bool HasImages { get; set; }

    public ImagesViewModel(IWslcImageService images)
    {
        _images = images;
        PullText = string.Empty;
        PullImageText = string.Empty;
        ErrorMessage = string.Empty;
    }

    public AsyncRelayCommand RefreshCommand => new(LoadAsync);

    public AsyncRelayCommand<string> PullCommand => new(PullAsync);

    public async Task LoadAsync()
    {
        IsLoading = true;
        HasError = false;
        try
        {
            var list = await _images.ListAsync();
            Images.Clear();
            foreach (var item in list)
                Images.Add(new ImageItemViewModel(item));
            HasImages = Images.Count > 0;
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

    public async Task PullAsync(string? imageRef)
    {
        if (string.IsNullOrWhiteSpace(imageRef)) return;
        IsPulling = true;
        IsPullIndeterminate = true;
        PullText = $"正在拉取 {imageRef} ...";
        HasError = false;
        var progress = new Progress<string>(line => PullText = line);
        try
        {
            await _images.PullAsync(imageRef, progress);
            PullText = $"已拉取 {imageRef}";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            PullText = $"拉取失败：{ex.Message}";
            ShowError(ex);
        }
        finally
        {
            IsPulling = false;
            IsPullIndeterminate = false;
        }
    }

    public async Task DeleteImageAsync(ImageItemViewModel image)
    {
        try
        {
            await _images.DeleteAsync(image.Source.FullName);
            Images.Remove(image);
            HasImages = Images.Count > 0;
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