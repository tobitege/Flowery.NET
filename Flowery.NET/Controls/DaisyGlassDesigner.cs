using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Flowery.Controls.ColorPicker;
using Flowery.Enums;
using Flowery.Helpers;
using Flowery.Localization;

namespace Flowery.Controls
{
    /// <summary>
    /// A visual editor for a glass look (<see cref="DaisyGlassDesign"/>): a live preview over a colorful backdrop, the
    /// settings in tabs (presets, button, glass, fill, pattern, motion), and the result as XAML, C# or JSON. The preview
    /// is a <see cref="DaisyButton"/> with the glass button theme; its text, variant, style, size and shape are preview
    /// options, not part of the design, which applies to every control on glass.
    /// Embed it in a page, or open it over the app with <see cref="ShowOverlayAsync"/> or <see cref="EditAsync"/>.
    /// </summary>
    public class DaisyGlassDesigner : UserControl
    {
        private enum Tab { Presets, Button, Glass, Fill, Pattern, Motion }

        private enum CodeKind { Xaml, CSharp, Json }

        private enum Backdrop { Aurora, Sunset, Night, Paper }

        private const int MaxFillStops = 5;

        private DaisyGlassDesign _design = new();
        private bool _loading;
        private Tab _tab = Tab.Presets;
        private CodeKind _codeKind = CodeKind.Xaml;
        private Backdrop _backdrop = Backdrop.Aurora;

        private bool _codeOpen;

