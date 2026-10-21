using Flowery.NET.Photos.Localization;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos.Views
{
    /// <summary>
    /// A virtualized row in the gallery: either a date header or a justified row of photos.
    /// Height includes the spacing below the row so offsets can be computed exactly.
    /// </summary>
    public abstract class GalleryRow
    {
        public double Top { get; set; }
        public double Height { get; set; }
        public DateTime Date { get; init; }
    }

    public sealed class GalleryHeaderRow : GalleryRow
    {
        public required string Title { get; init; }
        public required List<PhotoItem> Items { get; init; }
        public override string ToString() => Title;
    }

    public sealed class GalleryPhotoRow : GalleryRow
    {
        public required List<(PhotoItem Item, double Width)> Tiles { get; init; }
        public double TileHeight { get; init; }
        public override string ToString() => PhotosLocalization.Format("Photos_ItemCount", Tiles.Count);
    }

    /// <summary>
    /// Builds justified ("Google Photos style") rows: every full row fills the available width while
    /// keeping each photo's aspect ratio. The last row of a group keeps the target height.
    /// Square tiles use the same rows with an aspect ratio of 1, so square photos keep their size.
    /// </summary>
    public static class GalleryLayout
    {
        public const double HeaderHeight = 52;

        public static List<GalleryRow> Build(
            IReadOnlyList<PhotoItem> items,
            double width,
            double targetHeight,
            double gap,
            PhotoGroupMode groupMode,
            bool squareTiles)
        {
            List<GalleryRow> rows = [];
            if (items.Count == 0 || width < 50)
                return rows;

            var top = 0.0;
            foreach (var group in Group(items, groupMode))
            {
                if (group.Title != null)
                {
                    rows.Add(new GalleryHeaderRow
                    {
                        Title = group.Title,
                        Items = group.Items,
                        Date = group.Date,
                        Top = top,
                        Height = HeaderHeight
                    });
                    top += HeaderHeight;
                }

                var groupRows = BuildJustified(group.Items, width, targetHeight, gap, squareTiles);

                foreach (var row in groupRows)
                {
                    row.Top = top;
                    rows.Add(row);
                    top += row.Height;
                }
            }

            return rows;
        }

        /// <summary>
        /// Fills each row to the full width. A row takes the photo count whose height is closest to the target
        /// height; the last row of a group reuses the height of the row above (or the target height), so all
        /// rows of a group look alike.
        /// </summary>
        private static List<GalleryPhotoRow> BuildJustified(List<PhotoItem> items, double width, double targetHeight, double gap, bool squareTiles)
        {
            List<GalleryPhotoRow> rows = [];
            double? previousHeight = null;
            var start = 0;

            while (start < items.Count)
            {
                var count = 0;
                var aspectSum = 0.0;
                var full = false;
                while (start + count < items.Count)
                {
                    aspectSum += Aspect(items[start + count], squareTiles);
                    count++;
                    if (aspectSum * targetHeight + gap * (count - 1) >= width)
                    {
                        full = true;
                        break;
                    }
                }

                if (!full)
                {
                    var height = previousHeight is { } h && aspectSum * h + gap * (count - 1) <= width ? h : targetHeight;
                    rows.Add(CreateRow(items.GetRange(start, count), height, gap, width, stretch: false, squareTiles));
                    break;
                }

                var rowHeight = (width - gap * (count - 1)) / aspectSum;
                if (count > 1)
                {
                    var sumWithout = aspectSum - Aspect(items[start + count - 1], squareTiles);
                    var heightWithout = (width - gap * (count - 2)) / sumWithout;
                    if (Math.Abs(heightWithout - targetHeight) < Math.Abs(rowHeight - targetHeight))
                    {
                        count--;
                        rowHeight = heightWithout;
                    }
                }

                rows.Add(CreateRow(items.GetRange(start, count), rowHeight, gap, width, stretch: true, squareTiles));
                previousHeight = rowHeight;
                start += count;
            }

            return rows;
        }

        private static double Aspect(PhotoItem item, bool squareTiles) => squareTiles ? 1 : item.AspectRatio;

        private static GalleryPhotoRow CreateRow(List<PhotoItem> items, double height, double gap, double width, bool stretch, bool squareTiles)
        {
            height = Math.Max(40, Math.Round(height));
            List<(PhotoItem, double)> tiles = [];
            var used = 0.0;
            for (var i = 0; i < items.Count; i++)
            {
                var w = Math.Floor((squareTiles ? 1 : items[i].AspectRatio) * height);
                if (stretch && i == items.Count - 1)
                    w = Math.Max(20, width - used);
                tiles.Add((items[i], w));
                used += w + gap;
            }

            return new GalleryPhotoRow
            {
                Tiles = tiles,
                TileHeight = height,
                Height = height + gap,
                Date = items[0].SortDate
            };
        }

        private sealed record PhotoGroup(string? Title, DateTime Date, List<PhotoItem> Items);

        private static IEnumerable<PhotoGroup> Group(IReadOnlyList<PhotoItem> items, PhotoGroupMode mode)
        {
            if (mode == PhotoGroupMode.None)
            {
                yield return new PhotoGroup(null, items[0].SortDate, [.. items]);
                yield break;
            }

            var culture = PhotosLocalization.CurrentCulture;
            List<PhotoItem> current = [];
            DateTime? currentKey = null;

            foreach (var item in items)
            {
                var date = item.SortDate;
                var key = mode switch
                {
                    PhotoGroupMode.Day => date.Date,
                    PhotoGroupMode.Year => new DateTime(date.Year, 1, 1),
                    _ => new DateTime(date.Year, date.Month, 1)
                };

                if (currentKey != null && key != currentKey)
                {
                    yield return new PhotoGroup(FormatGroupTitle(currentKey.Value, mode, culture), currentKey.Value, current);
                    current = [];
                }
                currentKey = key;
                current.Add(item);
            }

            if (current.Count > 0 && currentKey != null)
                yield return new PhotoGroup(FormatGroupTitle(currentKey.Value, mode, culture), currentKey.Value, current);
        }

        public static string FormatGroupTitle(DateTime date, PhotoGroupMode mode, CultureInfo culture)
        {
            return mode switch
            {
                PhotoGroupMode.Day => FormatLongDate(date, culture),
                PhotoGroupMode.Year => date.Year.ToString(culture),
                _ => Capitalize(date.ToString("Y", culture), culture)
            };
        }

        /// <summary>
        /// Long date without the weekday (e.g. "October 3, 2026" / "3. Oktober 2026").
        /// </summary>
        public static string FormatLongDate(DateTime date, CultureInfo culture)
        {
            var pattern = culture.DateTimeFormat.LongDatePattern;
            pattern = pattern.Replace("dddd", string.Empty, StringComparison.Ordinal).Trim(' ', ',', '.', '،');
            if (pattern.StartsWith(',') || pattern.StartsWith('.'))
                pattern = pattern[1..].Trim();
            try
            {
                return Capitalize(date.ToString(pattern, culture), culture);
            }
            catch (FormatException)
            {
                return date.ToString("D", culture);
            }
        }

        private static string Capitalize(string text, CultureInfo culture) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0], culture) + text[1..];

        /// <summary>
        /// Finds the index of the row at the given vertical offset (binary search on Top).
        /// </summary>
        public static int FindRowAt(IReadOnlyList<GalleryRow> rows, double offset)
        {
            if (rows.Count == 0)
                return -1;
            int lo = 0, hi = rows.Count - 1;
            while (lo < hi)
            {
                var mid = (lo + hi + 1) / 2;
                if (rows[mid].Top <= offset)
                    lo = mid;
                else
                    hi = mid - 1;
            }
            return lo;
        }
    }
}
