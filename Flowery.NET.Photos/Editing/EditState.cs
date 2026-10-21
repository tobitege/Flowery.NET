using System.Collections.Immutable;

namespace Flowery.NET.Photos.Editing
{
    /// <summary>
    /// A freehand markup stroke. Points are normalized to the rotated/flipped (uncropped) image,
    /// so strokes stay in place when the crop changes. Width is relative to the longer image edge.
    /// </summary>
    public sealed record MarkupStroke(Color Color, float Width, bool Highlighter, ImmutableArray<Vector2> Points);

    /// <summary>
    /// Immutable description of all non-destructive edits applied to a photo.
    /// </summary>
    public sealed record EditState
    {
        // FullCrop must be initialized before Original (static field initializers run in declaration order).
        public static readonly Rect FullCrop = new(0, 0, 1, 1);
        public static readonly EditState Original = new();

        // Geometry
        public int Rotation { get; init; }
        public bool FlipH { get; init; }
        public bool FlipV { get; init; }
        public double Straighten { get; init; }
        public Rect Crop { get; init; } = FullCrop;

        // Light (-100..100)
        public double Brightness { get; init; }
        public double Exposure { get; init; }
        public double Contrast { get; init; }
        public double Highlights { get; init; }
        public double Shadows { get; init; }

        // Color (-100..100)
        public double Saturation { get; init; }
        public double Warmth { get; init; }
        public double Tint { get; init; }

        // Detail (-100..100)
        public double Sharpness { get; init; }
        public double Clarity { get; init; }

        // Effects
        public double Vignette { get; init; }
        public double Fade { get; init; }

        // Filter
        public string Filter { get; init; } = FilterPresets.NoneId;
        public double FilterIntensity { get; init; } = 100;

        // Markup
        public ImmutableList<MarkupStroke> Strokes { get; init; } = [];

        public bool HasGeometry =>
            Rotation % 4 != 0 || FlipH || FlipV || Math.Abs(Straighten) > 0.01 || Crop != FullCrop;

        public bool HasColorEdits =>
            Brightness != 0 || Exposure != 0 || Contrast != 0 || Highlights != 0 || Shadows != 0 ||
            Saturation != 0 || Warmth != 0 || Tint != 0 || Sharpness != 0 || Clarity != 0 ||
            Vignette != 0 || Fade != 0 || (Filter != FilterPresets.NoneId && FilterIntensity > 0);

        public bool HasChanges => HasGeometry || HasColorEdits || !Strokes.IsEmpty;

        public EditState Rotate(bool clockwise)
        {
            var crop = clockwise
                ? new Rect(1 - Crop.Bottom, Crop.X, Crop.Height, Crop.Width)
                : new Rect(Crop.Y, 1 - Crop.Right, Crop.Height, Crop.Width);
            return this with
            {
                Rotation = (Rotation + (clockwise ? 1 : 3)) % 4,
                Crop = crop,
                Strokes = Strokes.Select(stroke => stroke with
                {
                    Points = [.. stroke.Points.Select(point => clockwise
                        ? new Vector2(1 - point.Y, point.X)
                        : new Vector2(point.Y, 1 - point.X))]
                }).ToImmutableList()
            };
        }

        public EditState Mirror(bool horizontal) => horizontal
            ? this with { FlipH = !FlipH, Crop = new Rect(1 - Crop.Right, Crop.Y, Crop.Width, Crop.Height) }
            : this with { FlipV = !FlipV, Crop = new Rect(Crop.X, 1 - Crop.Bottom, Crop.Width, Crop.Height) };

        public EditState GeometryOnly() => Original with
        {
            Rotation = Rotation, FlipH = FlipH, FlipV = FlipV, Straighten = Straighten, Crop = Crop
        };

        public EditState ResetAdjustments() => this with
        {
            Brightness = 0,
            Exposure = 0,
            Contrast = 0,
            Highlights = 0,
            Shadows = 0,
            Saturation = 0,
            Warmth = 0,
            Tint = 0,
            Sharpness = 0,
            Clarity = 0,
            Vignette = 0,
            Fade = 0
        };

        public EditState ResetGeometry() => this with
        {
            Rotation = 0,
            FlipH = false,
            FlipV = false,
            Straighten = 0,
            Crop = FullCrop
        };
    }

    /// <summary>
    /// Linear undo/redo history of edit states. Rapid consecutive changes of the same property
    /// (e.g. slider drags) are coalesced into a single undo step.
    /// </summary>
    public sealed class EditHistory
    {
        private readonly List<EditState> _states = [EditState.Original];
        private int _index;
        private string? _lastKey;
        private long _lastTicks;

        public EditState Current => _states[_index];
        public bool CanUndo => _index > 0;
        public bool CanRedo => _index < _states.Count - 1;

        public event EventHandler? Changed;

        public void Reset()
        {
            _states.Clear();
            _states.Add(EditState.Original);
            _index = 0;
            _lastKey = null;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Apply(EditState state, string? coalesceKey = null)
        {
            if (state == Current)
                return;

            var now = Environment.TickCount64;
            var coalesce = coalesceKey != null && coalesceKey == _lastKey && now - _lastTicks < 900 && _index > 0;
            _lastKey = coalesceKey;
            _lastTicks = now;

            if (_index < _states.Count - 1)
                _states.RemoveRange(_index + 1, _states.Count - _index - 1);

            if (coalesce)
            {
                _states[_index] = state;
            }
            else
            {
                _states.Add(state);
                _index = _states.Count - 1;
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Undo()
        {
            if (!CanUndo)
                return;
            _index--;
            _lastKey = null;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void Redo()
        {
            if (!CanRedo)
                return;
            _index++;
            _lastKey = null;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Ends the current coalescing window so the next change creates a new undo step.
        /// </summary>
        public void Commit() => _lastKey = null;
    }
}