        // The preview and the disc survive a rebuild for another language; the other parts are built again.
        private readonly DaisyButton _preview;
        private string _previewText = "Get started";
        private TextBox? _textBox;
        private readonly Border _blob;
        private Border _stage = null!;
        private DaisyGlassDesignerFieldPanel _fields = null!;
        private Border _codeDrawer = null!;
        private SelectableTextBlock _codeText = null!;
        private DaisyCopyButton _copyButton = null!;
        private StackPanel _importPanel = null!;
        private TextBox _importBox = null!;
        private TextBlock _importError = null!;
        private readonly Dictionary<Tab, RadioButton> _tabButtons = new();
        private CancellationTokenSource? _motion;
        private DaisyToggle? _move;
        private CultureInfo? _builtCulture;
        // One suffix per designer keeps its radio groups apart from those of another designer.
        private readonly string _groups = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Occurs when the design changes through the editor, a preset, an import or <see cref="Design"/>.
        /// </summary>
        public event EventHandler? DesignChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="DaisyGlassDesigner"/> class.
        /// </summary>
        public DaisyGlassDesigner()
        {
            _preview = new DaisyButton
            {
                Content = _previewText,
                Theme = GlassButtonTheme(),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            // The size and the theme set the font of the preview, which the size of a square or circle follows.
            _preview.PropertyChanged += (_, e) =>
            {
                if (e.Property == FontSizeProperty || e.Property == FontFamilyProperty || e.Property == FontWeightProperty)
                    UpdatePreviewShapeSize();
            };
            _blob = new Border
            {
                Width = 120,
                Height = 120,
                CornerRadius = new CornerRadius(60),
                Background = new SolidColorBrush(Color.FromUInt32(0xFFFACC15)),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(40, 0, 0, 0),
                IsHitTestVisible = false,
                RenderTransform = new TranslateTransform()
            };
            Build();
            Load(_design);
        }

        private void Build()
        {
            StopMotion();
            _builtCulture = FloweryLocalization.CurrentCulture;
            (_preview.Parent as Panel)?.Children.Remove(_preview);
            (_blob.Parent as Panel)?.Children.Remove(_blob);
            _tabButtons.Clear();

            _stage = CreateStage();
            _fields = new DaisyGlassDesignerFieldPanel();

            _codeText = new SelectableTextBlock { Classes = { "gbd-code-text" }, TextWrapping = TextWrapping.NoWrap };
            _copyButton = new DaisyCopyButton { Size = DaisySize.Small, ButtonStyle = DaisyButtonStyle.Soft };
            _importBox = new TextBox { AcceptsReturn = true, Height = 140, TextWrapping = TextWrapping.Wrap };
            _importError = new TextBlock { Classes = { "gbd-error" }, TextWrapping = TextWrapping.Wrap, IsVisible = false };
            _importPanel = CreateImportPanel();
            _codeDrawer = CreateCodeDrawer();

            Content = new Border
            {
                Classes = { "gbd-root" },
                Child = new StackPanel
                {
                    Spacing = 20,
                    Children =
                    {
                        CreateHeader(),
                        _stage,
                        CreateTextRow(),
                        CreateTabBar(),
                        new Border { Padding = new Thickness(24, 0), Child = _fields },
                        _codeDrawer
                    }
                }
            };
            _codeDrawer.IsVisible = _codeOpen;
        }

        /// <summary>
        /// Gets or sets the design being edited. Getting returns a copy; setting loads a copy into the editor.
        /// </summary>
        public DaisyGlassDesign Design
        {
            get => _design.Clone();
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                Load(value.Clone());
                DesignChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets the button of the preview, which shows the current design.
        /// </summary>
        public DaisyButton PreviewButton => _preview;

        /// <summary>
        /// Gets or sets the text of the preview button. It is a preview option, not part of the design.
        /// </summary>
        public string PreviewText
        {
            get => _previewText;
            set
            {
                _previewText = value ?? "";
                if (_textBox != null && _textBox.Text != _previewText)
                    _textBox.Text = _previewText;
                UpdatePreviewContent();
            }
        }

        /// <summary>
        /// Gets or sets the variant of the preview button, whose color tints the glass. A preview option.
        /// </summary>
        public DaisyButtonVariant PreviewVariant
        {
            get => _preview.Variant;
            set => _preview.Variant = value;
        }

        /// <summary>
        /// Gets or sets the style of the preview button. A preview option.
        /// </summary>
        public DaisyButtonStyle PreviewButtonStyle
        {
            get => _preview.ButtonStyle;
            set => _preview.ButtonStyle = value;
        }

        /// <summary>
        /// Gets or sets the size of the preview button. A preview option.
        /// </summary>
        public DaisySize PreviewSize
        {
            get => _preview.Size;
            set => _preview.Size = value;
        }

        /// <summary>
        /// Gets or sets the shape of the preview button. A preview option.
        /// </summary>
        public DaisyButtonShape PreviewShape
        {
            get => _preview.Shape;
            set
            {
                _preview.Shape = value;
                UpdatePreviewContent();
            }
        }

        private void UpdatePreviewContent()
        {
            _preview.Content = _previewText;
            // A block button takes the full width; the preview centers every other shape.
            var block = _preview.Shape == DaisyButtonShape.Block;
            _preview.HorizontalAlignment = block ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
            _preview.Margin = block ? new Thickness(32, 0) : default;
            UpdatePreviewShapeSize();
        }

        // A square or circle DaisyButton is as wide as it is high and made for an icon. In the preview it grows until the
        // whole text fits inside the shape, with room around it.
        private void UpdatePreviewShapeSize()
        {
            _preview.ClearValue(HeightProperty);
            if (_preview.Shape is not (DaisyButtonShape.Square or DaisyButtonShape.Circle))
                return;
            using var layout = new TextLayout(_previewText, new Typeface(_preview.FontFamily, _preview.FontStyle, _preview.FontWeight),
                _preview.FontSize, null);
            var room = _preview.FontSize;
            var side = _preview.Shape == DaisyButtonShape.Circle
                ? Math.Sqrt(layout.Width * layout.Width + layout.Height * layout.Height) + room
                : layout.Width + 2 * room;
            // The square style binds the width to the height.
            if (side > _preview.Height || double.IsNaN(_preview.Height))
                _preview.Height = Math.Ceiling(side);
        }

        /// <summary>
        /// Gets the current design as an element with the attached glass properties.
        /// </summary>
        public string Xaml => _design.ToXaml();

        /// <summary>
        /// Gets the current design as C# statements that set the attached glass properties.
        /// </summary>
        public string CSharp => _design.ToCSharp();

        /// <summary>
        /// Gets the current design as JSON, readable by <see cref="ImportJson"/>.
        /// </summary>
        public string Json => _design.ToJson();

        /// <summary>
        /// Loads the glass look of <paramref name="control"/> into the editor. A <see cref="DaisyButton"/> also sets the
        /// preview options.
        /// </summary>
        public void LoadFrom(Control control)
        {
            ArgumentNullException.ThrowIfNull(control);
            if (control is DaisyButton button)
                TakePreviewOptions(button);
            Design = DaisyGlassDesign.FromControl(control);
        }

        /// <summary>
        /// Puts <paramref name="control"/> on glass with the current design.
        /// </summary>
        public void ApplyTo(Control control) => _design.ApplyTo(control);

        /// <summary>
        /// Loads a design from JSON written by <see cref="Json"/>.
        /// </summary>
        /// <exception cref="JsonException">The text is not a valid design.</exception>
        public void ImportJson(string json) => Design = DaisyGlassDesign.FromJson(json);

        /// <summary>
        /// Returns to the default look; the preview options stay.
        /// </summary>
        public void Reset() => Design = new DaisyGlassDesign();

        private void TakePreviewOptions(DaisyButton button)
        {
            if (button.Content is string text)
                PreviewText = text;
            PreviewVariant = button.Variant;
            PreviewButtonStyle = button.ButtonStyle;
            PreviewSize = button.Size;
            PreviewShape = button.Shape;
        }

        private static ControlTheme? GlassButtonTheme() =>
            Application.Current?.FindResource("DaisyGlassButtonTheme") as ControlTheme;

        /// <summary>
        /// Opens a designer over the app and returns the design when the user applies it, or <c>null</c> when the user
        /// cancels. It works on every platform, since it uses the overlay layer of the window or view.
        /// </summary>
        /// <param name="target">Any visual in the window or view to open the designer over.</param>
        /// <param name="initial">The design to start with, or <c>null</c> for the default look.</param>
        /// <exception cref="InvalidOperationException"><paramref name="target"/> has no overlay layer.</exception>
        public static Task<DaisyGlassDesign?> ShowOverlayAsync(Visual target, DaisyGlassDesign? initial = null) =>
            ShowOverlayAsync(target, initial, previewFrom: null);

        private static Task<DaisyGlassDesign?> ShowOverlayAsync(Visual target, DaisyGlassDesign? initial, DaisyButton? previewFrom)
        {
            ArgumentNullException.ThrowIfNull(target);
            var overlay = OverlayLayer.GetOverlayLayer(target)
                          ?? throw new InvalidOperationException("No overlay layer found for the target visual.");

            var result = new TaskCompletionSource<DaisyGlassDesign?>();
            var designer = new DaisyGlassDesigner();
            if (previewFrom is not null)
                designer.TakePreviewOptions(previewFrom);
            if (initial is not null)
                designer.Design = initial;

            var cancel = new DaisyButton { Content = L("GlassDesigner_Cancel", "Cancel"), ButtonStyle = DaisyButtonStyle.Soft };
            var apply = new DaisyButton { Content = L("GlassDesigner_Apply", "Apply"), Variant = DaisyButtonVariant.Primary };
            AutomationProperties.SetAutomationId(cancel, "GlassDesigner_Cancel");
            AutomationProperties.SetAutomationId(apply, "GlassDesigner_Apply");
            var card = new Border
            {
                Classes = { "gbd-overlay-card" },
                MaxWidth = 1040,
                // Anchored at the top, so the card does not jump when a tab changes its height.
                Margin = new Thickness(16, 40, 16, 16),
                Child = new StackPanel
                {
                    Children =
                    {
                        designer,
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 8,
                            Margin = new Thickness(24, 0, 24, 24),
                            Children = { cancel, apply }
                        }
                    }
                }
            };
            // The overlay keeps the keyboard inside it and gives the focus back when it closes.
            var focusManager = TopLevel.GetTopLevel(target)?.FocusManager;
            var previousFocus = focusManager?.GetFocusedElement();
            var backdrop = new Border
            {
                Classes = { "gbd-overlay" },
                Width = overlay.Bounds.Width,
                Height = overlay.Bounds.Height,
                Focusable = true,
                Child = new ScrollViewer
                {
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = new Panel { Children = { card } }
                }
            };

            void Resize(object? sender, EventArgs e)
            {
                backdrop.Width = overlay.Bounds.Width;
                backdrop.Height = overlay.Bounds.Height;
            }

            void Close(DaisyGlassDesign? design)
            {
                overlay.LayoutUpdated -= Resize;
                // Set before the backdrop leaves the tree, which would otherwise report a cancel.
                result.TrySetResult(design);
                overlay.Children.Remove(backdrop);
                previousFocus?.Focus();
            }

            // The window or view closes while the overlay is open: the caller gets null instead of waiting forever.
            void Detached(object? sender, VisualTreeAttachmentEventArgs e)
            {
                backdrop.DetachedFromVisualTree -= Detached;
                overlay.LayoutUpdated -= Resize;
                result.TrySetResult(null);
            }

            overlay.LayoutUpdated += Resize;
            cancel.Click += (_, _) => Close(null);
            apply.Click += (_, _) => Close(designer.Design);
            backdrop.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Escape)
                    return;
                e.Handled = true;
                Close(null);
            };
            KeyboardNavigation.SetTabNavigation(backdrop, KeyboardNavigationMode.Cycle);
            overlay.Children.Add(backdrop);
            backdrop.DetachedFromVisualTree += Detached;
            backdrop.Focus();
            return result.Task;
        }

