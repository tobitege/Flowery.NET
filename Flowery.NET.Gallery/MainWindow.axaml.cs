using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Flowery.Localization;

namespace Flowery.NET.Gallery;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        
        UpdateFlowDirection();
        FloweryLocalization.CultureChanged += (_, _) => Dispatcher.UIThread.InvokeAsync(UpdateFlowDirection);
    }

    private void UpdateFlowDirection()
    {
        FlowDirection = FloweryLocalization.Instance.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }
}
