using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Flowery.Controls.Scheduler.Models;

namespace Flowery.Controls;

/// <summary>
/// The toolbar of a <see cref="DaisyScheduler"/>: the date range as a title that opens a date picker, previous and next,
/// Today, the host buttons and the settings on the first row, and the views as icon segments on the second.
/// <see cref="DaisyScheduler"/> shows one by default; set <see cref="DaisyScheduler.ShowToolbar"/> to false and place
/// a toolbar anywhere else, such as in a title bar, with its <see cref="Scheduler"/> set.
/// </summary>
public class DaisySchedulerToolbar : UserControl
{
    /// <summary>Defines the <see cref="Scheduler"/> property.</summary>
    public static readonly StyledProperty<DaisyScheduler?> SchedulerProperty =
        AvaloniaProperty.Register<DaisySchedulerToolbar, DaisyScheduler?>(nameof(Scheduler));

    // 16-unit line icons drawn with a 1.5 stroke.
    private const string PreviousIcon = "M10 3.5 5.5 8 10 12.5";
    private const string NextIcon = "M6 3.5 10.5 8 6 12.5";
    private const string SettingsIcon =
        "M2.5 4.5h6.5 M12 4.5h1.5 M10.5 3v3 M2.5 8h2 M7.5 8h6 M6 6.5v3 M2.5 11.5h7.5 M13 11.5h0.5 M11.5 10v3";
    private const string DayIcon = "M3.5 2.5h9a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1h-9a1 1 0 0 1-1-1v-9a1 1 0 0 1 1-1z M2.5 5.5h11 M5.5 8.5h5 M5.5 11h5";
    private const string WeekIcon = "M3.5 2.5h9a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1h-9a1 1 0 0 1-1-1v-9a1 1 0 0 1 1-1z M2.5 5.5h11 M6.2 5.5v8 M9.8 5.5v8";
    private const string MonthIcon = "M3.5 2.5h9a1 1 0 0 1 1 1v9a1 1 0 0 1-1 1h-9a1 1 0 0 1-1-1v-9a1 1 0 0 1 1-1z M2.5 5.5h11 M2.5 8.5h11 M2.5 11h11 M6.2 5.5v8 M9.8 5.5v8";
    private const string AgendaIcon = "M3 4.5h0.5 M6 4.5h7 M3 8h0.5 M6 8h7 M3 11.5h0.5 M6 11.5h7";
    private const string PlannerIcon = "M2.5 3.5v9 M4.5 5h4 M6.5 8h6 M4.5 11h5";
    private const string TimelineIcon = "M2.5 4h11 M4 2.5v3 M8 2.5v3 M12 2.5v3 M4 8.5h5 M7 11.5h5";
    private const string CalendarIcon = "M3.5 3.5h9a1 1 0 0 1 1 1v8a1 1 0 0 1-1 1h-9a1 1 0 0 1-1-1v-8a1 1 0 0 1 1-1z M5.5 2v3 M10.5 2v3 M2.5 6.5h11 M7.5 10h1";

    private DaisyScheduler? _attached;
    private double _height = 32;
    private double _iconSize = 20;

    /// <summary>Initializes a new toolbar.</summary>
    public DaisySchedulerToolbar() => Focusable = false;

    /// <summary>Gets or sets the scheduler this toolbar shows and controls.</summary>
    public DaisyScheduler? Scheduler
    {
        get => GetValue(SchedulerProperty);
        set => SetValue(SchedulerProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SchedulerProperty)
            Attach(Scheduler);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Attach(Scheduler);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Attach(null);
    }

    private void Attach(DaisyScheduler? scheduler)
    {
        if (!ReferenceEquals(_attached, scheduler) && _attached is not null)
            _attached.ToolbarChanged -= OnToolbarChanged;
        if (scheduler is not null && !ReferenceEquals(_attached, scheduler))
            scheduler.ToolbarChanged += OnToolbarChanged;
        _attached = scheduler;
        Rebuild();
    }

    private void OnToolbarChanged(object? sender, EventArgs e) => Rebuild();