        /// <summary>
        /// Opens a designer over the app with the glass look of <paramref name="control"/> and applies the result to it.
        /// A <see cref="DaisyButton"/> also sets the preview options, which stay a preview: the text, variant, style,
        /// size and shape of the control do not change. On a control on glass only what the user changed is written, so
        /// its styles and glass theme keep applying to the rest; a control not on glass yet gets the whole design. A fill
        /// or animated background the design cannot describe
        /// stays as it is (see <see cref="DaisyGlassFillKind.Keep"/>).
        /// </summary>
        /// <returns><c>true</c> when the user applied a design; <c>false</c> when the user cancelled.</returns>
        public static async Task<bool> EditAsync(Control control)
        {
            ArgumentNullException.ThrowIfNull(control);
            var original = DaisyGlassDesign.FromControl(control);
            // A control not on glass yet has no theme or glass that holds the values equal to the defaults, so it gets
            // the whole design.
            var onGlass = DaisyGlassSurface.GetGlass(control) is not null;
            if (await ShowOverlayAsync(control, original, control as DaisyButton) is not { } design)
                return false;

            design.ApplyTo(control, reuseBackground: false, onGlass ? original : null);
            return true;
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            FloweryLocalization.CultureChanged += OnCultureChanged;
            // A language change while detached was not seen; a disc that moved before detaching moves again.
            if (!Equals(_builtCulture, FloweryLocalization.CurrentCulture))
                OnCultureChanged(this, FloweryLocalization.CurrentCulture);
            else if (_move?.IsChecked == true)
                StartMotion();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            FloweryLocalization.CultureChanged -= OnCultureChanged;
            StopMotion();
        }

        private void OnCultureChanged(object? sender, CultureInfo culture)
        {
            Build();
            ShowTab(_tab);
            UpdateCode();
            if (_move?.IsChecked == true && this.IsAttachedToVisualTree())
                StartMotion();
        }

        private static string L(string key, string fallback) => FloweryLocalization.GetString(key, fallback);

        private void Load(DaisyGlassDesign design)
        {
            _loading = true;
            try
            {
                _design = design;
                _design.ApplyTo(_preview, reuseBackground: true);
                ShowKept();
                ShowTab(_tab);
                UpdateCode();
            }
            finally
            {
                _loading = false;
            }
        }

        // ApplyTo leaves a Keep fill or background as it is, which on the preview is the one of an earlier design: the
        // preview shows the kept fill as it was read from the control, and no animated background.
        private void ShowKept()
        {
            if (_design.FillKind == DaisyGlassFillKind.Keep)
            {
                DaisyGlassSurface.SetFill(_preview, _design.KeptFill);
                DaisyGlassSurface.SetGradient(_preview, DaisyGlassGradient.None);
            }
            if (_design.BackgroundRenderer == DaisyGlassBackgroundRenderer.Keep)
                DaisyGlassSurface.SetBackground(_preview, null);
        }

        private void Change(Action<DaisyGlassDesign> edit, bool rebuildTab = false)
        {
            if (_loading)
                return;

            edit(_design);
            _design.ApplyTo(_preview, reuseBackground: true);
            ShowKept();
            if (rebuildTab)
                ShowTab(_tab);
            UpdateCode();
            DesignChanged?.Invoke(this, EventArgs.Empty);
        }

        // ---------------------------------------------------------------- Header, stage and tabs

        private Control CreateHeader()
        {
            var code = new ToggleButton
            {
                Classes = { "gbd-code-toggle" },
                Content = "</>  " + L("GlassDesigner_Code", "Code"),
                IsChecked = _codeOpen
            };
            code.IsCheckedChanged += (_, _) =>
            {
                _codeOpen = code.IsChecked == true;
                _codeDrawer.IsVisible = _codeOpen;
                UpdateCode();
            };

            var titles = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock { Classes = { "gbd-title" }, Text = L("GlassDesigner_LookTitle", "Glass Designer") },
                    new TextBlock
                    {
                        Classes = { "gbd-subtitle" },
                        Text = L("GlassDesigner_LookSubtitle", "Shape a glass look live, then take the code to any control on glass."),
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };
            code.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(code, 1);
            return new Grid
            {
                Margin = new Thickness(24, 24, 24, 0),
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children = { titles, code }
            };
        }

        private Border CreateStage()
        {
            var behind = new StackPanel
            {
                Margin = new Thickness(28, 20),
                Spacing = 4,
                IsHitTestVisible = false,
                Children =
                {
                    new TextBlock { Classes = { "gbd-stage-word" }, Text = "FLOWERY" },
                    new TextBlock { Classes = { "gbd-stage-line" }, Text = "★ ★ ★ ★ ★ ★ ★ ★ ★ ★ ★ ★ ★ ★ ★ ★" },
                    new TextBlock { Classes = { "gbd-stage-line" }, Text = "▲ ● ■ ◆ ▲ ● ■ ◆ ▲ ● ■ ◆ ▲ ● ■ ◆" }
                }
            };
            var orbs = new Panel
            {
                IsHitTestVisible = false,
                Children =
                {
                    Orb(0xFF22D3EE, 150, HorizontalAlignment.Right, VerticalAlignment.Top, new Thickness(0, -30, 60, 0)),
                    Orb(0xFFF472B6, 110, HorizontalAlignment.Right, VerticalAlignment.Bottom, new Thickness(0, 0, 200, -30)),
                    _blob
                }
            };

            var backdrops = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            foreach (var backdrop in Enum.GetValues<Backdrop>())
            {
                var swatch = new RadioButton
                {
                    Classes = { "gbd-swatch" },
                    GroupName = "gbd-backdrop-" + _groups,
                    Background = StageBrush(backdrop),
                    IsChecked = backdrop == _backdrop
                };
                ToolTip.SetTip(swatch, backdrop.ToString());
                swatch.IsCheckedChanged += (_, _) =>
                {
                    if (swatch.IsChecked != true)
                        return;
                    _backdrop = backdrop;
                    _stage.Background = StageBrush(backdrop);
                    // The light backdrop needs dark text and symbols.
                    _stage.Classes.Set("paper", backdrop == Backdrop.Paper);
                };
                backdrops.Children.Add(swatch);
            }

            var move = new DaisyToggle { Size = DaisySize.Small, Content = L("GlassDesigner_Move", "Move"), IsChecked = _move?.IsChecked == true };
            _move = move;
            move.IsCheckedChanged += (_, _) =>
            {
                if (move.IsChecked == true)
                    StartMotion();
                else
                    StopMotion();
            };

            var controls = new Border
            {
                Classes = { "gbd-stage-bar" },
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(12),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    Children = { backdrops, move }
                }
            };

