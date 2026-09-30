using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace WSLCC.App.Controls;

internal sealed class OperationProgressPanel
{
    private const int MaxLogEntries = 300;

    private readonly TextBlock _phase;
    private readonly ProgressBar _bar;
    private readonly StackPanel _logPanel;
    private readonly ScrollViewer _scroll;

    public StackPanel Root { get; }

    public OperationProgressPanel(string initialPhase)
    {
        _phase = new TextBlock
        {
            Text = initialPhase,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            TextWrapping = TextWrapping.Wrap,
        };
        _bar = new ProgressBar
        {
            IsIndeterminate = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _logPanel = new StackPanel { Spacing = 2 };
        _scroll = new ScrollViewer
        {
            MaxHeight = 220,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _logPanel,
        };
        Root = new StackPanel { Spacing = 12 };
        Root.Children.Add(_phase);
        Root.Children.Add(_bar);
        Root.Children.Add(_scroll);
    }

    public IProgress<string> CreateSink() => new Progress<string>(Report);

    public void Report(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        var text = line.TrimEnd();
        _phase.Text = text;
        AppendLog(text, emphasize: true);
    }

    public void Complete(string summary)
    {
        _bar.Visibility = Visibility.Collapsed;
        if (string.IsNullOrWhiteSpace(summary))
        {
            _phase.Text = string.Empty;
            return;
        }
        AppendSummary(summary, isError: false);
    }

    public void Fail(string message)
    {
        _bar.Visibility = Visibility.Collapsed;
        AppendSummary(message, isError: true);
    }

    public void Cancelled(string message)
    {
        _bar.Visibility = Visibility.Collapsed;
        _phase.Text = message;
    }

    private void AppendSummary(string text, bool isError)
    {
        var block = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            Foreground = isError
                ? (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"]
                : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"],
        };
        var divider = new Border
        {
            Height = 1,
            Margin = new Thickness(0, 6, 0, 6),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
        };
        _logPanel.Children.Add(divider);
        _logPanel.Children.Add(block);
        ScrollToEnd();
    }

    private void AppendLog(string text, bool emphasize)
    {
        while (_logPanel.Children.Count >= MaxLogEntries)
            _logPanel.Children.RemoveAt(0);

        var block = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            IsTextSelectionEnabled = true,
            Foreground = emphasize
                ? (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"]
                : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        _logPanel.Children.Add(block);
        ScrollToEnd();
    }

    private void ScrollToEnd()
    {
        _scroll.UpdateLayout();
        _scroll.ChangeView(null, _scroll.ScrollableHeight, null);
    }
}