    private void Rebuild()
    {
        if (_attached is not { } scheduler || !this.IsAttachedToVisualTree())
        {
            Content = null;
            return;
        }

        // Heights, icons and the title follow the design tokens of the scheduler size, which follows the size of the app.
        _height = scheduler.ToolbarButtonHeight;
        _iconSize = scheduler.ToolbarIconSize;
        FontSize = scheduler.FontSize;
        var (primary, secondary) = scheduler.ToolbarTitle();
        var title = new StackPanel { Orientation = Orientation.Horizontal, Spacing = scheduler.SpacingXS * 1.5 };
        title.Children.Add(new TextBlock { Text = primary, FontWeight = FontWeight.Bold, FontSize = scheduler.ToolbarTitleFontSize });
        if (secondary.Length > 0)
            title.Children.Add(new TextBlock
            {
                Text = secondary, FontSize = scheduler.ToolbarTitleFontSize, Foreground = scheduler.Brush("DaisyBaseContentMutedBrush")
            });
        var date = Action(scheduler, "Date", title, "ChooseDate");
        date.Click += (_, _) => scheduler.ShowDatePicker(date);
        var previous = Action(scheduler, "Previous", Icon(PreviousIcon), "Previous");
        previous.Click += (_, _) => scheduler.Navigate(-1);
        var next = Action(scheduler, "Next", Icon(NextIcon), "Next");
        next.Click += (_, _) => scheduler.Navigate(1);
        var today = Action(scheduler, "Today", new TextBlock { Text = DaisyScheduler.Text("Today"), FontWeight = FontWeight.SemiBold }, "Today");
        today.Classes.Add("today");
        today.MinHeight = Math.Round(_height * 0.8);
        today.Padding = new Thickness(Math.Round(_height * 0.4), 0);
        today.Click += (_, _) => scheduler.GoToToday();

        var left = new StackPanel { Orientation = Orientation.Horizontal, Spacing = scheduler.SpacingXS, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(date);
        left.Children.Add(previous);
        left.Children.Add(next);
        left.Children.Add(today);

        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = scheduler.SpacingXS, VerticalAlignment = VerticalAlignment.Center };
        foreach (var model in scheduler.ToolbarButtons)
            right.Children.Add(HostButton(scheduler, model, false));
        foreach (var model in scheduler.ToolbarAdornments)
            right.Children.Add(HostButton(scheduler, model, true));
        var settings = Action(scheduler, "Settings", Icon(SettingsIcon), "Settings");
        settings.Click += (_, _) => scheduler.ShowSettings(settings);
        right.Children.Add(settings);

        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        top.Children.Add(left);
        Grid.SetColumn(right, 1);
        top.Children.Add(right);

        var segments = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (var view in Enum.GetValues<SchedulerView>())
        {
            var segment = new Button { Theme = ButtonTheme("DaisySchedulerToolbarSegment"), Content = Icon(ViewIcon(view)), Tag = view };
            // The segments and the padding of their bar are as high as a toolbar button.
            segment.MinHeight = _height - 4;
            segment.Padding = new Thickness(Math.Round(_height * 0.45), 0);
            if (view == scheduler.CurrentView)
                segment.Classes.Add("selected");
            Describe(scheduler, segment, DaisyScheduler.Text(view.ToString()));
            segment.Click += (_, _) => scheduler.SetCurrentValue(DaisyScheduler.CurrentViewProperty, view);
            BindIconStroke(segment);
            segments.Children.Add(segment);
        }
        var views = new Border
        {
            Classes = { "daisy-scheduler-views" },
            Background = scheduler.Brush("DaisyBase200Brush"),
            CornerRadius = new CornerRadius(scheduler.ToolbarCornerRadius + 2),
            Padding = new Thickness(2),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, scheduler.SpacingXS * 2, 0, 0),
            Child = segments
        };
        Grid.SetRow(views, 1);

