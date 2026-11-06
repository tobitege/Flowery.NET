using Avalonia.Layout;
using DaisyCardPattern = Flowery.Enums.DaisyCardPattern;

namespace Flowery.NET.Kiosk.Slides;

public partial class PatternsSlide : UserControl
{
    // The entrance counts ticks of a 16 ms timer. A row of two cards fades in off screen, then both cards move in from
    // their side with an ease-out; each row starts RowDelay ticks after the one before and is created CreateLead ticks
    // before it starts.
    private const int FadeTicks = 10;
    private const int MoveTicks = 25;
    private const int RowDelay = 35;
    private const int CreateLead = 25;
    private const double StartOffset = 400;

    // Row by row, the left card first.
    private static readonly (DaisyCardPattern Pattern, DaisyColor Color, string Title)[] Cards =
    [
        (DaisyCardPattern.CarbonFiber, DaisyColor.Neutral, "Carbon Fiber"),
        (DaisyCardPattern.Honeycomb, DaisyColor.Primary, "Honeycomb"),
        (DaisyCardPattern.Circuit, DaisyColor.Secondary, "Circuit Board"),
        (DaisyCardPattern.DiamondPlate, DaisyColor.Neutral, "Diamond Plate"),
        (DaisyCardPattern.Dots, DaisyColor.Accent, "Subtle Dots"),
        (DaisyCardPattern.Stripes, DaisyColor.Default, "Dynamic Stripes")
    ];

    private readonly Border[] _slots;
    private readonly DaisyPatternedCard?[] _cards = new DaisyPatternedCard?[Cards.Length];
    private DispatcherTimer? _animTimer;
    private int _animTick;
    private TaskCompletionSource _entranceFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task EntranceFinished => _entranceFinished.Task;
    internal bool IsEntranceTimerRunning => _animTimer is not null;

    public PatternsSlide()
    {
        InitializeComponent();
        _slots = [Row0Left, Row0Right, Row1Left, Row1Right, Row2Left, Row2Right];
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        StopTimer();
        _entranceFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Array.Clear(_cards);
        _animTick = 0;

        _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animTimer.Tick += OnAnimTick;
        _animTimer.Start();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        StopTimer();
        _entranceFinished.TrySetCanceled();
    }

    private void StopTimer()
    {
        if (_animTimer is null)
            return;
        _animTimer.Stop();
        _animTimer.Tick -= OnAnimTick;
        _animTimer = null;
    }

    private void OnAnimTick(object? sender, EventArgs e)
    {
        _animTick++;
        for (var index = 0; index < Cards.Length; index++)
        {
            var start = index / 2 * RowDelay;
            if (_cards[index] is null && _animTick >= start - CreateLead)
                _slots[index].Child = _cards[index] = CreateCard(Cards[index], index % 2 == 0);
            if (_cards[index] is { } card)
                Animate(card, _animTick - start, index % 2 == 0 ? -1 : 1);
        }

        if (_animTick >= (Cards.Length / 2 - 1) * RowDelay + FadeTicks + MoveTicks)
        {
            StopTimer();
            _entranceFinished.TrySetResult();
        }
    }

    private static DaisyPatternedCard CreateCard((DaisyCardPattern Pattern, DaisyColor Color, string Title) card, bool left) => new()
    {
        Pattern = card.Pattern,
        ColorVariant = card.Color,
        Width = 200,
        Height = 100,
        Margin = new Thickness(10),
        HorizontalAlignment = left ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        RenderTransform = new TranslateTransform { X = left ? -StartOffset : StartOffset },
        Opacity = 0,
        Content = new TextBlock
        {
            Text = card.Title,
            FontWeight = FontWeight.Bold,
            FontSize = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        }
    };

    // Before its start a card stays hidden off screen; side is -1 for the left card and 1 for the right one.
    private static void Animate(DaisyPatternedCard card, int tick, int side)
    {
        var transform = (TranslateTransform)card.RenderTransform!;
        if (tick < FadeTicks)
        {
            card.Opacity = Math.Max(0, tick) / (double)FadeTicks;
            transform.X = side * StartOffset;
            return;
        }

        card.Opacity = 1;
        var moved = Math.Min(1, (tick - FadeTicks) / (double)MoveTicks);
        transform.X = side * StartOffset * Math.Pow(1 - moved, 3);
    }
}
