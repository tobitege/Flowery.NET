using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Platform;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Composition;
using Flowery.Localization;
using Flowery.Theming;

namespace Flowery.Controls;

/// <summary>A window-scoped, modal guided tour. Add one host to the window's visual tree.</summary>
public sealed partial class DaisyTour : ContentControl
{
    private sealed class TourCardPeer(DaisyTour owner) : ControlAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(DaisyTour);
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;
        protected override string? GetNameCore() => AutomationProperties.GetName(((DaisyTour)Owner)._card);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TourCardPeer(this);

    private sealed class Session(DaisyTourStep[] steps, CancellationTokenSource cancellation)
    {
        internal readonly DaisyTourStep[] Steps = steps;
        internal readonly CancellationTokenSource Cancellation = cancellation;
        internal readonly List<IDisposable> Trackers = [];
        internal readonly List<Action> Unsubscribe = [];
        internal readonly List<Action> StepUnsubscribe = [];
        internal WeakReference<IInputElement>? PreviousFocus;
        internal CancellationTokenRegistration Registration;
        internal DaisyTourStep? PendingStep;
        internal bool Busy;
        internal bool Editing;
        // The visit of the current step: its number, its failed preparation, the warning it shows, the problems
        // already reported, the reports waiting until the step settles, and whether the hidden step target was
        // scrolled into view once since it was last visible.
        internal int Visit;
        internal Exception? PreparationError;
        internal DaisyTourTargetProblem? Warning;
        internal readonly HashSet<(DaisyTourTargetProblem, DaisyTourAnnotation?, Control?)> ReportedProblems = [];
        internal readonly List<TargetProblem> PendingReports = [];
        internal bool HiddenTargetRevealed;
    }

    private readonly record struct TargetProblem(DaisyTourTargetProblem Kind, DaisyTourAnnotation? Annotation, Control? Target, Exception Error)
    {
        public (DaisyTourTargetProblem, DaisyTourAnnotation?, Control?) Key => (Kind, Annotation, Target);

        // The warning names the most fundamental problem: a failed preparation, then the step target, then an annotation end.
        public int Priority => Kind switch { DaisyTourTargetProblem.Preparation => 0, DaisyTourTargetProblem.Target => 1, _ => 2 };
    }

    // Where the targets of a step are, and what keeps them from being shown.
    private sealed class TargetLayout
    {
        // The visible bounds of the step target; null when it cannot be shown.
        public Rect? Visible;
        public bool TargetHidden;
        public readonly Dictionary<Control, Rect> Bounds = [];
        public readonly List<Rect> Holes = [];
        public readonly HashSet<DaisyTourAnnotation> Hidden = [];
        public readonly List<TargetProblem> Problems = [];
    }

