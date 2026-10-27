using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;

namespace Flowery.NET.Gallery.Examples;

/// <summary>
/// The glass controls: DaisyGlass in its blur modes, the glass button and controls, fills, patterns and animated backgrounds.
/// </summary>
public partial class GlassExamples : UserControl, IScrollableExample
{
    private Dictionary<string, Visual>? _sectionTargetsById;

    // The glass button demo's ball, in DIPs from the top-left of its area and DIPs per second.
    private Point _ballPosition = new(40, 30);
    private Vector _ballVelocity = new(190, 140);
    private TimeSpan? _lastBallFrame;
    private bool _ballFrameRequested;
    private bool _ballCheckQueued;
    private bool _ballInView = true;

    public GlassExamples()
    {
        InitializeComponent();
        GlassButtonMotionToggle.IsCheckedChanged += (_, _) => RequestBallFrame();
        // The ball stops while its section is scrolled out of view and moves on when it comes back.
        GlassDemoBall.EffectiveViewportChanged += (_, e) =>
        {
            _ballInView = e.EffectiveViewport.Intersects(new Rect(GlassDemoBall.Bounds.Size));
            RequestBallFrame();
        };
        GlassInterfaceDarkToggle.IsCheckedChanged += (_, _) => UpdateGlassInterfaceMode();
        // The menu acts on a tap or Enter; the arrow keys only move through it.
        GlassInterfaceMenu.AddHandler(TappedEvent, (_, _) => InvokeGlassInterfaceMenuItem(), handledEventsToo: true);
        GlassInterfaceMenu.KeyDown += (_, e) =>
        {
            if (e.Key is not (Key.Enter or Key.Space))
                return;
            InvokeGlassInterfaceMenuItem();
            e.Handled = true;
        };
        OpenGlassDesignerButton.Click += async (_, _) =>
        {
            if (await DaisyGlassDesigner.ShowOverlayAsync(this, GlassDesigner.Design) is { } design)
                GlassDesigner.Design = design;
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        InitializeSkiaMatrix();
        RequestBallFrame();
    }

    private void RequestBallFrame()
    {
        if (_ballFrameRequested || GlassButtonMotionToggle.IsChecked != true || !_ballInView
            || TopLevel.GetTopLevel(this) is not { } topLevel)
        {
            _lastBallFrame = null;
            return;
        }
        // The Gallery keeps a page it showed once and only hides it, and nothing reports when it is shown again, so a
        // hidden ball looks again a few times per second instead of asking for every frame.
        if (!GlassDemoBall.IsEffectivelyVisible)
        {
            _lastBallFrame = null;
            if (_ballCheckQueued)
                return;
            _ballCheckQueued = true;
            DispatcherTimer.RunOnce(() =>
            {
                _ballCheckQueued = false;
                RequestBallFrame();
            }, TimeSpan.FromMilliseconds(250));
            return;
        }
        _ballFrameRequested = true;
        topLevel.RequestAnimationFrame(OnBallFrame);
    }

    private void OnBallFrame(TimeSpan time)
    {
        _ballFrameRequested = false;
        if (GlassDemoBall is not { Parent: Control area, RenderTransform: TranslateTransform transform })
            return;
        if (GlassButtonMotionToggle.IsChecked != true || !_ballInView || !GlassDemoBall.IsEffectivelyVisible
            || TopLevel.GetTopLevel(this) == null)
        {
            RequestBallFrame();
            return;
        }

        // A long gap, such as after the page was hidden, must not throw the ball through a wall.
        var elapsed = _lastBallFrame is { } last ? Math.Min((time - last).TotalSeconds, 0.05) : 0;
        _lastBallFrame = time;
        var (x, vx) = Bounce(_ballPosition.X + _ballVelocity.X * elapsed, _ballVelocity.X, area.Bounds.Width - GlassDemoBall.Bounds.Width);
        var (y, vy) = Bounce(_ballPosition.Y + _ballVelocity.Y * elapsed, _ballVelocity.Y, area.Bounds.Height - GlassDemoBall.Bounds.Height);
        _ballPosition = new Point(x, y);
        _ballVelocity = new Vector(vx, vy);
        transform.X = x;
        transform.Y = y;
        RequestBallFrame();
    }

    // Reflects a position that passed a wall back inside, and turns the velocity away from that wall.
    private static (double Position, double Velocity) Bounce(double position, double velocity, double limit)
    {
        if (limit <= 0)
            return (0, velocity);
        if (position < 0)
            return (Math.Min(-position, limit), Math.Abs(velocity));
        if (position > limit)
            return (Math.Max(2 * limit - position, 0), -Math.Abs(velocity));
        return (position, velocity);
    }

    private void UpdateGlassInterfaceMode()
    {
        var dark = GlassInterfaceDarkToggle.IsChecked == true;
        GlassInterfaceDarkSky.Opacity = dark ? 1 : 0;
        GlassInterfaceLightSky.Opacity = dark ? 0 : 1;
        GlassInterfaceStage.Classes.Set("light", !dark);
        // The menu lies in the overlay layer, outside the stage.
        GlassInterfaceMenu.Classes.Set("light", !dark);
    }

    public void OpenGlassInterfaceMenu_Click(object? sender, RoutedEventArgs e) => GlassInterfaceMenuPopup.IsOpen = true;

    // A demo action: closes the menu, which keeps no selection.
    private void InvokeGlassInterfaceMenuItem()
    {
        if (GlassInterfaceMenu.SelectedItem == null)
            return;
        GlassInterfaceMenu.SelectedItem = null;
        GlassInterfaceMenuPopup.IsOpen = false;
    }

    public void OpenGlassModal_Click(object? sender, RoutedEventArgs e)
    {
        GlassShowcaseModal.IsOpen = true;
    }

    public void CloseGlassModal_Click(object? sender, RoutedEventArgs e)
    {
        GlassShowcaseModal.IsOpen = false;
    }

    private void InitializeSkiaMatrix()
    {
        var container = this.FindControl<StackPanel>("SkiaMatrixContainer");
        if (container == null || container.Children.Count > 0) return;

        // Header Row (Saturation values)
        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        headerRow.Children.Add(new Control { Width = 40 }); // Corner spacer

        for (int sat = 0; sat <= 100; sat += 10)
        {
            headerRow.Children.Add(new TextBlock
            {
                Text = $"S{sat}%",
                Width = 50,
                FontSize = 10,
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                FontWeight = FontWeight.Bold
            });
        }
        container.Children.Add(headerRow);

        // Rows (Blur values)
        for (int blur = 0; blur <= 40; blur += 10)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };

            // Row Header
            row.Children.Add(new TextBlock
            {
                Text = $"B{blur}",
                Width = 40,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.White,
                FontSize = 10,
                FontWeight = FontWeight.Bold,
                TextAlignment = TextAlignment.Right
            });

            for (int sat = 0; sat <= 100; sat += 10)
            {
                // Container for tile + background content
                var tileGrid = new Grid { Width = 50, Height = 35 };

                // Background content (text "lorem")
                var bgText = new TextBlock
                {
                    Text = "lorem",
                    FontSize = 10,
                    Foreground = Brushes.Black,
                    FontWeight = FontWeight.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                tileGrid.Children.Add(bgText);

                // Glass Overlay
                var glass = new DaisyGlass
                {
                    GlassBlur = blur,
                    GlassSaturation = sat / 100.0,
                    BlurMode = GlassBlurMode.SkiaSharp,
                    EnableBackdropBlur = true,
                    CornerRadius = new CornerRadius(6),
                    GlassTintOpacity = 0.1,
                    GlassTint = Colors.White,
                    GlassBorderOpacity = 0.2,
                    GlassReflectOpacity = 0.15,
                    Padding = new Thickness(0)
                };
                ToolTip.SetTip(glass, $"Blur: {blur}, Saturation: {sat/100.0:P0}");

                tileGrid.Children.Add(glass);
                row.Children.Add(tileGrid);
            }
            container.Children.Add(row);
        }
    }

    public void ScrollToSection(string sectionName)
    {
        var scrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
        if (scrollViewer == null) return;

        var target = GetSectionTarget(sectionName);
        if (target == null) return;

        var transform = target.TransformToVisual(scrollViewer);
        if (transform.HasValue)
        {
            var point = transform.Value.Transform(new Point(0, 0));
            // Add current scroll offset to get absolute position in content
            scrollViewer.Offset = new Vector(0, point.Y + scrollViewer.Offset.Y);
        }
    }

    private Visual? GetSectionTarget(string sectionId)
    {
        if (_sectionTargetsById == null)
        {
            _sectionTargetsById = new Dictionary<string, Visual>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in this.GetVisualDescendants().OfType<SectionHeader>())
            {
                if (!string.IsNullOrWhiteSpace(header.SectionId))
                    _sectionTargetsById[header.SectionId] = header.Parent as Visual ?? header;
            }
        }

        return _sectionTargetsById.TryGetValue(sectionId, out var target) ? target : null;
    }
}