            var stage = new Border
            {
                Classes = { "gbd-stage" },
                Margin = new Thickness(24, 0),
                Height = 240,
                ClipToBounds = true,
                Background = StageBrush(_backdrop),
                Child = new Panel { Children = { orbs, behind, _preview, controls } }
            };
            stage.Classes.Set("paper", _backdrop == Backdrop.Paper);
            return stage;
        }

        private static Border Orb(uint color, double size, HorizontalAlignment horizontal, VerticalAlignment vertical, Thickness margin) => new()
        {
            Width = size,
            Height = size,
            CornerRadius = new CornerRadius(size / 2),
            Background = new SolidColorBrush(Color.FromUInt32(color)),
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical,
            Margin = margin
        };

        private static IBrush StageBrush(Backdrop backdrop)
        {
            (uint, uint, uint) colors = backdrop switch
            {
                Backdrop.Sunset => (0xFFF97316, 0xFFDB2777, 0xFF4C1D95),
                Backdrop.Night => (0xFF0B1020, 0xFF1E1B4B, 0xFF312E81),
                Backdrop.Paper => (0xFFF8FAFC, 0xFFE2E8F0, 0xFFCBD5E1),
                _ => (0xFF0EA5E9, 0xFF7C3AED, 0xFFF43F5E)
            };
            return new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromUInt32(colors.Item1), 0),
                    new GradientStop(Color.FromUInt32(colors.Item2), 0.5),
                    new GradientStop(Color.FromUInt32(colors.Item3), 1)
                }
            };
        }

        private void StartMotion()
        {
            StopMotion();
            _motion = new CancellationTokenSource();
            var animation = new Animation
            {
                Duration = TimeSpan.FromSeconds(8),
                IterationCount = IterationCount.Infinite,
                PlaybackDirection = PlaybackDirection.Alternate,
                Easing = new Avalonia.Animation.Easings.SineEaseInOut(),
                Children =
                {
                    new KeyFrame { Cue = new Cue(0), Setters = { new Setter(TranslateTransform.XProperty, 0d), new Setter(TranslateTransform.YProperty, -50d) } },
                    new KeyFrame { Cue = new Cue(0.5), Setters = { new Setter(TranslateTransform.XProperty, 260d), new Setter(TranslateTransform.YProperty, 50d) } },
                    new KeyFrame { Cue = new Cue(1), Setters = { new Setter(TranslateTransform.XProperty, 520d), new Setter(TranslateTransform.YProperty, -30d) } }
                }
            };
            _ = animation.RunAsync(_blob, _motion.Token);
        }

        private void StopMotion()
        {
            _motion?.Cancel();
            _motion?.Dispose();
            _motion = null;
        }

        // The text of the preview button, above the tabs, so it can be changed on every tab.
        private Control CreateTextRow()
        {
            var text = new TextBox { Text = PreviewText, Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
            AutomationProperties.SetName(text, L("GlassDesigner_Text", "Text"));
            // The text box reports a change later; by then it shows the preview text, also when code changed that.
            text.TextChanged += (_, _) => PreviewText = text.Text ?? "";
            _textBox = text;
            return new StackPanel
            {
                Margin = new Thickness(24, 0),
                Spacing = 6,
                Children =
                {
                    new TextBlock { Classes = { "gbd-label" }, Text = L("GlassDesigner_Text", "Text").ToUpper(FloweryLocalization.CurrentCulture) },
                    text
                }
            };
        }

        private Control CreateTabBar()
        {
            var tabs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            foreach (var tab in Enum.GetValues<Tab>())
            {
                var button = new RadioButton
                {
                    Classes = { "gbd-tab" },
                    GroupName = "gbd-tab-" + _groups,
                    Content = L("GlassDesigner_Tab" + tab, tab.ToString()),
                    IsChecked = tab == _tab
                };
                AutomationProperties.SetAutomationId(button, "GlassDesigner_Tab" + tab);
                button.IsCheckedChanged += (_, _) =>
                {
                    if (button.IsChecked == true)
                        ShowTab(tab);
                };
                _tabButtons[tab] = button;
                tabs.Children.Add(button);
            }

            // Reset sits next to the tabs, so it is at hand on every tab, also when the stage is scrolled away.
            var reset = new DaisyButton
            {
                Content = L("GlassDesigner_Reset", "Reset"),
                ButtonStyle = DaisyButtonStyle.Soft,
                Size = DaisySize.Small,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            reset.Click += (_, _) => Reset();
            Grid.SetColumn(reset, 1);

            return new Grid
            {
                Margin = new Thickness(24, 0),
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    new ScrollViewer
                    {
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                        Content = new Border { Classes = { "gbd-tabs" }, HorizontalAlignment = HorizontalAlignment.Left, Child = tabs }
                    },
                    reset
                }
            };
        }

        private void ShowTab(Tab tab)
        {
            _tab = tab;
            var wasLoading = _loading;
            _loading = true;
            try
            {
                _fields.Children.Clear();
                foreach (var field in CreateFields(tab))
                    _fields.Children.Add(field);
            }
            finally
            {
                _loading = wasLoading;
            }
        }

        private IEnumerable<Control> CreateFields(Tab tab) => tab switch
        {
            Tab.Presets => CreatePresetFields(),
            Tab.Button => CreateButtonFields(),
            Tab.Glass => CreateGlassFields(),
            Tab.Fill => CreateFillFields(),
            Tab.Pattern => CreatePatternFields(),
            _ => CreateMotionFields()
        };

        // ---------------------------------------------------------------- Tabs

        private IEnumerable<Control> CreatePresetFields()
        {
            var tiles = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var (name, preset, preview) in DaisyGlassDesignPresets.All)
            {
                var sample = new DaisyButton
                {
                    Content = name,
                    Theme = GlassButtonTheme(),
                    Variant = preview.Variant,
                    ButtonStyle = preview.Style,
                    Shape = preview.Shape,
                    IsHitTestVisible = false,
                    Size = DaisySize.Small,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var look = preset.Clone();
                look.BackgroundRenderer = DaisyGlassBackgroundRenderer.None;
                look.ApplyTo(sample);
                var tile = new Button
                {
                    Classes = { "gbd-tile" },
                    Margin = new Thickness(0, 0, 10, 10),
                    Content = new Border
                    {
                        Classes = { "gbd-tile-stage" },
                        Width = 150,
                        Height = 84,
                        Background = StageBrush(Backdrop.Aurora),
                        Child = new Panel
                        {
                            Children =
                            {
                                Orb(0xFFFACC15, 46, HorizontalAlignment.Left, VerticalAlignment.Top, new Thickness(10, 8, 0, 0)),
                                sample
                            }
                        }
                    }
                };
                ToolTip.SetTip(tile, name);
                tile.Click += (_, _) =>
                {
                    // The preset shows on the button it was made for; the text and size of the preview stay.
                    PreviewVariant = preview.Variant;
                    PreviewButtonStyle = preview.Style;
                    PreviewShape = preview.Shape;
                    Design = preset;
                };
                tiles.Children.Add(tile);
            }
            yield return Field("GlassDesigner_Presets", "Start from a preset", tiles, wide: true);
        }

        private IEnumerable<Control> CreateButtonFields()
        {
            // The preview options change the preview button only; they are not part of the design. Its text sits above
            // the tabs.
            yield return ChipField("GlassDesigner_Variant", "Variant", Enum.GetValues<DaisyButtonVariant>(), PreviewVariant, v => PreviewVariant = v, wide: true);
            yield return ChipField("GlassDesigner_Style", "Style", Enum.GetValues<DaisyButtonStyle>(), PreviewButtonStyle, v => PreviewButtonStyle = v);
            yield return ChipField("GlassDesigner_Size", "Size", Enum.GetValues<DaisySize>(), PreviewSize, v => PreviewSize = v);
            yield return ChipField("GlassDesigner_Shape", "Shape", Enum.GetValues<DaisyButtonShape>(), PreviewShape, v => PreviewShape = v);
            yield return Slider("GlassDesigner_ColorOpacity", "Variant color", 0, 1, 0.05, d => d.ColorOpacity, (d, v) => d.ColorOpacity = v);
            yield return Slider("GlassDesigner_TextShadow", "Text shadow", 0, 1, 0.05, d => d.TextShadowOpacity, (d, v) => d.TextShadowOpacity = v);
            yield return Toggle("GlassDesigner_TextContrast", "Keep text readable", d => d.TextContrast, (d, v) => d.TextContrast = v);
        }

        private IEnumerable<Control> CreateGlassFields()
        {
            yield return Chips("GlassDesigner_BlurMode", "Blur mode", Enum.GetValues<GlassBlurMode>(), d => d.BlurMode, (d, v) => d.BlurMode = v,
                hint: DaisyGlassLiveRenderer.Current is null
                    ? L("GlassDesigner_SkiaHint", "SkiaSharp needs Flowery.NET.Skia; without it the glass uses BitmapCapture.")
                    : null);
            yield return Toggle("GlassDesigner_BackdropBlur", "Blur the backdrop", d => d.EnableBackdropBlur, (d, v) => d.EnableBackdropBlur = v);
            yield return Slider("GlassDesigner_Blur", "Blur", 0, 80, 1, d => d.Blur, (d, v) => d.Blur = v);
            yield return Slider("GlassDesigner_Saturation", "Saturation", 0, 2, 0.05, d => d.Saturation, (d, v) => d.Saturation = v);
            yield return ColorField("GlassDesigner_Tint", "Tint", d => d.Tint, (d, v) => d.Tint = v);
            yield return Slider("GlassDesigner_TintOpacity", "Tint opacity", 0, 1, 0.01, d => d.TintOpacity, (d, v) => d.TintOpacity = v);
            yield return Slider("GlassDesigner_ReflectOpacity", "Reflection", 0, 1, 0.01, d => d.ReflectOpacity, (d, v) => d.ReflectOpacity = v);
            yield return Slider("GlassDesigner_ReflectAngle", "Reflection angle", 0, 360, 5, d => d.ReflectDegree, (d, v) => d.ReflectDegree = v, "0°");
            yield return Slider("GlassDesigner_SimulatedOpacity", "Simulated opacity", 0, 1, 0.01, d => d.Opacity, (d, v) => d.Opacity = v);
            var liquid = L("GlassDesigner_LiquidHint", "Liquid optics show in SkiaSharp mode.");
            yield return Slider("GlassDesigner_Depth", "Depth", 0, 1, 0.01, d => d.Depth, (d, v) => d.Depth = v, hint: liquid);
            yield return Slider("GlassDesigner_Curvature", "Curvature", 0, 1, 0.01, d => d.Curvature, (d, v) => d.Curvature = v);
            yield return Slider("GlassDesigner_Bend", "Bend", 0, 1, 0.01, d => d.Bend, (d, v) => d.Bend = v);
            yield return Slider("GlassDesigner_Dispersion", "Dispersion", 0, 1, 0.01, d => d.Dispersion, (d, v) => d.Dispersion = v);
        }

        private IEnumerable<Control> CreateFillFields()
        {
            // Keep is offered only for a fill read from a button that the design cannot describe, also after another
            // kind was picked, so the fill of the button can be had back.
            var kinds = Enum.GetValues<DaisyGlassFillKind>()
                .Where(kind => kind != DaisyGlassFillKind.Keep || _design.KeptFill is not null || _design.FillKind == kind).ToArray();
            yield return Chips("GlassDesigner_Fill", "Fill", kinds, d => d.FillKind, (d, v) =>
            {
                d.FillKind = v;
                if (v == DaisyGlassFillKind.Preset && d.FillPreset == DaisyGlassGradient.None)
                    d.FillPreset = DaisyGlassGradient.Aurora;
            }, wide: true, rebuildTab: true);

            if (_design.FillKind != DaisyGlassFillKind.Keep)
                yield return FillPalettes();

            switch (_design.FillKind)
            {
                case DaisyGlassFillKind.Preset:
                    yield return GradientPresets();
                    break;
                case DaisyGlassFillKind.Linear or DaisyGlassFillKind.Radial or DaisyGlassFillKind.Conic:
                    yield return FillStops();
                    if (_design.FillKind != DaisyGlassFillKind.Radial)
                        yield return Slider("GlassDesigner_FillAngle", "Angle", 0, 360, 5, d => d.FillAngle, (d, v) => d.FillAngle = v, "0°");
                    break;
            }

            if (_design.FillKind != DaisyGlassFillKind.None)
                yield return Slider("GlassDesigner_FillOpacity", "Fill opacity", 0, 1, 0.01, d => d.FillOpacity, (d, v) => d.FillOpacity = v);
        }

        // Color suggestions for a gradient fill. A palette sets the colors, which stay editable below.
        private static readonly (string Name, uint[] Colors)[] Palettes =
        [
            ("Sunrise", [0xFFFF7E5F, 0xFFFEB47B, 0xFFFFE29F]),
            ("Lagoon", [0xFF0EA5E9, 0xFF6366F1, 0xFF22D3EE]),
            ("Forest", [0xFF134E5E, 0xFF3A8D6F, 0xFF71B280]),
            ("Berry", [0xFF8E2DE2, 0xFFD946EF, 0xFFFF6CAB]),
            ("Mint", [0xFFA8FF78, 0xFF78FFD6]),
            ("Candy", [0xFFFF9A9E, 0xFFFAD0C4, 0xFFA18CD1]),
            ("Ember", [0xFFF97316, 0xFFDC2626, 0xFF7F1D1D]),
            ("Twilight", [0xFF0F2027, 0xFF203A43, 0xFF2C5364]),
            ("Neon", [0xFF00F5A0, 0xFF00D9F5, 0xFF7B61FF]),
            ("Pearl", [0xFFFFFFFF, 0xFFE2E8F0, 0xFFCBD5E1])
        ];

        private Control FillPalettes()
        {
            var tiles = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var (name, colors) in Palettes)
            {
                var brush = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative)
                };
                for (var i = 0; i < colors.Length; i++)
                    brush.GradientStops.Add(new GradientStop(Color.FromUInt32(colors[i]), (double)i / (colors.Length - 1)));
                var tile = new Button
                {
                    Classes = { "gbd-tile" },
                    Margin = new Thickness(0, 0, 6, 6),
                    Content = new Border { CornerRadius = new CornerRadius(12), Width = 76, Height = 34, Background = brush }
                };
                ToolTip.SetTip(tile, name);
                AutomationProperties.SetName(tile, name);
                tile.Click += (_, _) => Change(d =>
                {
                    d.FillStops.Clear();
                    foreach (var color in colors)
                        d.FillStops.Add(new DaisyGlassFillStop { Color = Color.FromUInt32(color) });
                    Spread(d.FillStops);
                    if (d.FillKind is not (DaisyGlassFillKind.Linear or DaisyGlassFillKind.Radial or DaisyGlassFillKind.Conic))
                        d.FillKind = DaisyGlassFillKind.Linear;
                }, rebuildTab: true);
                tiles.Children.Add(tile);
            }
            return Field("GlassDesigner_FillPalettes", "Palettes", tiles, wide: true);
        }

        private Control GradientPresets()
        {
            var tiles = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var gradient in Enum.GetValues<DaisyGlassGradient>().Where(g => g != DaisyGlassGradient.None))
            {
                var tile = new RadioButton
                {
                    Classes = { "gbd-gradient" },
                    GroupName = "gbd-gradient-" + _groups,
                    Background = DaisyGlassGradients.CreateBrush(gradient),
                    Content = gradient.ToString(),
                    IsChecked = _design.FillPreset == gradient,
                    Margin = new Thickness(0, 0, 8, 8)
                };
                tile.IsCheckedChanged += (_, _) =>
                {
                    if (tile.IsChecked == true)
                        Change(d => d.FillPreset = gradient);
                };
                tiles.Children.Add(tile);
            }
            return Field("GlassDesigner_FillPreset", "Preset", tiles, wide: true);
        }

        private Control FillStops()
        {
            var row = new WrapPanel { Orientation = Orientation.Horizontal };
            for (var i = 0; i < _design.FillStops.Count; i++)
            {
                var index = i;
                var swatch = Swatch(_design.FillStops[i].Color);
                swatch.Margin = new Thickness(0, 0, 4, 8);
                swatch.Click += async (_, _) =>
                {
                    if (await PickColorAsync(_design.FillStops[index].Color) is { } color)
                        Change(d => d.FillStops[index].Color = color, rebuildTab: true);
                };
                row.Children.Add(swatch);

                if (_design.FillStops.Count > 2)
                {
                    var remove = new Button { Classes = { "gbd-icon-button" }, Content = "×", Margin = new Thickness(0, 0, 10, 8) };
                    ToolTip.SetTip(remove, L("GlassDesigner_RemoveColor", "Remove color"));
                    remove.Click += (_, _) => Change(d =>
                    {
                        d.FillStops.RemoveAt(index);
                        Spread(d.FillStops);
                    }, rebuildTab: true);
                    row.Children.Add(remove);
                }
            }

            if (_design.FillStops.Count < MaxFillStops)
            {
                var add = new Button { Classes = { "gbd-icon-button" }, Content = "+", Margin = new Thickness(4, 0, 0, 8) };
                ToolTip.SetTip(add, L("GlassDesigner_AddColor", "Add color"));
                add.Click += (_, _) => Change(d =>
                {
                    d.FillStops.Add(new DaisyGlassFillStop { Color = d.FillStops.Count > 0 ? d.FillStops[^1].Color : Colors.White });
                    Spread(d.FillStops);
                }, rebuildTab: true);
                row.Children.Add(add);
            }

            return Field("GlassDesigner_FillColors", "Colors", row, wide: true);
        }

        private static void Spread(List<DaisyGlassFillStop> stops)
        {
            for (var i = 0; i < stops.Count; i++)
                stops[i].Offset = stops.Count == 1 ? 0 : Math.Round((double)i / (stops.Count - 1), 3);
        }

        private IEnumerable<Control> CreatePatternFields()
        {
            var tiles = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var pattern in Enum.GetValues<DaisyCardPattern>())
            {
                var tile = new RadioButton
                {
                    Classes = { "gbd-pattern" },
                    GroupName = "gbd-pattern-" + _groups,
                    IsChecked = _design.Pattern == pattern,
                    Margin = new Thickness(0, 0, 8, 8),
                    Content = new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            new DaisyGlass
                            {
                                Width = 72,
                                Height = 44,
                                Padding = new Thickness(0),
                                CornerRadius = new CornerRadius(10),
                                BlurMode = GlassBlurMode.Simulated,
                                GlassTint = Color.FromUInt32(0xFF6366F1),
                                GlassTintOpacity = 0.9,
                                GlassPattern = pattern,
                                GlassPatternOpacity = 0.6,
                                IsHitTestVisible = false
                            },
                            new TextBlock { Classes = { "gbd-tile-label" }, Text = pattern.ToString() }
                        }
                    }
                };
                tile.IsCheckedChanged += (_, _) =>
                {
                    if (tile.IsChecked == true)
                        Change(d => d.Pattern = pattern);
                };
                tiles.Children.Add(tile);
            }
            yield return Field("GlassDesigner_Pattern", "Pattern", tiles, wide: true);
            yield return ColorField("GlassDesigner_PatternColor", "Pattern color", d => d.PatternColor, (d, v) => d.PatternColor = v);
            yield return Slider("GlassDesigner_PatternOpacity", "Pattern opacity", 0, 1, 0.01, d => d.PatternOpacity, (d, v) => d.PatternOpacity = v);
        }

        private IEnumerable<Control> CreateMotionFields()
        {
            var renderers = Enum.GetValues<DaisyGlassBackgroundRenderer>()
                .Where(renderer => renderer != DaisyGlassBackgroundRenderer.Keep || _design.KeptBackground || _design.BackgroundRenderer == renderer)
                .ToArray();
            var shaderAvailable = DaisyGlassDesign.ShaderBackgroundAvailable;
            yield return Chips("GlassDesigner_Renderer", "Animated background", renderers, d => d.BackgroundRenderer, (d, v) => d.BackgroundRenderer = v,
                wide: true, rebuildTab: true,
                isEnabled: renderer => renderer != DaisyGlassBackgroundRenderer.Shader || shaderAvailable,
                hint: shaderAvailable ? null : L("GlassDesigner_ShaderHint", "Shader needs Flowery.NET.Skia."));

            if (_design.BackgroundRenderer == DaisyGlassBackgroundRenderer.None)
                yield break;
            if (_design.BackgroundRenderer == DaisyGlassBackgroundRenderer.Keep)
            {
                yield return Slider("GlassDesigner_BackgroundOpacity", "Background opacity", 0, 1, 0.01, d => d.BackgroundOpacity, (d, v) => d.BackgroundOpacity = v);
                yield break;
            }

            yield return Chips("GlassDesigner_Effect", "Effect", Enum.GetValues<DaisyGlassAnimation>(), d => d.BackgroundEffect, (d, v) => d.BackgroundEffect = v);
            yield return Slider("GlassDesigner_Speed", "Speed", 0, 3, 0.05, d => d.BackgroundSpeed, (d, v) => d.BackgroundSpeed = v);
            yield return Chips("GlassDesigner_FrameRate", "Frame rate", [15d, 24d, 30d, 60d, 0d], d => d.BackgroundFrameRate, (d, v) => d.BackgroundFrameRate = v,
                label: rate => rate == 0 ? L("GlassDesigner_EveryFrame", "Every frame") : rate.ToString(CultureInfo.CurrentCulture));
            yield return Slider("GlassDesigner_BackgroundOpacity", "Background opacity", 0, 1, 0.01, d => d.BackgroundOpacity, (d, v) => d.BackgroundOpacity = v);
        }

        // ---------------------------------------------------------------- Field builders

        private static Border Field(string key, string fallback, Control editor, Control? value = null, string? hint = null, bool wide = false)
        {
            var label = L(key, fallback);
            AutomationProperties.SetName(editor, label);
            var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            header.Children.Add(new TextBlock { Classes = { "gbd-label" }, Text = label.ToUpper(FloweryLocalization.CurrentCulture), VerticalAlignment = VerticalAlignment.Center });
            if (value is not null)
            {
                Grid.SetColumn(value, 1);
                header.Children.Add(value);
            }

            var content = new StackPanel { Spacing = 10, Children = { header, editor } };
            if (hint is not null)
                content.Children.Add(new TextBlock { Classes = { "gbd-hint" }, Text = hint, TextWrapping = TextWrapping.Wrap });

            var field = new Border { Classes = { "gbd-field" }, Child = content };
            AutomationProperties.SetAutomationId(field, key);
            if (wide)
                field.Classes.Add("gbd-wide");
            return field;
        }

        private Border Chips<T>(string key, string fallback, IReadOnlyList<T> values, Func<DaisyGlassDesign, T> get, Action<DaisyGlassDesign, T> set,
            bool wide = false, bool rebuildTab = false, Func<T, bool>? isEnabled = null, string? hint = null, Func<T, string>? label = null) =>
            ChipField(key, fallback, values, get(_design), value => Change(d => set(d, value), rebuildTab), wide, isEnabled, hint, label);

        private Border ChipField<T>(string key, string fallback, IReadOnlyList<T> values, T current, Action<T> pick,
            bool wide = false, Func<T, bool>? isEnabled = null, string? hint = null, Func<T, string>? label = null)
        {
            var group = "gbd-chips-" + key + "-" + _groups;
            var chips = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var value in values)
            {
                var chip = new RadioButton
                {
                    Classes = { "gbd-chip" },
                    GroupName = group,
                    Content = label?.Invoke(value) ?? value?.ToString(),
                    IsChecked = EqualityComparer<T>.Default.Equals(value, current),
                    IsEnabled = isEnabled?.Invoke(value) ?? true,
                    Margin = new Thickness(0, 0, 6, 6)
                };
                chip.IsCheckedChanged += (_, _) =>
                {
                    if (chip.IsChecked == true)
                        pick(value);
                };
                chips.Children.Add(chip);
            }
            return Field(key, fallback, chips, hint: hint, wide: wide);
        }

        private Border Slider(string key, string fallback, double minimum, double maximum, double step, Func<DaisyGlassDesign, double> get,
            Action<DaisyGlassDesign, double> set, string format = "0.##", string? hint = null)
        {
            var value = new TextBlock { Classes = { "gbd-value" } };
            var slider = new DaisyRange
            {
                Minimum = minimum,
                Maximum = maximum,
                SmallChange = step,
                LargeChange = step * 10,
                TickFrequency = step,
                IsSnapToTickEnabled = true,
                Size = DaisySize.ExtraSmall,
                Variant = DaisyRangeVariant.Primary,
                Value = Math.Clamp(get(_design), minimum, maximum)
            };
            value.Text = get(_design).ToString(format, CultureInfo.CurrentCulture);
            slider.PropertyChanged += (_, e) =>
            {
                if (e.Property != RangeBase.ValueProperty)
                    return;
                value.Text = slider.Value.ToString(format, CultureInfo.CurrentCulture);
                Change(d => set(d, slider.Value));
            };
            return Field(key, fallback, slider, new Border { Classes = { "gbd-pill" }, Child = value }, hint);
        }

        private Border Toggle(string key, string fallback, Func<DaisyGlassDesign, bool> get, Action<DaisyGlassDesign, bool> set)
        {
            var toggle = new DaisyToggle { Size = DaisySize.Medium, IsChecked = get(_design) };
            toggle.IsCheckedChanged += (_, _) => Change(d => set(d, toggle.IsChecked == true));
            return Field(key, fallback, toggle);
        }

        private Border ColorField(string key, string fallback, Func<DaisyGlassDesign, Color> get, Action<DaisyGlassDesign, Color> set)
        {
            var swatch = Swatch(get(_design));
            var hex = new TextBlock { Classes = { "gbd-value" }, Text = DaisyGlassDesign.FormatColor(get(_design)) };
            swatch.Click += async (_, _) =>
            {
                if (await PickColorAsync(get(_design)) is not { } color)
                    return;
                ((Border)swatch.Content!).Background = new SolidColorBrush(color);
                hex.Text = DaisyGlassDesign.FormatColor(color);
                Change(d => set(d, color));
            };
            return Field(key, fallback, swatch, new Border { Classes = { "gbd-pill" }, Child = hex });
        }

        private static Button Swatch(Color color) => new()
        {
            Classes = { "gbd-color" },
            Content = new Border { Background = new SolidColorBrush(color), CornerRadius = new CornerRadius(7) }
        };

        private async Task<Color?> PickColorAsync(Color color)
        {
            try
            {
                return await DaisyColorPickerDialog.ShowOverlayAsync(this, color);
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        // ---------------------------------------------------------------- Code drawer

        private StackPanel CreateImportPanel()
        {
            var load = new DaisyButton { Content = L("GlassDesigner_Load", "Load"), Size = DaisySize.Small, Variant = DaisyButtonVariant.Primary };
            load.Click += (_, _) =>
            {
                try
                {
                    ImportJson(_importBox.Text ?? "");
                    _importError.IsVisible = false;
                    _importBox.Text = "";
                }
                catch (JsonException ex)
                {
                    _importError.Text = string.Format(CultureInfo.CurrentCulture, L("GlassDesigner_ImportError", "This is not a design: {0}"), ex.Message);
                    _importError.IsVisible = true;
                }
            };
            _importBox.PlaceholderText = L("GlassDesigner_ImportWatermark", "Paste the JSON of a design");
            return new StackPanel
            {
                Spacing = 8,
                IsVisible = false,
                Children = { _importBox, _importError, load }
            };
        }

        private Border CreateCodeDrawer()
        {
            var kinds = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
            foreach (var kind in Enum.GetValues<CodeKind>())
            {
                var button = new RadioButton
                {
                    Classes = { "gbd-code-tab" },
                    GroupName = "gbd-code-" + _groups,
                    Content = kind switch { CodeKind.CSharp => "C#", CodeKind.Json => "JSON", _ => "XAML" },
                    IsChecked = kind == _codeKind
                };
                button.IsCheckedChanged += (_, _) =>
                {
                    if (button.IsChecked != true)
                        return;
                    _codeKind = kind;
                    UpdateCode();
                };
                kinds.Children.Add(button);
            }

            var import = new ToggleButton { Classes = { "gbd-code-toggle" }, Content = L("GlassDesigner_Import", "Import") };
            import.IsCheckedChanged += (_, _) => _importPanel.IsVisible = import.IsChecked == true;
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { import, _copyButton } };
            Grid.SetColumn(actions, 1);

            return new Border
            {
                Classes = { "gbd-code" },
                Margin = new Thickness(24, 0, 24, 24),
                IsVisible = false,
                Child = new StackPanel
                {
                    Spacing = 12,
                    Children =
                    {
                        new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { kinds, actions } },
                        _importPanel,
                        new ScrollViewer
                        {
                            MaxHeight = 280,
                            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                            Content = _codeText
                        }
                    }
                }
            };
        }

        private void UpdateCode()
        {
            if (!_codeDrawer.IsVisible)
                return;

            var code = _codeKind switch
            {
                CodeKind.CSharp => _design.ToCSharp(),
                CodeKind.Json => _design.ToJson(),
                _ => _design.ToXaml()
            };
            _codeText.Text = code;
            _copyButton.CopyText = code;
        }
    }

    /// <summary>
    /// Lays out the fields of <see cref="DaisyGlassDesigner"/> in as many equal columns as fit, each at least
    /// <see cref="MinColumnWidth"/> wide; the fields of a row share its height. A field with the class <c>gbd-wide</c>
    /// takes a row of its own.
    /// </summary>
    internal sealed class DaisyGlassDesignerFieldPanel : Panel
    {
        public const double MinColumnWidth = 240;
        public const double Gap = 12;

        protected override Size MeasureOverride(Size availableSize) => Layout(availableSize.Width, arrange: false);

        protected override Size ArrangeOverride(Size finalSize)
        {
            Layout(finalSize.Width, arrange: true);
            return finalSize;
        }

        private Size Layout(double width, bool arrange)
        {
            if (double.IsInfinity(width) || width <= 0)
                width = MinColumnWidth;
            var columns = Math.Max(1, (int)((width + Gap) / (MinColumnWidth + Gap)));
            var cell = (width - (columns - 1) * Gap) / columns;

            var rows = new List<List<Control>>();
            List<Control>? row = null;
            foreach (var child in Children)
            {
                var wide = child.Classes.Contains("gbd-wide");
                if (row is null || wide || row.Count == columns || row[0].Classes.Contains("gbd-wide"))
                    rows.Add(row = new List<Control>());
                row.Add(child);
                if (!arrange)
                    child.Measure(new Size(wide ? width : cell, double.PositiveInfinity));
            }

            var y = 0d;
            foreach (var fields in rows)
            {
                var height = fields.Max(field => field.DesiredSize.Height);
                if (arrange)
                {
                    for (var i = 0; i < fields.Count; i++)
                    {
                        var wide = fields[i].Classes.Contains("gbd-wide");
                        fields[i].Arrange(new Rect(wide ? 0 : i * (cell + Gap), y, wide ? width : cell, height));
                    }
                }
                y += height + Gap;
            }
            return new Size(width, Math.Max(0, y - Gap));
        }
    }

    /// <summary>
    /// The presets of <see cref="DaisyGlassDesigner"/>.
    /// </summary>
    internal static class DaisyGlassDesignPresets
    {
        /// <summary>
        /// The button a preset was made for, which the preview takes when the preset is picked. It is not part of the
        /// design.
        /// </summary>
        public sealed record PresetPreview(
            DaisyButtonVariant Variant = DaisyButtonVariant.Default,
            DaisyButtonStyle Style = DaisyButtonStyle.Default,
            DaisyButtonShape Shape = DaisyButtonShape.Default);

        private static readonly PresetPreview Plain = new();

        public static IReadOnlyList<(string Name, DaisyGlassDesign Design, PresetPreview Preview)> All { get; } =
        [
            ("Frost", new DaisyGlassDesign
            {
                TintOpacity = 0.2, ReflectOpacity = 0.3, FillKind = DaisyGlassFillKind.Preset, FillPreset = DaisyGlassGradient.Frost,
                FillOpacity = 0.35
            }, Plain),
            ("Midnight", new DaisyGlassDesign
            {
                Blur = 40, Tint = Color.FromUInt32(0xFF0F172A), TintOpacity = 0.45,
                ColorOpacity = 0.2, ReflectOpacity = 0.2
            }, new(DaisyButtonVariant.Neutral)),
            ("Aurora", new DaisyGlassDesign
            {
                FillKind = DaisyGlassFillKind.Preset, FillPreset = DaisyGlassGradient.Aurora, FillOpacity = 0.55
            }, Plain),
            ("Sunset", new DaisyGlassDesign
            {
                FillKind = DaisyGlassFillKind.Preset, FillPreset = DaisyGlassGradient.Sunset, FillOpacity = 0.6
            }, new(Shape: DaisyButtonShape.Wide)),
            ("Holo", new DaisyGlassDesign
            {
                FillKind = DaisyGlassFillKind.Preset, FillPreset = DaisyGlassGradient.Holo, FillOpacity = 0.5,
                Pattern = DaisyCardPattern.Scales, PatternOpacity = 0.25
            }, Plain),
            ("Neon", new DaisyGlassDesign
            {
                Tint = Color.FromUInt32(0xFF22D3EE),
                TintOpacity = 0.1, Curvature = 0.9, Dispersion = 0.5
            }, new(DaisyButtonVariant.Accent, DaisyButtonStyle.Outline)),
            ("Ocean", new DaisyGlassDesign
            {
                FillKind = DaisyGlassFillKind.Preset, FillPreset = DaisyGlassGradient.Ocean, FillOpacity = 0.5
            }, new(DaisyButtonVariant.Info)),
            ("Candy", new DaisyGlassDesign
            {
                FillKind = DaisyGlassFillKind.Linear, FillAngle = 0, FillOpacity = 0.65, Pattern = DaisyCardPattern.Dots,
                PatternOpacity = 0.25,
                FillStops =
                [
                    new() { Color = Color.FromUInt32(0xFFF9A8D4), Offset = 0 },
                    new() { Color = Color.FromUInt32(0xFFFDBA74), Offset = 1 }
                ]
            }, Plain),
            ("Liquid Gold", new DaisyGlassDesign
            {
                Tint = Color.FromUInt32(0xFFF59E0B), TintOpacity = 0.1, BackgroundRenderer = DaisyGlassBackgroundRenderer.Gradient,
                BackgroundEffect = DaisyGlassAnimation.LiquidGold, BackgroundOpacity = 0.7
            }, Plain),
            ("Plasma", new DaisyGlassDesign
            {
                ColorOpacity = 0.2, BackgroundRenderer = DaisyGlassBackgroundRenderer.Gradient,
                BackgroundEffect = DaisyGlassAnimation.Plasma, BackgroundSpeed = 0.6
            }, new(DaisyButtonVariant.Secondary))
        ];
    }
}
