using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Flowery.Controls;

namespace Flowery.NET.Gallery.Examples;

public partial class CardsExamples : UserControl, IScrollableExample
{
    private Dictionary<string, Visual>? _sectionTargetsById;

    private Grid? _cardStackContainer;
    private int _currentCardIndex = 0;
    private readonly List<DaisyCard> _cards = new();
    private DaisyButton? _prevCardButton;
    private DaisyButton? _nextCardButton;

    public CardsExamples()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        InitializeSkiaMatrix();
        InitializeCardStack();
    }

    private void InitializeCardStack()
    {
        _cardStackContainer = this.FindControl<Grid>("CardStackContainer");
        _prevCardButton = this.FindControl<DaisyButton>("PrevCardBtn");
        _nextCardButton = this.FindControl<DaisyButton>("NextCardBtn");
        if (_cardStackContainer == null || _cards.Count > 0) return;

        var colors = new IBrush[] {
            new SolidColorBrush(Color.Parse("#7c3aed")),  // Primary (purple)
            new SolidColorBrush(Color.Parse("#db2777")),  // Secondary (pink)
            new SolidColorBrush(Color.Parse("#f59e0b")),  // Accent (amber)
            new SolidColorBrush(Color.Parse("#0ea5e9"))   // Info (sky)
        };

        for (int i = 0; i < colors.Length; i++)
        {
            var card = new DaisyCard
            {
                Width = 260,
                Height = 340,
                Background = colors[i],
                Content = new TextBlock
                {
                    Text = $"CARD {i + 1}",
                    Foreground = Brushes.White,
                    FontWeight = FontWeight.Bold,
                    FontSize = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                },
                RenderTransform = new TransformGroup
                {
                    Children = new Transforms { new ScaleTransform(), new TranslateTransform() }
                }
            };
            _cards.Add(card);
            _cardStackContainer.Children.Add(card);
        }

        UpdateCardStack();
    }

    private void UpdateCardStack()
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            var card = _cards[i];
            var offset = i - _currentCardIndex;

            // Visual properties
            var zIndex = _cards.Count - Math.Abs(offset);
            var opacity = offset < 0 ? 0 : (1.0 - offset * 0.2);
            var scale = 1.0 - (offset * 0.05);
            var translateY = offset * 20.0;

            card.ZIndex = zIndex;
            card.IsVisible = offset >= 0;

            var group = (TransformGroup)card.RenderTransform!;
            var scaleTransform = (ScaleTransform)group.Children[0];
            var translateTransform = (TranslateTransform)group.Children[1];

            scaleTransform.ScaleX = scaleTransform.ScaleY = scale;
            translateTransform.Y = translateY;
            card.Opacity = opacity;
        }

        UpdateCardStackNavigationButtons();
    }

    private void UpdateCardStackNavigationButtons()
    {
        if (_prevCardButton == null || _nextCardButton == null) return;

        var canNavigate = _cards.Count > 1;
        _prevCardButton.IsVisible = canNavigate && _currentCardIndex > 0;
        _nextCardButton.IsVisible = canNavigate && _currentCardIndex < _cards.Count - 1;
    }

    private void PrevCard_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentCardIndex > 0)
        {
            _currentCardIndex--;
            UpdateCardStack();
        }
    }

    private void NextCard_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentCardIndex < _cards.Count - 1)
        {
            _currentCardIndex++;
            UpdateCardStack();
        }
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
