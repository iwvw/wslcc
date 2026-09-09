using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace WSLCC.App.Controls;

public sealed partial class SkeletonGrid : UserControl
{
    private Storyboard? _pulse;

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(SkeletonGrid), new PropertyMetadata(false, OnIsActiveChanged));

    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public IReadOnlyList<int> Rows { get; } = new int[2];

    public SkeletonGrid()
    {
        InitializeComponent();
        Loaded += (_, _) => { if (IsActive) StartPulse(); };
        Unloaded += (_, _) => StopPulse();
    }

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (SkeletonGrid)d;
        var active = e.NewValue is true;
        self.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        if (active) self.StartPulse();
        else self.StopPulse();
    }

    private void StartPulse()
    {
        if (_pulse is not null) return;
        var animation = new DoubleAnimation
        {
            From = 1,
            To = 0.45,
            Duration = new Duration(TimeSpan.FromMilliseconds(750)),
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        };
        Storyboard.SetTarget(animation, Root);
        Storyboard.SetTargetProperty(animation, "Opacity");
        _pulse = new Storyboard();
        _pulse.Children.Add(animation);
        _pulse.Begin();
    }

    private void StopPulse()
    {
        _pulse?.Stop();
        _pulse = null;
        Root.Opacity = 1;
    }
}