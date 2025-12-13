using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Flowery.NET.Gallery.Examples;

public class SongItem
{
    public string Artist { get; set; } = "";
    public string Song { get; set; } = "";
    public IBrush AvatarColor { get; set; } = Brushes.Gray;
}

public partial class DataDisplayExamples : UserControl, IScrollableExample
{
    public List<SongItem> Songs { get; } = new();
    private Dictionary<string, Visual>? _sectionTargetsById;

    public DataDisplayExamples()
    {
        InitializeComponent();
        InitializeSongData();
        DataContext = this;
    }

    private void InitializeSongData()
    {
        Songs.AddRange(new SongItem[]
        {
            new() { Artist = "Dio Lupa", Song = "REMAINING REASON", AvatarColor = new SolidColorBrush(Color.Parse("#7c3aed")) },
            new() { Artist = "Ellie Beilish", Song = "BEARS OF A FEVER", AvatarColor = new SolidColorBrush(Color.Parse("#db2777")) },
            new() { Artist = "Sabrino Gardener", Song = "CAPPUCCINO", AvatarColor = new SolidColorBrush(Color.Parse("#0d9488")) },
            new() { Artist = "Luna Starr", Song = "MIDNIGHT DREAMS", AvatarColor = new SolidColorBrush(Color.Parse("#2563eb")) },
            new() { Artist = "Max Thunder", Song = "ELECTRIC STORM", AvatarColor = new SolidColorBrush(Color.Parse("#16a34a")) },
            new() { Artist = "Aria Moon", Song = "SILVER LINING", AvatarColor = new SolidColorBrush(Color.Parse("#d97706")) },
            new() { Artist = "Kai Ember", Song = "PHOENIX RISING", AvatarColor = new SolidColorBrush(Color.Parse("#dc2626")) },
            new() { Artist = "Nova Chen", Song = "STARDUST MEMORIES", AvatarColor = new SolidColorBrush(Color.Parse("#8b5cf6")) },
            new() { Artist = "Zara Vox", Song = "ECHOES IN TIME", AvatarColor = new SolidColorBrush(Color.Parse("#ec4899")) },
            new() { Artist = "Leo Frost", Song = "WINTER SUN", AvatarColor = new SolidColorBrush(Color.Parse("#06b6d4")) },
        });
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