    // The overlay layer of the window shows the surface above the app, like a popup over the window content.
    // The scope gives the surface the theme variant of the tour instead of the window's.
    private OverlayLayer? _overlay;
    private readonly ThemeVariantScope _scope = new();
    private readonly Canvas _surface = new() { Background = new SolidColorBrush(Colors.Transparent) };
    private readonly Path _backdrop = new() { IsHitTestVisible = false };
    private readonly Path _outline = new() { IsHitTestVisible = false, StrokeThickness = 2 };
    private readonly Canvas _annotations = new() { IsHitTestVisible = false };
    // Avalonia's Grid has no padding; the card's border holds the padding of the card layout.
    private readonly Border _card = new() { CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), Padding = new Thickness(20) };
    private readonly Grid _cardLayout = new() { RowSpacing = 12 };
    private readonly TextBlock _title = new() { TextWrapping = TextWrapping.Wrap, FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _description = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _count = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Border _warningNote = new()
    {
        CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1), Padding = new Thickness(10, 6, 10, 6), IsVisible = false
    };
    private readonly TextBlock _warningNoteText = new() { TextWrapping = TextWrapping.Wrap };
    private readonly ContentPresenter _customContent = new();
    private readonly ScrollViewer _textScroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly ScrollViewer _progressScroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, IsTabStop = false };
    private readonly DaisySteps _progress = new();
    private readonly DaisyButton _back = new();
    private readonly DaisyButton _next = new();
    private readonly DaisyButton _skip = new();
    private readonly Grid _buttons = new() { ColumnSpacing = 8, RowSpacing = 8 };
    private readonly DaisyTourAnnotation _defaultAnnotation = new() { Id = "default-pointer" };
    private readonly List<DaisyTourAnnotationPresenter> _presenters = [];
    private readonly List<DaisyStepItem> _progressItems = [];
    private Session? _session;
    private Rect _visibleArea;
    // Where the card ended on the previous step; a card that must move off a course returns there when clear.
    private Rect _previousStepCard;
    private bool _updateQueued;
    private bool? _verticalButtons;
    // Opacity and the visibility of ancestors change no bounds, so the target trackers miss them; while a
    // session runs, this timer compares the availability of the step's targets with the last update.
    private DispatcherTimer? _availabilityTimer;
    private bool[]? _availability;
    // The session state is reported at the same points as in WinUI, not at each assignment, and each time for all
    // three properties.
    private bool _reportedOpen;
    private int _reportedIndex = -1;
    private DaisyTourStep? _reportedStep;

    public DaisyTour()
    {
        IsTabStop = false;
        IsHitTestVisible = false;
        _scope.Child = _surface;
        _surface.DetachedFromVisualTree += OnPopupClosed;
        _surface.Children.Add(_backdrop);
        _surface.Children.Add(_outline);
        _surface.Children.Add(_annotations);
        _surface.Children.Add(_card);
        _card.Child = _cardLayout;
        KeyboardNavigation.SetTabNavigation(_card, KeyboardNavigationMode.Cycle);
        for (var index = 0; index < 4; index++)
            _cardLayout.RowDefinitions.Add(new RowDefinition { Height = index == 0 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });
        var text = new StackPanel { Spacing = 8 };
        text.Children.Add(CreateCardHeader());
        _warningNote.Child = _warningNoteText;
        text.Children.Add(_warningNote);
        text.Children.Add(_description);
        text.Children.Add(_customContent);
        _textScroll.Content = text;
        // The numbered steps sit side by side and scroll horizontally instead of sharing the card's width.
        _progress.ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Orientation = Orientation.Horizontal });
        // The steps take the tour's size; the global size manager would give each loaded step the global size.
        FlowerySizeManager.SetIgnoreGlobalSize(_progress, true);
        _progressScroll.Content = _progress;
        AddRow(_textScroll, 0);
        AddRow(_count, 1);
        AddRow(_progressScroll, 2);
        AddRow(_buttons, 3);
        _buttons.Children.Add(_back);
        _buttons.Children.Add(_skip);
        _buttons.Children.Add(_next);
        _back.Click += OnBack;
        _next.Click += OnNext;
        _skip.Click += OnSkip;
        _surface.KeyDown += OnSurfaceKeyDown;
        _card.SizeChanged += OnCardSizeChanged;
        AutomationProperties.SetAutomationId(_back, "DaisyTourBack");
        AutomationProperties.SetAutomationId(_next, "DaisyTourNext");
        AutomationProperties.SetAutomationId(_skip, "DaisyTourSkip");
        AutomationProperties.SetAutomationId(_card, "DaisyTourCard");
        AutomationProperties.SetAutomationId(_warningNote, "DaisyTourWarning");
        AutomationProperties.SetLiveSetting(_card, AutomationLiveSetting.Polite);
        Steps.CollectionChanged += OnStepsChanged;
        // The surface's scope follows the tour's theme variant, also when the system or an ancestor scope changes it.
        ActualThemeVariantChanged += OnActualThemeVariantChanged;
    }

    public ObservableCollection<DaisyTourStep> Steps { get; } = [];
    public static readonly DirectProperty<DaisyTour, bool> IsOpenProperty =
        AvaloniaProperty.RegisterDirect<DaisyTour, bool>(nameof(IsOpen), tour => tour.IsOpen);
    public bool IsOpen => _session is not null;
    public static readonly DirectProperty<DaisyTour, int> CurrentIndexProperty =
        AvaloniaProperty.RegisterDirect<DaisyTour, int>(nameof(CurrentIndex), tour => tour.CurrentIndex, unsetValue: -1);
    public int CurrentIndex { get; private set; } = -1;
    public static readonly DirectProperty<DaisyTour, DaisyTourStep?> CurrentStepProperty =
        AvaloniaProperty.RegisterDirect<DaisyTour, DaisyTourStep?>(nameof(CurrentStep), tour => tour.CurrentStep);
    public DaisyTourStep? CurrentStep { get; private set; }
    public event EventHandler<DaisyTourClosedEventArgs>? Closed;
    /// <summary>A step cannot show everything it describes: its preparation failed, or its target or a connector end
    /// is missing, collapsed, transparent, in another window or outside the visible area after scrolling. The step
    /// stays open in a warning state. Raised after the step is laid out, once per cause and visit of the step, and
    /// not while the editor previews the tour.</summary>
    public event EventHandler<DaisyTourStepTargetUnavailableEventArgs>? StepTargetUnavailable;

    internal Rect TargetBounds { get; private set; }
    /// <summary>The problem the step's warning shows, or null without a warning.</summary>
    internal DaisyTourTargetProblem? Warning => _session?.Warning;
    internal Rect CardBounds { get; private set; }
    internal DaisySteps Progress => _progress;
    internal bool IsTransitioning => _session?.Busy == true;
    internal event EventHandler? GeometryUpdated;

    public static readonly StyledProperty<DaisySize> SizeProperty =
        AvaloniaProperty.Register<DaisyTour, DaisySize>(nameof(Size), DaisySize.Medium);
    public DaisySize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }
    public static readonly StyledProperty<double> HighlightPaddingProperty =
        AvaloniaProperty.Register<DaisyTour, double>(nameof(HighlightPadding), 6d);
    public double HighlightPadding { get => GetValue(HighlightPaddingProperty); set => SetValue(HighlightPaddingProperty, value); }
    public static readonly StyledProperty<double> HighlightCornerRadiusProperty =
        AvaloniaProperty.Register<DaisyTour, double>(nameof(HighlightCornerRadius), 8d);
    public double HighlightCornerRadius { get => GetValue(HighlightCornerRadiusProperty); set => SetValue(HighlightCornerRadiusProperty, value); }
    public static readonly StyledProperty<DaisyTourAnnotationStyle?> AnnotationDefaultsProperty =
        AvaloniaProperty.Register<DaisyTour, DaisyTourAnnotationStyle?>(nameof(AnnotationDefaults));
    public DaisyTourAnnotationStyle? AnnotationDefaults { get => GetValue(AnnotationDefaultsProperty); set => SetValue(AnnotationDefaultsProperty, value); }
    /// <summary>Places the step card beside the target (default) or at a fixed window position.</summary>
    public static readonly StyledProperty<DaisyTourCardPosition> CardPositionProperty =
        AvaloniaProperty.Register<DaisyTour, DaisyTourCardPosition>(nameof(CardPosition), DaisyTourCardPosition.Target);
    public DaisyTourCardPosition CardPosition { get => GetValue(CardPositionProperty); set => SetValue(CardPositionProperty, value); }
    /// <summary>Border of the step card. Without a brush, the <c>DaisyTourCardBorderBrush</c> resource or the muted theme text color applies.</summary>
    public static readonly StyledProperty<IBrush?> CardBorderBrushProperty =
        AvaloniaProperty.Register<DaisyTour, IBrush?>(nameof(CardBorderBrush));
    public IBrush? CardBorderBrush { get => GetValue(CardBorderBrushProperty); set => SetValue(CardBorderBrushProperty, value); }
    public static readonly StyledProperty<string?> NextTextProperty =
        AvaloniaProperty.Register<DaisyTour, string?>(nameof(NextText));
    public string? NextText { get => GetValue(NextTextProperty); set => SetValue(NextTextProperty, value); }
    public static readonly StyledProperty<string?> BackTextProperty =
        AvaloniaProperty.Register<DaisyTour, string?>(nameof(BackText));
    public string? BackText { get => GetValue(BackTextProperty); set => SetValue(BackTextProperty, value); }
    public static readonly StyledProperty<string?> SkipTextProperty =
        AvaloniaProperty.Register<DaisyTour, string?>(nameof(SkipText));
    public string? SkipText { get => GetValue(SkipTextProperty); set => SetValue(SkipTextProperty, value); }
    public static readonly StyledProperty<string?> FinishTextProperty =
        AvaloniaProperty.Register<DaisyTour, string?>(nameof(FinishText));
    public string? FinishText { get => GetValue(FinishTextProperty); set => SetValue(FinishTextProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StyleProfileProperty) OnStyleProfileChanged(this, change);
        // The session state (IsOpen, CurrentIndex, CurrentStep) is reported, not presented.
        else if (change.Property.OwnerType == typeof(DaisyTour) && !change.Property.IsDirect) OnPresentationChanged(this, change);
    }

    private static void OnPresentationChanged(AvaloniaObject sender, AvaloniaPropertyChangedEventArgs args)
    {
        var tour = (DaisyTour)sender;
        if (tour._session is { } session && args.Property == AnnotationDefaultsProperty)
            tour.ObserveCurrentStep(session);
        tour.RefreshPresentation();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        DaisyThemeManager.ThemeChanged += OnThemeChanged;
        DaisyAccessibility.PreferencesChanged += OnPreferencesChanged;
        FlowerySizeManager.SizeChanged += OnGlobalSizeChanged;
    }

    private void OnThemeChanged(object? sender, string themeName) => RefreshPresentation();
    private void OnPreferencesChanged(object? sender, EventArgs args) => RefreshPresentation();
    private void OnActualThemeVariantChanged(object? sender, EventArgs args) => RefreshPresentation();
    // A profile that inherits the size follows the global size, also while the tour has a size of its own.
    private void OnGlobalSizeChanged(object? sender, DaisySize size) => RefreshPresentation();

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        Cancel();
        DaisyThemeManager.ThemeChanged -= OnThemeChanged;
        DaisyAccessibility.PreferencesChanged -= OnPreferencesChanged;
        FlowerySizeManager.SizeChanged -= OnGlobalSizeChanged;
        base.OnUnloaded(e);
    }

    /// <summary>Starts a fresh session. Calling again cancels the previous session before replay.</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        VerifyAccess();
        var steps = Steps.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var step in steps)
        {
            if (string.IsNullOrWhiteSpace(step.Id) || !ids.Add(step.Id))
                throw new InvalidOperationException("Tour step IDs must be nonempty and unique.");
        }
        Cancel();
        if (steps.Length == 0) return;
        cancellationToken.ThrowIfCancellationRequested();
        if (TopLevel.GetTopLevel(this) is not { } root || OverlayLayer.GetOverlayLayer(this) is not { } overlay)
        {
            if (_editor is not null) throw new InvalidOperationException("The tour must belong to a window before opening the editor.");
            Closed?.Invoke(this, new(DaisyTourCloseReason.TargetUnavailable, steps[0].Id));
            return;
        }
        var session = new Session(steps, CancellationTokenSource.CreateLinkedTokenSource(cancellationToken));
        session.Editing = _editor is not null;
        _session = session;
        session.PreviousFocus = root.FocusManager?.GetFocusedElement() is { } focus ? new(focus) : null;
        _overlay = overlay;
        _surface.FlowDirection = FlowDirection.LeftToRight;
        ResizeSurface();
        overlay.Children.Add(_scope);
        overlay.PropertyChanged += OnRootChanged;
        FloweryLocalization.CultureChanged += OnCultureChanged;
        // A handler that marks the event handled must not open a way out of the modal tour.
        root.AddHandler(GettingFocusEvent, OnGettingFocus, RoutingStrategies.Bubble, handledEventsToo: true);
        session.Unsubscribe.Add(() => overlay.PropertyChanged -= OnRootChanged);
        session.Unsubscribe.Add(() => FloweryLocalization.CultureChanged -= OnCultureChanged);
        session.Unsubscribe.Add(() => root.RemoveHandler(GettingFocusEvent, OnGettingFocus));
        _availabilityTimer ??= CreateAvailabilityTimer();
        _availabilityTimer.Start();
        // Moving the window changes which part of it the display shows.
        if (root is Window window)
        {
            window.PositionChanged += OnAppWindowChanged;
            session.Unsubscribe.Add(() => window.PositionChanged -= OnAppWindowChanged);
        }
        // A view without a window keeps the card out of its safe-area insets, which change with the system bars.
        else if (root.InsetsManager is { } insets)
        {
            insets.SafeAreaChanged += OnSafeAreaChanged;
            session.Unsubscribe.Add(() => insets.SafeAreaChanged -= OnSafeAreaChanged);
        }
        // Registered last: a cancellation from another thread can close the session at once, and Close then
        // unsubscribes everything above.
        session.Registration = cancellationToken.Register(() => DispatchCancel(session));
        NotifyState();
        if (!ReferenceEquals(_session, session)) return;
        await TransitionAsync(session, 0);
    }

    public Task NextAsync()
    {
        VerifyAccess();
        if (_session is not { Busy: false } session) return Task.CompletedTask;
        if (CurrentIndex == session.Steps.Length - 1)
        {
            if (session.Editing) return Task.CompletedTask;
            Close(session, DaisyTourCloseReason.Completed);
            return Task.CompletedTask;
        }
        return TransitionAsync(session, CurrentIndex + 1);
    }

    public Task BackAsync()
    {
        VerifyAccess();
        return _session is { Busy: false } session && CurrentIndex > 0
            ? TransitionAsync(session, CurrentIndex - 1) : Task.CompletedTask;
    }

    public void Skip() { VerifyAccess(); if (_session is { } session) Close(session, DaisyTourCloseReason.Skipped); }
    public void Cancel() { VerifyAccess(); if (_session is { } session) Close(session, DaisyTourCloseReason.Cancelled); }

    private async Task TransitionAsync(Session session, int index)
    {
        _editor?.CancelGesture();
        ClearCardPositionPreview();
        session.Busy = true;
        _editor?.SetTransitioning(true);
        session.PendingStep = session.Steps[index];
        ClearStepObservers(session);
        _annotations.Children.Clear();
        _outline.Data = null;
        session.Visit++;
        session.Warning = null;
        session.PreparationError = null;
        session.ReportedProblems.Clear();
        session.PendingReports.Clear();
        session.HiddenTargetRevealed = false;
        _backdrop.Data = new RectangleGeometry { Rect = new Rect(0, 0, _surface.Width, _surface.Height) };
        _surface.Focusable = true;
        _surface.Focus();
        if (CurrentStep is null)
            _card.IsVisible = false;
        RefreshPresentation();
        if (_card.IsVisible)
            _skip.Focus();
        var step = session.PendingStep;
        var token = session.Cancellation.Token;
        try
        {
            // A failed preparation still shows the step, in its warning state.
            Exception? preparationError = null;
            if (step.PrepareAsync is { } prepare)
            {
                try { await prepare(step, token).WaitAsync(token); }
                catch (Exception error) when (!token.IsCancellationRequested) { preparationError = error; }
            }
            token.ThrowIfCancellationRequested();
            await RevealTargetsAsync(step, token);
            if (!ReferenceEquals(_session, session)) return;
            _previousStepCard = CardBounds;
            CurrentIndex = index;
            CurrentStep = step;
            session.PendingStep = null;
            session.PreparationError = preparationError;
            BuildProgress(session);
            ObserveCurrentStep(session);
            _card.IsVisible = true;
            RefreshPresentation();
            await RevealHiddenTargetAsync(session, step, token);
            if (!ReferenceEquals(_session, session)) return;
            if (!UpdateGeometry(session)) return;
            await DaisyTourLayout.SettleAsync(_surface, token);
            if (!ReferenceEquals(_session, session)) return;
            session.Busy = false;
            RefreshPresentation();
            ShowCurrentIndicator();
            if (_cardCollapsed) _cardToggle.Focus();
            else _next.Focus();
            _surface.Focusable = false;
            NotifyState();
            // Avalonia raises no automation notifications; the card is a polite live region, and its name
            // (UpdateCardName) carries the step.
            FlushReports(session);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error)
        {
            // Target problems keep the step open; this is a failure of the tour itself, such as laying out a step
            // that throws.
            if (ReferenceEquals(_session, session))
                Close(session, DaisyTourCloseReason.Failed, step.Target, error);
        }
        finally
        {
            session.Busy = false;
            if (ReferenceEquals(_session, session)) _editor?.SetTransitioning(false);
        }
    }

    // The highlight frame around each target is revealed together with the target.
    private Task RevealTargetsAsync(DaisyTourStep step, CancellationToken token) =>
        DaisyTourLayout.PrepareTargetsAsync(GetTargets(step), _surface, token,
            Math.Max(0, double.IsFinite(EffectiveHighlightPadding) ? EffectiveHighlightPadding : 0) + _outline.StrokeThickness);

    // Another scroll can move the step target out of view after its targets were revealed, such as a page
    // control bringing itself into view. The targets are revealed once more; a target that stays hidden puts
    // the step in its warning state, and it is not scrolled twice in a row without having been visible in between,
    // so the tour does not keep scrolling against another control that hides it.
    private async Task RevealHiddenTargetAsync(Session session, DaisyTourStep step, CancellationToken token)
    {
        if (session.HiddenTargetRevealed || step.Target is not { } target || !DaisyTourLayout.TryBounds(target, _surface, out var bounds) ||
            DaisyTourLayout.VisibleBounds(target, _surface, bounds) is { Width: > 0, Height: > 0 })
            return;
        session.HiddenTargetRevealed = true;
        await RevealTargetsAsync(step, token);
    }

    private static List<Control> GetTargets(DaisyTourStep step)
    {
        var targets = new List<Control>();
        if (step.Target is { } target) targets.Add(target);
        foreach (var annotation in step.Annotations)
        {
            if (annotation.Source is { } source && !targets.Contains(source)) targets.Add(source);
            if (annotation.Target is { } end && !targets.Contains(end)) targets.Add(end);
        }
        return targets;
    }

    private void ObserveCurrentStep(Session session)
    {
        ClearStepObservers(session);
        if (CurrentStep is not { } step) return;
        step.Changed += OnStepChanged;
        session.StepUnsubscribe.Add(() => step.Changed -= OnStepChanged);
        foreach (var annotation in step.Annotations)
        {
            annotation.Changed += OnAnnotationChanged;
            session.StepUnsubscribe.Add(() => annotation.Changed -= OnAnnotationChanged);
        }
        foreach (var style in new[] { AnnotationDefaults, step.AnnotationDefaults }.OfType<DaisyTourAnnotationStyle>())
        {
            style.Changed += OnAnnotationChanged;
            session.StepUnsubscribe.Add(() => style.Changed -= OnAnnotationChanged);
        }
        foreach (var target in GetTargets(step))
            session.Trackers.Add(new CompositionElementTracker(target, _surface, QueueGeometry));
        // Route limit controls move connectors without being highlighted or scrolled into view.
        foreach (var limit in step.Annotations.Select(annotation => annotation.RouteLimit).OfType<Control>().Distinct())
            session.Trackers.Add(new CompositionElementTracker(limit, _surface, QueueGeometry));
    }

    private static void ClearStepObservers(Session session)
    {
        foreach (var tracker in session.Trackers) tracker.Dispose();
        session.Trackers.Clear();
        foreach (var unsubscribe in session.StepUnsubscribe) unsubscribe();
        session.StepUnsubscribe.Clear();
    }

    private void OnStepChanged(object? sender, EventArgs args)
    {
        if (_session is not { Busy: false } session) return;
        ObserveCurrentStep(session);
        RefreshPresentation();
    }

    private void OnAnnotationChanged(object? sender, EventArgs args) => OnStepChanged(sender, args);

    private void QueueGeometry()
    {
        if (_updateQueued || _session is not { Busy: false } session) return;
        _updateQueued = true;
        Dispatcher.UIThread.Post(async () =>
        {
            if (!ReferenceEquals(_session, session) || session.Busy) { _updateQueued = false; return; }
            var token = session.Cancellation.Token;
            try
            {
                await DaisyTourLayout.SettleAsync(_surface, token);
                if (ReferenceEquals(_session, session) && !session.Busy && CurrentStep is { } step)
                    await RevealHiddenTargetAsync(session, step, token);
                if (ReferenceEquals(_session, session) && !session.Busy) UpdateGeometry(session, revealHidden: false);
            }
            catch (OperationCanceledException) { }
            finally { _updateQueued = false; }
        }, DispatcherPriority.Normal);
    }

    // Where the step's targets are and what keeps them from being shown. It only reads layout.
    private TargetLayout ResolveTargets(Session session, DaisyTourStep step)
    {
        var layout = new TargetLayout();
        if (session.PreparationError is { } preparation)
            layout.Problems.Add(new(DaisyTourTargetProblem.Preparation, null, step.Target, preparation));
        var targetMissing = false;
        if (step.Target is not { } target || !DaisyTourLayout.TryBounds(target, _surface, out var primary))
        {
            targetMissing = true;
            layout.Problems.Add(new(DaisyTourTargetProblem.Target, null, step.Target,
                new InvalidOperationException("The tour target is unavailable. " + DaisyTourLayout.Describe(step.Target, _surface))));
        }
        else if (DaisyTourLayout.VisibleBounds(target, _surface, primary) is { Width: > 0, Height: > 0 } visible)
        {
            layout.Visible = visible;
            layout.Holes.Add(visible);
            layout.Bounds[target] = visible;
        }
        else
        {
            layout.TargetHidden = true;
            layout.Problems.Add(new(DaisyTourTargetProblem.Target, null, target, new InvalidOperationException(
                $"The tour target is outside the visible area. Bounds={primary}; viewport={_surface.Bounds.Width}x{_surface.Bounds.Height}.")));
        }
        // Without annotations, the default pointer runs from the card to the step target.
        foreach (var annotation in step.Annotations.Count == 0 ? new[] { _defaultAnnotation } : step.Annotations.ToArray())
        {
            // Without its own target, an annotation ends at the step target and needs it on screen.
            if (layout.Visible is null && annotation.Target is null)
            {
                layout.Hidden.Add(annotation);
                continue;
            }
            var endpoints = new[] { annotation.Source, annotation.Target }.OfType<Control>().ToArray();
            var ends = new Rect[endpoints.Length];
            var available = true;
            for (var index = 0; index < endpoints.Length; index++)
            {
                if (DaisyTourLayout.TryBounds(endpoints[index], _surface, out ends[index])) continue;
                available = false;
                // An end that is the step target is reported as the step target.
                if (targetMissing && ReferenceEquals(endpoints[index], step.Target)) continue;
                layout.Problems.Add(new(DaisyTourTargetProblem.Annotation, annotation, endpoints[index],
                    new InvalidOperationException("A connector end is unavailable. " + DaisyTourLayout.Describe(endpoints[index], _surface))));
            }
            if (!available)
            {
                layout.Hidden.Add(annotation);
                continue;
            }
            for (var index = 0; index < endpoints.Length; index++)
            {
                // Connectors keep the endpoint's real position, even partly outside the visible area.
                // Only its visible part is highlighted.
                layout.Bounds.TryAdd(endpoints[index], ends[index]);
                var clipped = DaisyTourLayout.VisibleBounds(endpoints[index], _surface, ends[index]);
                if (clipped is { Width: > 0, Height: > 0 } && !layout.Holes.Contains(clipped)) layout.Holes.Add(clipped);
            }
        }
        return layout;
    }

    // A target that cannot be shown never closes the tour: the step stays open in its warning state, without the
    // target's highlight and the connectors that need it, and the card waits at a fixed position.
    private bool UpdateGeometry(Session session, bool revealHidden = true)
    {
        if (CurrentStep is not { } step) return false;
        var layout = ResolveTargets(session, step);
        if (layout.Visible is not null)
            session.HiddenTargetRevealed = false;
        else if (layout.TargetHidden && revealHidden && !session.Busy && !session.HiddenTargetRevealed)
        {
            // Direct updates, such as a card size change, can run while another scroll hides the target.
            // The queued update scrolls the targets into view once before the warning shows.
            QueueGeometry();
            return false;
        }
        _availability = Availability(step);
        TargetBounds = layout.Visible ?? default;
        _visibleArea = _overlay is not null && TopLevel.GetTopLevel(this) is { } root ? DaisyTourLayout.VisibleArea(root) : new Rect(0, 0, _surface.Width, _surface.Height);
        _editor?.ArrangeInspector(new Size(_visibleArea.Width, _visibleArea.Height), new Point(_visibleArea.X, _visibleArea.Y));
        DaisyTourTargetProblem? warning = layout.Problems.Count == 0 ? null : layout.Problems.MinBy(problem => problem.Priority).Kind;
        if (warning != session.Warning)
        {
            // Avalonia raises no automation notifications; the warning starts the card's name, and the card is a
            // polite live region.
            session.Warning = warning;
            ApplyWarning();
            if (warning is null && session.Editing) _editor?.OnPreviewTargetAvailable();
        }
        LayoutCard();
        // Avalonia measures a valid card again only for a new constraint. Text that changed inside it is measured in
        // the next layout pass, and OnCardSizeChanged places the card again before that frame renders.
        _card.Measure(new Size(_card.Width, _card.MaxHeight));
        var highlighted = layout.Holes.Count > 0;
        var allTargets = highlighted ? layout.Holes[0] : default;
        foreach (var hole in layout.Holes) allTargets = allTargets.Union(hole);
        // The card is placed in the visible part of the window, in coordinates relative to that part.
        var viewport = new Size(_visibleArea.Width, _visibleArea.Height);
        var stepPosition = StepSetting(step, DaisyTourStep.CardPositionProperty, step.CardPosition, DaisyTourCardPosition.Default);
        var position = stepPosition == DaisyTourCardPosition.Default ? EffectiveCardPosition : stepPosition;
        if (layout.Visible is null && position is (DaisyTourCardPosition.Default or DaisyTourCardPosition.Target))
            position = DaisyTourCardPosition.Center;
        var placement = StepSetting(step, DaisyTourStep.PlacementProperty, step.Placement, DaisyTourPlacement.Auto);
        var frame = Math.Max(0, double.IsFinite(EffectiveHighlightPadding) ? EffectiveHighlightPadding : 0) + _outline.StrokeThickness;
        var targets = Shift(allTargets, -_visibleArea.X, -_visibleArea.Y);
        // Without anything highlighted, nothing is in the card's way.
        Rect? highlight = highlighted ? Inflate(targets, frame) : null;
        CardBounds = _editor is not null
            ? PlaceEditorCard(_card.DesiredSize)
            : Shift(DaisyTourLayout.PlaceStationary(position, _card.DesiredSize, viewport, highlight)
                ?? DaisyTourLayout.Place(targets, _card.DesiredSize, viewport, placement), _visibleArea.X, _visibleArea.Y);
        var cardX = StepSetting(step, DaisyTourStep.CardXProperty, step.CardX, double.NaN);
        var cardY = StepSetting(step, DaisyTourStep.CardYProperty, step.CardY, double.NaN);
        var manualCard = double.IsFinite(cardX) || double.IsFinite(cardY);
        if (manualCard)
        {
            var travel = CardTravelArea(_card.DesiredSize);
            CardBounds = new Rect(
                double.IsFinite(cardX) ? travel.X + Math.Clamp(cardX, 0, 1) * travel.Width : Math.Clamp(CardBounds.X, travel.Left, travel.Right),
                double.IsFinite(cardY) ? travel.Y + Math.Clamp(cardY, 0, 1) * travel.Height : Math.Clamp(CardBounds.Y, travel.Top, travel.Bottom),
                CardBounds.Width, CardBounds.Height);
        }
        ApplyCardPositionPreview();
        Canvas.SetLeft(_card, CardBounds.X);
        Canvas.SetTop(_card, CardBounds.Y);
        var cutout = new GeometryGroup { FillRule = FillRule.EvenOdd };
        cutout.Children!.Add(new RectangleGeometry { Rect = new Rect(0, 0, _surface.Width, _surface.Height) });
        var outlines = new GeometryGroup { FillRule = FillRule.NonZero };
        var expandedHoles = new List<Rect>();
        foreach (var hole in layout.Holes)
        {
            var padding = Math.Max(0, double.IsFinite(EffectiveHighlightPadding) ? EffectiveHighlightPadding : 0);
            var expanded = new Rect(hole.X - padding, hole.Y - padding, hole.Width + 2 * padding, hole.Height + 2 * padding);
            // Adjacent/overlapping targets share one highlight. Disjoint cutouts must
            // never toggle each other's opacity in the even-odd backdrop geometry.
            for (var index = expandedHoles.Count - 1; index >= 0; index--)
            {
                // Avalonia's Intersect returns an empty rectangle for adjacent rectangles; they touch.
                if (!Touches(expanded, expandedHoles[index])) continue;
                expanded = expanded.Union(expandedHoles[index]);
                expandedHoles.RemoveAt(index);
                index = expandedHoles.Count;
            }
            expandedHoles.Add(expanded);
        }
        foreach (var expanded in expandedHoles)
        {
            var geometry = RoundedRectangle(expanded, EffectiveHighlightCornerRadius);
            cutout.Children!.Add(geometry);
            outlines.Children!.Add(RoundedRectangle(expanded, EffectiveHighlightCornerRadius));
        }
        _backdrop.Data = cutout;
        _outline.Data = outlines;
        UpdateAnnotations(step, layout);
        // A card covering a course between controls, or the area its route limit keeps clear, moves away.
        // Courses from the card itself follow it.
        var courses = CourseBounds(step, layout, -_visibleArea.X, -_visibleArea.Y);
        if (_editor is null && !manualCard && courses.Count > 0 && DaisyTourLayout.Covers(Shift(CardBounds, -_visibleArea.X, -_visibleArea.Y), courses))
        {
            var obstacles = new List<Rect>(courses);
            foreach (var hole in layout.Holes) obstacles.Add(Shift(Inflate(hole, frame), -_visibleArea.X, -_visibleArea.Y));
            Rect? kept = _previousStepCard.Width > 0
                ? new Rect(_previousStepCard.X - _visibleArea.X, _previousStepCard.Y - _visibleArea.Y, _card.DesiredSize.Width, _card.DesiredSize.Height)
                : null;
            if (DaisyTourLayout.AvoidObstacles(position, placement, highlighted ? targets : null, _card.DesiredSize, viewport, obstacles, kept) is { } clear)
            {
                CardBounds = Shift(clear, _visibleArea.X, _visibleArea.Y);
                Canvas.SetLeft(_card, CardBounds.X);
                Canvas.SetTop(_card, CardBounds.Y);
                UpdateAnnotations(step, layout);
            }
        }
        GeometryUpdated?.Invoke(this, EventArgs.Empty);
        _editor?.UpdateHandles();
        QueueReports(session, layout.Problems);
        return true;
    }

    // Reports wait until the step is laid out and settled, so a handler may close, replay or move the tour; a handler
    // that throws is logged without affecting the tour. Each problem is reported once per visit of the step, and
    // reports of a visit that is over are dropped. While the editor previews the tour, the editor shows them instead.
    private void QueueReports(Session session, List<TargetProblem> problems)
    {
        foreach (var problem in problems)
        {
            if (session.ReportedProblems.Add(problem.Key)) session.PendingReports.Add(problem);
        }
        if (!session.Busy) FlushReports(session);
    }

    private void FlushReports(Session session)
    {
        if (session.PendingReports.Count == 0) return;
        var reports = session.PendingReports.ToArray();
        var visit = session.Visit;
        session.PendingReports.Clear();
        Dispatcher.UIThread.Post(() => Report(session, visit, reports), DispatcherPriority.Normal);
    }

    private void Report(Session session, int visit, TargetProblem[] reports)
    {
        foreach (var problem in reports)
        {
            // A handler may have closed, replayed or moved the tour; the rest belongs to a visit that is over.
            if (!ReferenceEquals(_session, session) || session.Visit != visit || session.Busy || CurrentStep is not { } step) return;
            var args = new DaisyTourStepTargetUnavailableEventArgs(step.Id, problem.Kind, problem.Annotation?.Id, problem.Target, problem.Error);
            if (session.Editing) _editor?.OnPreviewTargetUnavailable(args);
            else Flowery.Services.FloweryEventRaiser.Raise(StepTargetUnavailable, this, args, nameof(StepTargetUnavailable));
        }
    }

    // The card frame, a note above the description and the current progress step take the warning color.
    // Contrast colors take precedence over it.
    private void ApplyWarning()
    {
        var contrast = DaisyAccessibility.IsHighContrast;
        var foreground = ThemeBrush("DaisyBaseContentBrush");
        var warningBrush = ThemeBrush("DaisyWarningBrush");
        var active = Warning is not null;
        _warningNote.IsVisible = active;
        _warningNote.Background = contrast ? new SolidColorBrush(Colors.Transparent) : warningBrush;
        _warningNote.BorderBrush = contrast ? GetBorderBrush() : warningBrush;
        _warningNoteText.Foreground = contrast ? foreground : ThemeBrush("DaisyWarningContentBrush");
        _warningNoteText.Text = FloweryLocalization.GetStringInternal(
            Warning == DaisyTourTargetProblem.Preparation ? "Tour_PreparationFailed" : "Tour_TargetUnavailable");
        _card.BorderBrush = active && !contrast ? warningBrush : CardBorder(foreground);
        _card.BorderThickness = new Thickness(active ? 2 : 1);
        for (var index = 0; index < _progressItems.Count; index++)
            _progressItems[index].Color = active && index == CurrentIndex ? DaisyStepColor.Warning : DaisyStepColor.Default;
        UpdateCardName();
    }

    // Screen readers read the card as one name, starting with the warning when there is one.
    private void UpdateCardName()
    {
        if (CurrentStep is not { } step) return;
        var descriptions = step.Annotations.Select(annotation => annotation.Text).Where(text => !string.IsNullOrWhiteSpace(text));
        var note = Warning is null ? string.Empty : _warningNoteText.Text + " ";
        AutomationProperties.SetName(_card, $"{note}{step.Title}. {step.Description}. {_count.Text}. {string.Join(". ", descriptions)}");
    }

    private DispatcherTimer CreateAvailabilityTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += OnAvailabilityTick;
        return timer;
    }

    private void OnAvailabilityTick(object? sender, EventArgs args)
    {
        if (_session is { Busy: false } && CurrentStep is { } step && (_availability is null || !Availability(step).SequenceEqual(_availability)))
            QueueGeometry();
    }

    // Which of the step's targets can be measured at all: collapsed or transparent ones and their ancestors cannot.
    private bool[] Availability(DaisyTourStep step) =>
        GetTargets(step).Select(target => DaisyTourLayout.TryBounds(target, _surface, out _)).ToArray();

    // Segment bounds of the connector courses between controls, padded by half the stroke plus 4 DIP
    // of clearance, and the areas their route limits keep clear.
    private List<Rect> CourseBounds(DaisyTourStep step, TargetLayout layout, double x, double y)
    {
        var courses = new List<Rect>();
        for (var index = 0; index < step.Annotations.Count && index < _presenters.Count; index++)
        {
            var annotation = step.Annotations[index];
            if (annotation.Source is not { } source || layout.Hidden.Contains(annotation)) continue;
            var presenter = _presenters[index];
            var course = presenter.Course;
            for (var point = 1; point < course.Count; point++)
                courses.Add(Shift(Inflate(new Rect(course[point - 1], course[point]).Normalize(), presenter.CourseThickness / 2 + 4), x, y));
            // Without its own target, a connector ends at the step target.
            var end = annotation.Target is { } target ? layout.Bounds[target] : TargetBounds;
            if (course.Count > 0 && RouteZone(annotation, layout.Bounds[source], end) is { } zone)
                courses.Add(Shift(zone, x, y));
        }
        return courses;
    }

    // Beyond the route limit, between the centers of both ends, up to the farther end.
    private Rect? RouteZone(DaisyTourAnnotation annotation, Rect source, Rect target)
    {
        if (RouteLimitFor(annotation) is not { } limit)
            return null;
        var a = new Point(source.X + source.Width / 2, source.Y + source.Height / 2);
        var b = new Point(target.X + target.Width / 2, target.Y + target.Height / 2);
        var value = limit.Value;
        return limit.Side switch
        {
            DaisyTourRouteSide.Above when Math.Max(a.Y, b.Y) > value =>
                new Rect(new Point(Math.Min(a.X, b.X), value), new Point(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y))),
            DaisyTourRouteSide.Below when Math.Min(a.Y, b.Y) < value =>
                new Rect(new Point(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), new Point(Math.Max(a.X, b.X), value)),
            DaisyTourRouteSide.LeftOf when Math.Max(a.X, b.X) > value =>
                new Rect(new Point(value, Math.Min(a.Y, b.Y)), new Point(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y))),
            DaisyTourRouteSide.RightOf when Math.Min(a.X, b.X) < value =>
                new Rect(new Point(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), new Point(value, Math.Max(a.Y, b.Y))),
            _ => null
        };
    }

    // Annotations that need a target that cannot be shown are hidden. The default pointer ends at the step target.
    private void UpdateAnnotations(DaisyTourStep step, TargetLayout layout)
    {
        var definitions = step.Annotations.Count == 0 ? new[] { _defaultAnnotation } : step.Annotations.ToArray();
        if (_presenters.Count != definitions.Length || _annotations.Children.Count != definitions.Length)
        {
            _presenters.Clear();
            _annotations.Children.Clear();
            foreach (var _ in definitions)
            {
                var presenter = new DaisyTourAnnotationPresenter();
                _presenters.Add(presenter);
                _annotations.Children.Add(presenter);
            }
        }
        for (var index = 0; index < definitions.Length; index++)
        {
            var annotation = definitions[index];
            var presenter = _presenters[index];
            presenter.Width = _surface.Width;
            presenter.Height = _surface.Height;
            var foreground = ThemeBrush("DaisyBaseContentBrush");
            var scale = EffectiveSize switch
            {
                DaisySize.ExtraSmall => 0.8, DaisySize.Small => 0.9, DaisySize.Large => 1.15, DaisySize.ExtraLarge => 1.3, _ => 1
            };
            var layers = ProfileLayers(step, annotation);
            if (layout.Hidden.Contains(annotation))
            {
                // A hidden annotation still resolves its style, so the editor inspects its current values.
                presenter.Update(annotation, CardBounds, CardBounds,
                    ThemeBrush("DaisyPrimaryBrush"), ThemeBrush("DaisyBase100Brush"),
                    foreground, AnnotationDefaults, step.AnnotationDefaults, scale, _card.FlowDirection, RouteLimitFor(annotation), layers);
                presenter.IsVisible = false;
                continue;
            }
            presenter.IsVisible = true;
            var end = annotation.Target is { } target ? layout.Bounds[target] : TargetBounds;
            var kind = DaisyTourAnnotationPresenter.ResolveKind(annotation, AnnotationDefaults, step.AnnotationDefaults, layers);
            var source = annotation.Source is { } start ? layout.Bounds[start] : CardBounds;
            if (kind != DaisyTourAnnotationKind.Custom)
            {
                // Connectors and tails start and end at the highlight frame instead of crossing it.
                var frame = Math.Max(0, double.IsFinite(EffectiveHighlightPadding) ? EffectiveHighlightPadding : 0) + _outline.StrokeThickness / 2;
                end = Inflate(end, frame);
                if (annotation.Source is not null) source = Inflate(source, frame);
            }
            if (kind == DaisyTourAnnotationKind.Custom)
                source = MarkerBounds(annotation.Source is null ? end : source);
            presenter.WidthLimit = double.PositiveInfinity;
            if (annotation.Source is null && kind is DaisyTourAnnotationKind.Cloud or DaisyTourAnnotationKind.SpeechBubble)
                source = PlaceSurface(presenter, annotation, step, end, layout.Bounds.Values, foreground, scale);
            presenter.Update(annotation, source, end,
                ThemeBrush("DaisyPrimaryBrush"), ThemeBrush("DaisyBase100Brush"),
                foreground, AnnotationDefaults, step.AnnotationDefaults, scale, _card.FlowDirection, RouteLimitFor(annotation), layers);
        }
    }

    // Surfaces go beside their target in one of eight directions. Without a fitting direction,
    // the surface narrows in steps down to its minimum width before it is clamped into view.
    private Rect PlaceSurface(DaisyTourAnnotationPresenter presenter, DaisyTourAnnotation annotation, DaisyTourStep step,
        Rect target, IEnumerable<Rect> highlighted, IBrush foreground, double scale)
    {
        var frame = Math.Max(0, double.IsFinite(EffectiveHighlightPadding) ? EffectiveHighlightPadding : 0) + _outline.StrokeThickness;
        var highlights = highlighted.Select(rect => Inflate(rect, frame)).ToList();
        if (_editor is { } editor) highlights.Add(editor.InspectorBounds);
        var viewport = new Size(_visibleArea.Width, _visibleArea.Height);
        var (dx, dy) = (_visibleArea.X, _visibleArea.Y);
        var visibleTarget = Shift(target, -dx, -dy);
        var card = Shift(CardBounds, -dx, -dy);
        highlights = highlights.Select(highlight => Shift(highlight, -dx, -dy)).ToList();
        var rightToLeft = _card.FlowDirection == FlowDirection.RightToLeft;
        for (var limit = double.PositiveInfinity; ; )
        {
            presenter.WidthLimit = limit;
            var measured = presenter.MeasureSurface(annotation, foreground, AnnotationDefaults, step.AnnotationDefaults, scale, _card.FlowDirection, ProfileLayers(step, annotation));
            var placed = DaisyTourLayout.PlaceAnnotation(visibleTarget, card, viewport, measured, highlights,
                presenter.PlacementDirection, rightToLeft, out var direction);
            if (direction >= 0)
                presenter.PlacementDirection = direction;
            // The limit falls strictly, so content with a fixed width ends the search as well.
            limit = Math.Min(limit, measured.Width) * 0.8;
            if (direction >= 0 || limit < 80 * scale)
                return Shift(placed, dx, dy);
        }
    }

    // A custom marker is centered on its anchor. An ellipse inscribed in this box
    // encloses the anchor's complete highlight, including its corners.
    private Rect MarkerBounds(Rect anchor)
    {
        var margin = Math.Max(0, double.IsFinite(EffectiveHighlightPadding) ? EffectiveHighlightPadding : 0) + _outline.StrokeThickness;
        var width = (anchor.Width + 2 * margin) * Math.Sqrt(2);
        var height = (anchor.Height + 2 * margin) * Math.Sqrt(2);
        return new Rect(anchor.X + anchor.Width / 2 - width / 2, anchor.Y + anchor.Height / 2 - height / 2, width, height);
    }

    // A limit control that cannot be measured leaves the connector unrestricted.
    private DaisyTourRouteLimit? RouteLimitFor(DaisyTourAnnotation annotation)
    {
        var side = AnnotationSetting(annotation, DaisyTourAnnotation.RouteLimitSideProperty, annotation.RouteLimitSide, DaisyTourRouteSide.None);
        if (side == DaisyTourRouteSide.None)
            return null;
        if (annotation.RouteLimit is not { } element)
        {
            var coordinate = AnnotationSetting(annotation, DaisyTourAnnotation.RouteLimitCoordinateProperty, annotation.RouteLimitCoordinate, double.NaN);
            return double.IsFinite(coordinate) ? new DaisyTourRouteLimit(side, coordinate) : null;
        }
        if (!DaisyTourLayout.TryBounds(element, _surface, out var bounds))
            return null;
        var configuredMargin = AnnotationSetting(annotation, DaisyTourAnnotation.RouteLimitMarginProperty, annotation.RouteLimitMargin, 12d);
        var margin = double.IsFinite(configuredMargin) ? Math.Max(0, configuredMargin) : 12;
        return new DaisyTourRouteLimit(side, side switch
        {
            DaisyTourRouteSide.Above => bounds.Top - margin,
            DaisyTourRouteSide.Below => bounds.Bottom + margin,
            DaisyTourRouteSide.LeftOf => bounds.Left - margin,
            _ => bounds.Right + margin
        });
    }

    private static Rect Shift(Rect rect, double x, double y) => new(rect.X + x, rect.Y + y, rect.Width, rect.Height);

    private static Rect Inflate(Rect rect, double margin) =>
        new(rect.X - margin, rect.Y - margin, rect.Width + 2 * margin, rect.Height + 2 * margin);

    // Overlapping or adjacent rectangles, like a nonempty WinUI intersection.
    private static bool Touches(Rect first, Rect second) =>
        first.Left <= second.Right && second.Left <= first.Right && first.Top <= second.Bottom && second.Top <= first.Bottom;

    private void BuildProgress(Session session)
    {
        _progressItems.Clear();
        _progress.Items.Clear();
        for (var index = 0; index < session.Steps.Length; index++)
        {
            var button = new Button
            {
                Tag = index, Padding = new Thickness(0), BorderThickness = new Thickness(0),
                Background = new SolidColorBrush(Colors.Transparent), MinWidth = 0, MinHeight = 0
            };
            button.Click += OnProgressClick;
            AutomationProperties.SetAutomationId(button, "DaisyTourStep" + (index + 1).ToString(CultureInfo.InvariantCulture));
            var item = new DaisyStepItem
            {
                DataContent = (index + 1).ToString(CultureInfo.CurrentCulture), IsCompact = true, NavigationButton = button
            };
            _progressItems.Add(item);
            _progress.Items.Add(item);
        }
        _progress.SelectedIndex = CurrentIndex;
    }

    private void RefreshPresentation()
    {
        if (_session is not { } session) return;
        _scope.RequestedThemeVariant = ActualThemeVariant;
        // Fluent's theme of a ThemeVariantScope sets a text color of its own; the card content keeps the theme's.
        TextElement.SetForeground(_scope, ThemeBrush("DaisyBaseContentBrush"));
        _surface.FlowDirection = FlowDirection.LeftToRight;
        _card.FlowDirection = FloweryLocalization.Instance.IsRtl ? FlowDirection.RightToLeft : FlowDirection;
        _progress.FlowDirection = FlowDirection.LeftToRight;
        _card.Background = ThemeBrush("DaisyBase100Brush");
        var foreground = ThemeBrush("DaisyBaseContentBrush");
        _title.Foreground = _description.Foreground = _count.Foreground = foreground;
        _outline.Stroke = ThemeBrush("DaisyPrimaryBrush");
        _backdrop.Fill = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0));
        _back.Content = BackText ?? FloweryLocalization.GetStringInternal("Tour_Back");
        _skip.Content = SkipText ?? FloweryLocalization.GetStringInternal("Tour_Skip");
        _next.Content = CurrentIndex == session.Steps.Length - 1
            ? FinishText ?? FloweryLocalization.GetStringInternal("Tour_Finish")
            : NextText ?? FloweryLocalization.GetStringInternal("Tour_Next");
        _back.IsEnabled = !session.Busy && CurrentIndex > 0;
        _progress.IsEnabled = !session.Busy;
        for (var index = 0; index < _progressItems.Count; index++)
        {
            if (_progressItems[index].NavigationButton is not { } button) continue;
            var label = string.Format(CultureInfo.CurrentCulture, FloweryLocalization.GetStringInternal("Tour_StepCount"), index + 1, session.Steps.Length);
            label += ". " + session.Steps[index].Title;
            AutomationProperties.SetName(button, label);
            ToolTip.SetTip(button, label);
        }
        _next.IsEnabled = !session.Busy && (!session.Editing || CurrentIndex < session.Steps.Length - 1);
        _skip.IsVisible = !session.Editing;
        _cardMove.IsVisible = session.Editing;
        _cardMove.IsEnabled = session.Editing && !session.Busy;
        _cardSave.IsVisible = _cardMove.IsVisible;
        _cardToggle.IsVisible = _cardMove.IsVisible;
        _cardToggle.IsEnabled = session.Editing && !session.Busy;
        if (_cardToggle.Content is PathIcon toggleIcon) toggleIcon.Data = CardToggleIcon(_cardCollapsed);
        var toggleCardLabel = FloweryLocalization.GetStringInternal(_cardCollapsed ? "TourEditor_ExpandCard" : "TourEditor_CollapseCard");
        AutomationProperties.SetName(_cardToggle, toggleCardLabel);
        ToolTip.SetTip(_cardToggle, toggleCardLabel);
        _cardSave.IsEnabled = session.Editing && !session.Busy && CardPositionChanged;
        var moveCardLabel = FloweryLocalization.GetStringInternal("TourEditor_MoveCard");
        AutomationProperties.SetName(_cardMove, moveCardLabel);
        ToolTip.SetTip(_cardMove, moveCardLabel);
        var saveCardLabel = FloweryLocalization.GetStringInternal("TourEditor_SaveCard");
        AutomationProperties.SetName(_cardSave, saveCardLabel);
        ToolTip.SetTip(_cardSave, saveCardLabel);
        _back.Size = _skip.Size = _next.Size = _progress.Size = EffectiveSize;
        _title.FontSize = FlowerySizeManager.GetFontSizeForTier(ResponsiveFontTier.SectionHeader, EffectiveSize);
        _description.FontSize = _count.FontSize = _warningNoteText.FontSize = FlowerySizeManager.GetFontSizeForTier(ResponsiveFontTier.Primary, EffectiveSize);
        if (CurrentStep is { } step)
        {
            _title.Text = step.Title;
            _description.Text = step.Description;
            _customContent.ContentTemplate = step.ContentTemplate;
            _customContent.Content = step.ContentTemplate is null ? null : step;
            _count.Text = string.Format(CultureInfo.CurrentCulture, FloweryLocalization.GetStringInternal("Tour_StepCount"), CurrentIndex + 1, session.Steps.Length);
        }
        ApplyWarning();
        QueueGeometry();
    }

    // The card frame differs from the Primary highlight and from the Base300 borders around it.
    // Contrast colors take precedence over the decorative color.
    private IBrush CardBorder(IBrush foreground)
    {
        if (DaisyAccessibility.IsHighContrast)
            return GetBorderBrush();
        if ((TourSetting(CardBorderBrushProperty, CardBorderBrush, (IBrush?)null) ??
             (this.TryFindResource("DaisyTourCardBorderBrush", ActualThemeVariant, out var resource) ? resource as IBrush : null)) is { } brush)
            return brush;
        return foreground is ISolidColorBrush solid
            ? new SolidColorBrush(solid.Color) { Opacity = 0.45 }
            : GetBorderBrush();
    }

    // The theme border, or the text color in contrast mode.
    private static IBrush GetBorderBrush() =>
        ThemeBrush(DaisyAccessibility.IsHighContrast ? "DaisyBaseContentBrush" : "DaisyBase300Brush");

    private static IBrush ThemeBrush(string key) => DaisyResourceLookup.GetBrush(key) ?? new SolidColorBrush(Colors.Transparent);

    private void LayoutCard()
    {
        // Measure the expanded card first so editor-only folding cannot change authored coordinates.
        SetCardCompact(false);
        var width = Math.Max(1, Math.Min(480, _visibleArea.Width - 24));
        _card.Width = width;
        _card.MaxHeight = Math.Max(1, _visibleArea.Height - 24);
        _card.Padding = new Thickness(width < 300 ? 12 : 20);
        _textScroll.MaxHeight = Math.Max(24, _visibleArea.Height * 0.38);
        _progressScroll.MaxWidth = Math.Max(1, width - 40);
        _progressScroll.IsVisible = width >= 240;
        // Numbered DaisySteps remain at the selected size and can scroll horizontally.
        var vertical = width < 340 || EffectiveSize == DaisySize.ExtraLarge;
        if (_verticalButtons == vertical) return;
        _verticalButtons = vertical;
        _buttons.ColumnDefinitions.Clear();
        _buttons.RowDefinitions.Clear();
        for (var index = 0; index < 3; index++)
        {
            if (vertical) _buttons.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            else _buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var button = _buttons.Children[index];
            Grid.SetRow(button, vertical ? index : 0);
            Grid.SetColumn(button, vertical ? 0 : index);
        }
    }

    private void ShowCurrentIndicator()
    {
        if (_cardCollapsed) return;
        if (CurrentIndex < 0 || CurrentIndex >= _progressItems.Count) return;
        // Centered like WinUI's BringIntoView with a horizontal ratio of 0.5; Avalonia's scrolls only to the nearest edge.
        // The center is measured in the scroll viewer, which includes the mirroring of right-to-left cards, and moved
        // relative to the current offset, which the viewer applies the same way in both directions.
        var item = _progressItems[CurrentIndex];
        if (item.TranslatePoint(new Point(item.Bounds.Width / 2, 0), _progressScroll) is not { } center) return;
        var maximum = Math.Max(0, _progressScroll.Extent.Width - _progressScroll.Viewport.Width);
        _progressScroll.Offset = new Vector(Math.Clamp(_progressScroll.Offset.X + center.X - _progressScroll.Viewport.Width / 2, 0, maximum),
            _progressScroll.Offset.Y);
    }

    private static PathGeometry RoundedRectangle(Rect rect, double requestedRadius)
    {
        var radius = Math.Clamp(double.IsFinite(requestedRadius) ? requestedRadius : 0, 0, Math.Min(rect.Width, rect.Height) / 2);
        var figure = new PathFigure { StartPoint = new Point(rect.Left + radius, rect.Top), IsClosed = true, IsFilled = true };
        figure.Segments!.Add(new LineSegment { Point = new Point(rect.Right - radius, rect.Top) });
        figure.Segments!.Add(new QuadraticBezierSegment { Point1 = new Point(rect.Right, rect.Top), Point2 = new Point(rect.Right, rect.Top + radius) });
        figure.Segments!.Add(new LineSegment { Point = new Point(rect.Right, rect.Bottom - radius) });
        figure.Segments!.Add(new QuadraticBezierSegment { Point1 = new Point(rect.Right, rect.Bottom), Point2 = new Point(rect.Right - radius, rect.Bottom) });
        figure.Segments!.Add(new LineSegment { Point = new Point(rect.Left + radius, rect.Bottom) });
        figure.Segments!.Add(new QuadraticBezierSegment { Point1 = new Point(rect.Left, rect.Bottom), Point2 = new Point(rect.Left, rect.Bottom - radius) });
        figure.Segments!.Add(new LineSegment { Point = new Point(rect.Left, rect.Top + radius) });
        figure.Segments!.Add(new QuadraticBezierSegment { Point1 = new Point(rect.Left, rect.Top), Point2 = new Point(rect.Left + radius, rect.Top) });
        var geometry = new PathGeometry();
        geometry.Figures!.Add(figure);
        return geometry;
    }

    private void ResizeSurface()
    {
        if (_overlay is not { } root) return;
        _surface.Width = root.Bounds.Width;
        _surface.Height = root.Bounds.Height;
        _annotations.Width = root.Bounds.Width;
        _annotations.Height = root.Bounds.Height;
        _editor?.ArrangeInspector(root.Bounds.Size);
    }

    private void OnRootChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property != BoundsProperty) return;
        if (TopLevel.GetTopLevel(this) is not { } root || TopLevel.GetTopLevel(_surface) != root) { Cancel(); return; }
        ResizeSurface();
        if (_surface.Width <= 0 || _surface.Height <= 0) return;
        QueueGeometry();
    }

    // Content that loads after placement, such as the progress items, can still change the card's size.
    // Layout raises this before the frame renders, so the card is placed again without a visible jump.
    private void OnCardSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        if (_session is { PendingStep: null } session && CurrentStep is not null &&
            (Math.Abs(args.NewSize.Height - CardBounds.Height) > 0.5 || Math.Abs(args.NewSize.Width - CardBounds.Width) > 0.5))
            UpdateGeometry(session);
    }

    private void OnAppWindowChanged(object? sender, PixelPointEventArgs args) => QueueGeometry();
    private void OnSafeAreaChanged(object? sender, SafeAreaChangedArgs args) => QueueGeometry();

    private void OnCultureChanged(object? sender, CultureInfo culture)
    {
        if (Dispatcher.UIThread.CheckAccess()) RefreshPresentation();
        else Dispatcher.UIThread.Post(RefreshPresentation);
    }

    private void OnGettingFocus(object? sender, FocusChangingEventArgs args)
    {
        // The handler sits on the tour's top level, so focus moves in popups with a top level of their own never
        // reach it. Popups that the editor opens in the overlay layer have an overlay popup host.
        if (_session is not null && args.NewFocusedElement is Visual element &&
            !DaisyTourLayout.IsDescendant(element, _surface) &&
            (_editor is null || !element.GetVisualAncestors().Any(ancestor => ancestor is OverlayPopupHost)))
            args.TrySetNewFocusedElement(_session.Busy ? _surface : _next);
    }

    private void OnSurfaceKeyDown(object? sender, KeyEventArgs args)
    {
        if (_editor is { } editor) { editor.HandleKey(args); return; }
        if (args.Key == Key.Escape) { Skip(); args.Handled = true; }
    }

    private async void OnBack(object? sender, RoutedEventArgs args) => await BackAsync();
    private async void OnProgressClick(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: int index } && _session is { Busy: false } session &&
            index >= 0 && index < session.Steps.Length && index != CurrentIndex)
            await TransitionAsync(session, index);
    }
    private async void OnNext(object? sender, RoutedEventArgs args) => await NextAsync();
    private void OnSkip(object? sender, RoutedEventArgs args) => Skip();
    private void OnPopupClosed(object? sender, VisualTreeAttachmentEventArgs args)
    {
        // Close removes the surface after it ended the session; anything else that removes it, also with an
        // ancestor, ends the session.
        if (_session is not null) Cancel();
    }
    private void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs args) => Cancel();

    private void DispatchCancel(Session session)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            if (ReferenceEquals(_session, session)) Close(session, DaisyTourCloseReason.Cancelled);
        }
        else Dispatcher.UIThread.Post(() => { if (ReferenceEquals(_session, session)) Close(session, DaisyTourCloseReason.Cancelled); });
    }

    private void Close(Session session, DaisyTourCloseReason reason, Control? target = null, Exception? error = null)
    {
        if (!ReferenceEquals(_session, session)) return;
        var stepId = (session.PendingStep ?? CurrentStep)?.Id;
        _session = null;
        _cardCollapsed = false;
        _updateQueued = false;
        _availabilityTimer?.Stop();
        _availability = null;
        try { session.Cancellation.Cancel(); }
        catch (AggregateException cancellationError) { error ??= cancellationError; }
        session.Registration.Dispose();
        ClearStepObservers(session);
        foreach (var unsubscribe in session.Unsubscribe) unsubscribe();
        session.Unsubscribe.Clear();
        _overlay?.Children.Remove(_scope);
        _overlay = null;
        _annotations.Children.Clear();
        _presenters.Clear();
        _progressItems.Clear();
        // Without a session there is no warning to show.
        ApplyWarning();
        _backdrop.Data = _outline.Data = null;
        _customContent.Content = null;
        _progress.Items.Clear();
        CurrentIndex = -1;
        CurrentStep = null;
        TargetBounds = CardBounds = _previousStepCard = default;
        session.PendingStep = null;
        session.Cancellation.Dispose();
        if (session.PreviousFocus?.TryGetTarget(out var previous) == true && previous is Control control &&
            TopLevel.GetTopLevel(control) is not null && control.IsEffectivelyEnabled && control.IsVisible)
            control.Focus();
        NotifyState();
        if (session.Editing) _editor?.OnPreviewClosed(error);
        else Closed?.Invoke(this, new(reason, stepId, target, error));
    }

    private void NotifyState()
    {
        var (open, index, step) = (_reportedOpen, _reportedIndex, _reportedStep);
        (_reportedOpen, _reportedIndex, _reportedStep) = (IsOpen, CurrentIndex, CurrentStep);
        RaisePropertyChanged(IsOpenProperty, open, IsOpen);
        RaisePropertyChanged(CurrentIndexProperty, index, CurrentIndex);
        RaisePropertyChanged(CurrentStepProperty, step, CurrentStep);
    }

    private void AddRow(Control element, int row)
    {
        Grid.SetRow(element, row);
        _cardLayout.Children.Add(element);
    }
}
