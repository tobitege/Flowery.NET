using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Flowery.Effects;

namespace Flowery.NET.Gallery.Examples;

public partial class EffectsExamples : UserControl, IScrollableExample
{
    public EffectsExamples()
    {
        InitializeComponent();
    }

    public void ReplayReveal_Click(object? sender, RoutedEventArgs e)
    {
        // Helper to replay a single demo
        void Replay(string name)
        {
            var demo = this.FindControl<Border>(name);
            if (demo != null)
            {
                RevealBehavior.SetIsEnabled(demo, false);
                RevealBehavior.SetIsEnabled(demo, true);
            }
        }

        // Replay all mode demos
        Replay("RevealDemo");
        Replay("SlideInDemo");
        Replay("FadeOnlyDemo");
        Replay("ScaleDemo");
        Replay("ScaleSlideDemo");

        // Replay all direction demos
        var directionsPanel = this.FindControl<WrapPanel>("RevealDirections");
        if (directionsPanel != null)
        {
            foreach (var child in directionsPanel.Children.OfType<Border>())
            {
                RevealBehavior.SetIsEnabled(child, false);
                RevealBehavior.SetIsEnabled(child, true);
            }
        }
    }

    public void ScrollToSection(string sectionName)
    {
        var scrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
        if (scrollViewer == null) return;

        var sectionHeader = this.GetVisualDescendants()
            .OfType<SectionHeader>()
            .FirstOrDefault(h => h.SectionId == sectionName);

        if (sectionHeader?.Parent is Visual parent)
        {
            var transform = parent.TransformToVisual(scrollViewer);
            if (transform.HasValue)
            {
                var point = transform.Value.Transform(new Point(0, 0));
                scrollViewer.Offset = new Vector(0, point.Y + scrollViewer.Offset.Y);
            }
        }
    }

    private CancellationTokenSource? _showcaseCts;
    private bool _showcaseRunning;

    public async void StartShowcase_Click(object? sender, RoutedEventArgs e)
    {
        var demo = this.FindControl<Border>("RevealShowcaseDemo");
        var label = this.FindControl<TextBlock>("RevealShowcaseLabel");
        var button = this.FindControl<Flowery.Controls.DaisyButton>("StartShowcaseBtn");
        var cursorPanel = this.FindControl<Panel>("CursorFollowShowcasePanel");
        var cursorLabel = this.FindControl<TextBlock>("CursorFollowShowcaseLabel");
        
        if (demo == null || label == null || button == null) return;

        // Toggle running state
        if (_showcaseRunning)
        {
            _showcaseCts?.Cancel();
            _showcaseRunning = false;
            button.Content = "▶ Start Showcase Loop";
            label.Text = "Reveal: Stopped";
            if (cursorPanel != null) CursorFollowBehavior.HideFollower(cursorPanel);
            if (cursorLabel != null) cursorLabel.Text = "Cursor Follow: Stopped";
            
            // Reset scramble to original text
            var scrambleDemo = this.FindControl<TextBlock>("ScrambleShowcaseDemo");
            if (scrambleDemo != null) ScrambleHoverBehavior.ResetScramble(scrambleDemo);
            
            return;
        }

        _showcaseRunning = true;
        _showcaseCts = new CancellationTokenSource();
        button.Content = "⏹ Stop";

        // Show cursor follower
        if (cursorPanel != null)
        {
            CursorFollowBehavior.ShowFollower(cursorPanel);
            if (cursorLabel != null) cursorLabel.Text = "Cursor Follow: ∞ Path";
        }

        // Start infinity path animation concurrently
        _ = AnimateInfinityPath(cursorPanel, _showcaseCts.Token);

        var modes = new (RevealMode Mode, RevealDirection Dir, double Dist, string Name)[]
        {
            (RevealMode.FadeReveal, RevealDirection.Bottom, 40, "Reveal: FadeReveal (Bottom)"),
            (RevealMode.FadeReveal, RevealDirection.Left, 40, "Reveal: FadeReveal (Left)"),
            (RevealMode.SlideIn, RevealDirection.Right, 80, "Reveal: SlideIn (Right)"),
            (RevealMode.SlideIn, RevealDirection.Top, 60, "Reveal: SlideIn (Top)"),
            (RevealMode.FadeOnly, RevealDirection.Bottom, 0, "Reveal: FadeOnly"),
            (RevealMode.Scale, RevealDirection.Bottom, 0, "Reveal: Scale"),
            (RevealMode.ScaleSlide, RevealDirection.Bottom, 50, "Reveal: ScaleSlide"),
        };

        try
        {
            while (!_showcaseCts.Token.IsCancellationRequested)
            {
                foreach (var (mode, dir, dist, name) in modes)
                {
                    if (_showcaseCts.Token.IsCancellationRequested) break;

                    // Update label
                    label.Text = name;

                    // Configure and trigger reveal
                    RevealBehavior.SetMode(demo, mode);
                    RevealBehavior.SetDirection(demo, dir);
                    RevealBehavior.SetDistance(demo, dist);
                    RevealBehavior.SetDuration(demo, TimeSpan.FromMilliseconds(600));
                    
                    // Trigger reveal animation
                    RevealBehavior.SetIsEnabled(demo, false);
                    await Task.Delay(50, _showcaseCts.Token);
                    RevealBehavior.SetIsEnabled(demo, true);

                    // Also trigger scramble effect
                    var scrambleDemo = this.FindControl<TextBlock>("ScrambleShowcaseDemo");
                    var scrambleLabel = this.FindControl<TextBlock>("ScrambleShowcaseLabel");
                    if (scrambleDemo != null && scrambleLabel != null)
                    {
                        scrambleLabel.Text = "Scramble: Running...";
                        ScrambleHoverBehavior.TriggerScramble(scrambleDemo);
                    }

                    // Wait for animation + pause
                    await Task.Delay(1200, _showcaseCts.Token);
                    
                    // Reset scramble label
                    if (scrambleLabel != null)
                    {
                        scrambleLabel.Text = "Scramble: (also hover)";
                    }
                }
            }
        }
        catch (TaskCanceledException)
        {
            // Expected when stopped
        }

        _showcaseRunning = false;
        button.Content = "▶ Start Showcase Loop";
        label.Text = "Reveal: Click Start";
        if (cursorPanel != null) CursorFollowBehavior.HideFollower(cursorPanel);
        if (cursorLabel != null) cursorLabel.Text = "Cursor Follow: Move mouse ↓";
    }
    private bool _mouseOverCursorPanel;