        var rows = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto") };
        rows.Children.Add(top);
        rows.Children.Add(views);
        Content = new Border
        {
            Child = rows,
            Padding = new Thickness(0, 0, 0, scheduler.SpacingXS * 2),
            BorderBrush = scheduler.Brush("DaisyBase300Brush"),
            BorderThickness = new Thickness(0, 0, 0, scheduler.LineThickness)
        };
    }

    private Button Action(DaisyScheduler scheduler, string tag, Control content, string nameKey)
    {
        var button = new Button { Theme = ButtonTheme("DaisySchedulerToolbarButton"), Content = content, Tag = tag };
        Fit(button);
        Describe(scheduler, button, DaisyScheduler.Text(nameKey), tooltip: content is Viewbox);
        BindIconStroke(button);
        return button;
    }

    private Button HostButton(DaisyScheduler scheduler, SchedulerToolbarButton model, bool adornment)
    {
        Control content;
        if (model.Image is { Length: > 0 } bytes)
            content = new DaisyScheduler.OwnedImage(bytes) { Width = 18 * scheduler.Scale, Height = 18 * scheduler.Scale };
        else if (model.IconData is { Length: > 0 } data)
            content = Icon(data);
        else if (model.Glyph is { Length: > 0 } glyph)
            content = new TextBlock { Text = model.Label is { Length: > 0 } label ? glyph + " " + label : glyph };
        else
            content = new TextBlock { Text = model.Label ?? model.Id ?? string.Empty };
        var button = new Button { Theme = ButtonTheme("DaisySchedulerToolbarButton"), Content = content, Tag = model, IsEnabled = model.IsEnabled };
        Fit(button);
        if (model.IsToggle && model.IsChecked)
            button.Classes.Add("checked");
        var name = model.Label ?? model.Tooltip ?? model.Id ?? string.Empty;
        AutomationProperties.SetName(button, name);
        scheduler.SetToolbarTooltip(button, model.Tooltip ?? (content is TextBlock ? null : model.Label));
        BindIconStroke(button);
        if (model.MenuItems is { Count: > 0 })
            button.Flyout = scheduler.CreateToolbarMenu(model, adornment);
        else
            button.Click += (_, _) => scheduler.InvokeToolbarAction(model, adornment);
        return button;
    }

    private static void Describe(DaisyScheduler scheduler, Control control, string name, bool tooltip = true)
    {
        AutomationProperties.SetName(control, name);
        if (tooltip)
            scheduler.SetToolbarTooltip(control, name);
    }

    private ControlTheme? ButtonTheme(string key) => this.TryFindResource(key, out var theme) ? theme as ControlTheme : null;

    // A 16-unit line icon, scaled with its stroke to the icon size of the scheduler.
    private Viewbox Icon(string data) => new()
    {
        Width = _iconSize, Height = _iconSize, VerticalAlignment = VerticalAlignment.Center,
        Child = new Path
        {
            Data = Geometry.Parse(data), Width = 16, Height = 16, Stretch = Stretch.None, StrokeThickness = 1.5,
            StrokeLineCap = PenLineCap.Round, StrokeJoin = PenLineJoin.Round
        }
    };

    // A toolbar button is as high as the buttons of the scheduler size; an icon button is square.
    private void Fit(Button button)
    {
        button.MinHeight = _height;
        if (button.Content is Viewbox)
        {
            button.MinWidth = _height;
            button.Padding = default;
        }
        else
        {
            button.Padding = new Thickness(Math.Round(_height * 0.3), 0);
        }
    }

    // A line icon takes the text color of its button, which follows hover, selection and the disabled state.
    private static void BindIconStroke(Button button)
    {
        if (button.Content is Viewbox { Child: Path path })
            path[!Shape.StrokeProperty] = button[!TemplatedControl.ForegroundProperty];
    }

    private static string ViewIcon(SchedulerView view) => view switch
    {
        SchedulerView.Day => DayIcon,
        SchedulerView.Week => WeekIcon,
        SchedulerView.Month => MonthIcon,
        SchedulerView.Agenda => AgendaIcon,
        SchedulerView.Planner => PlannerIcon,
        SchedulerView.Timeline => TimelineIcon,
        _ => CalendarIcon
    };
}