    /// <summary>
    /// Animates the cursor follower along an infinity (lemniscate) path.
    /// </summary>
    private async Task AnimateInfinityPath(Panel? panel, CancellationToken cancellationToken)
    {
        if (panel == null) return;

        // Hook up mouse events to pause animation when user hovers
        panel.PointerEntered += (_, _) => _mouseOverCursorPanel = true;
        panel.PointerExited += (_, _) => _mouseOverCursorPanel = false;

        const double speed = 0.03; // radians per frame
        double t = 0;
        int shapeIndex = 0;
        var shapes = new[] { FollowerShape.Circle, FollowerShape.Square, FollowerShape.Ring };
        var cursorLabel = this.FindControl<TextBlock>("CursorFollowShowcaseLabel");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Skip automation when mouse is over the panel
                if (!_mouseOverCursorPanel)
                {
                    // Get panel dimensions
                    var width = panel.Bounds.Width;
                    var height = panel.Bounds.Height;
                    
                    if (width > 0 && height > 0)
                    {
                        // Lemniscate of Bernoulli parametric equations
                        // x = a * cos(t) / (1 + sin²(t))
                        // y = a * sin(t) * cos(t) / (1 + sin²(t))
                        var sinT = Math.Sin(t);
                        var cosT = Math.Cos(t);
                        var denom = 1 + sinT * sinT;
                        
                        // Scale to fit panel with padding
                        var scaleX = (width - 20) / 2.5;
                        var scaleY = (height - 16) / 1.5;
                        
                        var x = (cosT / denom) * scaleX + width / 2;
                        var y = (sinT * cosT / denom) * scaleY + height / 2;
                        
                        CursorFollowBehavior.SetTargetPosition(panel, x, y);
                    }

                    t += speed;
                    if (t > Math.PI * 2)
                    {
                        t -= Math.PI * 2;
                        
                        // Change shape each loop
                        shapeIndex = (shapeIndex + 1) % shapes.Length;
                        CursorFollowBehavior.SetFollowerShape(panel, shapes[shapeIndex]);
                        if (cursorLabel != null)
                            cursorLabel.Text = $"Cursor: {shapes[shapeIndex]} ∞";
                    }
                }

                await Task.Delay(16, cancellationToken); // ~60fps
            }
        }
        catch (TaskCanceledException)
        {
            // Expected when stopped
        }
    }
}
