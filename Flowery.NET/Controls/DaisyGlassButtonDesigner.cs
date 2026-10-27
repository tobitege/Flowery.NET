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
using Avalonia.Styling;
using Avalonia.VisualTree;
using Flowery.Controls.ColorPicker;
using Flowery.Enums;
using Flowery.Helpers;
using Flowery.Localization;

namespace Flowery.Controls
{
    /// <summary>
    /// A visual editor for <see cref="DaisyGlassButton"/>: a live preview over a colorful backdrop, the settings in
    /// tabs (presets, button, glass, fill, pattern, motion), and the result as XAML, C# or JSON.
    /// Embed it in a page, or open it over the app with <see cref="ShowOverlayAsync"/> or <see cref="EditAsync"/>.
    /// </summary>
    public class DaisyGlassButtonDesigner : UserControl
    {
        private enum Tab { Presets, Button, Glass, Fill, Pattern, Motion }

        private enum CodeKind { Xaml, CSharp, Json }

        private enum Backdrop { Aurora, Sunset, Night, Paper }

        private const int MaxFillStops = 5;

        private DaisyGlassButtonDesign _design = new();
        private bool _loading;
        private Tab _tab = Tab.Presets;
        private CodeKind _codeKind = CodeKind.Xaml;
        private Backdrop _backdrop = Backdrop.Aurora;

        private bool _codeOpen;

        // The preview and the disc survive a rebuild for another language; the other parts are built again.
        private readonly DaisyGlassButton _preview;
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

        // Whether the Button tab offers the text: EditAsync leaves the content of a button that is no text alone.
        internal bool EditsText { get; set; } = true;

        /// <summary>
        /// Occurs when the design changes through the editor, a preset, an import or <see cref="Design"/>.
        /// </summary>
        public event EventHandler? DesignChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="DaisyGlassButtonDesigner"/> class.
        /// </summary>
        public DaisyGlassButtonDesigner()
        {
            _preview = new DaisyGlassButton
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
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
        public DaisyGlassButtonDesign Design
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
        public DaisyGlassButton PreviewButton => _preview;

        /// <summary>
        /// Gets the current design as a <c>DaisyGlassButton</c> element.
        /// </summary>
        public string Xaml => _design.ToXaml();

        /// <summary>
        /// Gets the current design as a C# object initializer.
        /// </summary>
        public string CSharp => _design.ToCSharp();

        /// <summary>
        /// Gets the current design as JSON, readable by <see cref="ImportJson"/>.
        /// </summary>
        public string Json => _design.ToJson();

        /// <summary>
        /// Loads the look and text of <paramref name="button"/> into the editor.
        /// </summary>
        public void LoadFrom(DaisyGlassButton button) => Design = DaisyGlassButtonDesign.FromButton(button);

        /// <summary>
        /// Gives <paramref name="button"/> the look of the current design; its content stays.
        /// </summary>
        public void ApplyTo(DaisyGlassButton button) => _design.ApplyTo(button);

        /// <summary>
        /// Loads a design from JSON written by <see cref="Json"/>.
        /// </summary>
        /// <exception cref="JsonException">The text is not a valid design.</exception>
        public void ImportJson(string json) => Design = DaisyGlassButtonDesign.FromJson(json);

        /// <summary>
        /// Returns to the default look, keeping the text.
        /// </summary>
        public void Reset() => Design = new DaisyGlassButtonDesign { Content = _design.Content };

        /// <summary>
        /// Opens a designer over the app and returns the design when the user applies it, or <c>null</c> when the user
        /// cancels. It works on every platform, since it uses the overlay layer of the window or view.
        /// </summary>
        /// <param name="target">Any visual in the window or view to open the designer over.</param>
        /// <param name="initial">The design to start with, or <c>null</c> for the default look.</param>
        /// <exception cref="InvalidOperationException"><paramref name="target"/> has no overlay layer.</exception>
        public static Task<DaisyGlassButtonDesign?> ShowOverlayAsync(Visual target, DaisyGlassButtonDesign? initial = null) =>
            ShowOverlayAsync(target, initial, editsText: true);

        private static Task<DaisyGlassButtonDesign?> ShowOverlayAsync(Visual target, DaisyGlassButtonDesign? initial, bool editsText)
        {
            ArgumentNullException.ThrowIfNull(target);
            var overlay = OverlayLayer.GetOverlayLayer(target)
                          ?? throw new InvalidOperationException("No overlay layer found for the target visual.");

            var result = new TaskCompletionSource<DaisyGlassButtonDesign?>();
            var designer = new DaisyGlassButtonDesigner { EditsText = editsText };
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

            void Close(DaisyGlassButtonDesign? design)
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
        /// Opens a designer over the app with the look of <paramref name="button"/> and applies the result to it. A text
        /// content can be edited too; other content, or none such as on an icon button, stays and the designer offers no
        /// text. Only what the user changed is written to the button, so its styles and the global size keep applying to
        /// the rest. A fill or animated background the design cannot describe stays as it is (see
        /// <see cref="DaisyGlassFillKind.Keep"/>).
        /// </summary>
        /// <returns><c>true</c> when the user applied a design; <c>false</c> when the user cancelled.</returns>
        public static async Task<bool> EditAsync(DaisyGlassButton button)
        {
            ArgumentNullException.ThrowIfNull(button);
            var editsText = button.Content is string;
            var original = DaisyGlassButtonDesign.FromButton(button);
            if (await ShowOverlayAsync(button, original, editsText) is not { } design)
                return false;

            design.ApplyTo(button, includeContent: editsText, reuseBackground: false, original);
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

        private void Load(DaisyGlassButtonDesign design)
        {
            _loading = true;
            try
            {
                _design = design;
                _design.ApplyTo(_preview, includeContent: true, reuseBackground: true);
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
        // preview shows the kept fill as it was read from the button, and no animated background.
        private void ShowKept()
        {
            if (_design.FillKind == DaisyGlassFillKind.Keep)
            {
                _preview.GlassFill = _design.KeptFill;
                _preview.GlassGradient = DaisyGlassGradient.None;
            }
            if (_design.BackgroundRenderer == DaisyGlassBackgroundRenderer.Keep)
                _preview.GlassBackground = null;
        }

        private void Change(Action<DaisyGlassButtonDesign> edit, bool rebuildTab = false)
        {
            if (_loading)
                return;

            edit(_design);
            _design.ApplyTo(_preview, includeContent: true, reuseBackground: true);
            ShowKept();
            if (rebuildTab)
                ShowTab(_tab);
            UpdateCode();
            DesignChanged?.Invoke(this, EventArgs.Empty);
        }

        // ---------------------------------------------------------------- Header, stage and tabs

        private Control CreateHeader()
        {
            var reset = new DaisyButton
            {
                Content = L("GlassDesigner_Reset", "Reset"),
                ButtonStyle = DaisyButtonStyle.Soft,
                Size = DaisySize.Small
            };
            reset.Click += (_, _) => Reset();

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
                    new TextBlock { Classes = { "gbd-title" }, Text = L("GlassDesigner_Title", "Glass Button Designer") },
                    new TextBlock
                    {
                        Classes = { "gbd-subtitle" },
                        Text = L("GlassDesigner_Subtitle", "Shape a glass button live, then take the code with you."),
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };
            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { reset, code }
            };
            Grid.SetColumn(actions, 1);
            return new Grid
            {
                Margin = new Thickness(24, 24, 24, 0),
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children = { titles, actions }
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

            return new Border
            {
                Classes = { "gbd-stage" },
                Margin = new Thickness(24, 0),
                Height = 240,
                ClipToBounds = true,
                Background = StageBrush(_backdrop),
                Child = new Panel { Children = { orbs, behind, _preview, controls } }
            };
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

            return new ScrollViewer
            {
                Margin = new Thickness(24, 0),
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = new Border { Classes = { "gbd-tabs" }, HorizontalAlignment = HorizontalAlignment.Left, Child = tabs }
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
            foreach (var (name, preset) in DaisyGlassButtonDesignPresets.All)
            {
                var sample = new DaisyGlassButton
                {
                    Content = name,
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
                    var design = preset.Clone();
                    design.Content = _design.Content;
                    Design = design;
                };
                tiles.Children.Add(tile);
            }
            yield return Field("GlassDesigner_Presets", "Start from a preset", tiles, wide: true);
        }

        private IEnumerable<Control> CreateButtonFields()
        {
            if (EditsText)
            {
                var text = new TextBox { Text = _design.Content };
                text.TextChanged += (_, _) => Change(d => d.Content = text.Text ?? "");
                yield return Field("GlassDesigner_Text", "Text", text);
            }
            yield return Chips("GlassDesigner_Variant", "Variant", Enum.GetValues<DaisyButtonVariant>(), d => d.Variant, (d, v) => d.Variant = v, wide: true);
            yield return Chips("GlassDesigner_Style", "Style", Enum.GetValues<DaisyButtonStyle>(), d => d.ButtonStyle, (d, v) => d.ButtonStyle = v);
            yield return Chips("GlassDesigner_Size", "Size", Enum.GetValues<DaisySize>(), d => d.Size, (d, v) => d.Size = v);
            yield return Chips("GlassDesigner_Shape", "Shape", Enum.GetValues<DaisyButtonShape>(), d => d.Shape, (d, v) => d.Shape = v);
            yield return Slider("GlassDesigner_ColorOpacity", "Variant color", 0, 1, 0.05, d => d.GlassColorOpacity, (d, v) => d.GlassColorOpacity = v);
            yield return Slider("GlassDesigner_TextShadow", "Text shadow", 0, 1, 0.05, d => d.GlassTextShadowOpacity, (d, v) => d.GlassTextShadowOpacity = v);
            yield return Toggle("GlassDesigner_TextContrast", "Keep text readable", d => d.GlassTextContrast, (d, v) => d.GlassTextContrast = v);
        }

        private IEnumerable<Control> CreateGlassFields()
        {
            yield return Chips("GlassDesigner_BlurMode", "Blur mode", Enum.GetValues<GlassBlurMode>(), d => d.BlurMode, (d, v) => d.BlurMode = v,
                hint: DaisyGlassLiveRenderer.Current is null
                    ? L("GlassDesigner_SkiaHint", "SkiaSharp needs Flowery.NET.Skia; without it the glass uses BitmapCapture.")
                    : null);
            yield return Toggle("GlassDesigner_BackdropBlur", "Blur the backdrop", d => d.EnableBackdropBlur, (d, v) => d.EnableBackdropBlur = v);
            yield return Slider("GlassDesigner_Blur", "Blur", 0, 80, 1, d => d.GlassBlur, (d, v) => d.GlassBlur = v);
            yield return Slider("GlassDesigner_Saturation", "Saturation", 0, 2, 0.05, d => d.GlassSaturation, (d, v) => d.GlassSaturation = v);
            yield return ColorField("GlassDesigner_Tint", "Tint", d => d.GlassTint, (d, v) => d.GlassTint = v);
            yield return Slider("GlassDesigner_TintOpacity", "Tint opacity", 0, 1, 0.01, d => d.GlassTintOpacity, (d, v) => d.GlassTintOpacity = v);
            yield return Slider("GlassDesigner_ReflectOpacity", "Reflection", 0, 1, 0.01, d => d.GlassReflectOpacity, (d, v) => d.GlassReflectOpacity = v);
            yield return Slider("GlassDesigner_ReflectAngle", "Reflection angle", 0, 360, 5, d => d.GlassReflectDegree, (d, v) => d.GlassReflectDegree = v, "0°");
            yield return Slider("GlassDesigner_SimulatedOpacity", "Simulated opacity", 0, 1, 0.01, d => d.GlassOpacity, (d, v) => d.GlassOpacity = v);
            var liquid = L("GlassDesigner_LiquidHint", "Liquid optics show in SkiaSharp mode.");
            yield return Slider("GlassDesigner_Depth", "Depth", 0, 1, 0.01, d => d.GlassDepth, (d, v) => d.GlassDepth = v, hint: liquid);
            yield return Slider("GlassDesigner_Curvature", "Curvature", 0, 1, 0.01, d => d.GlassCurvature, (d, v) => d.GlassCurvature = v);
            yield return Slider("GlassDesigner_Bend", "Bend", 0, 1, 0.01, d => d.GlassBend, (d, v) => d.GlassBend = v);
            yield return Slider("GlassDesigner_Dispersion", "Dispersion", 0, 1, 0.01, d => d.GlassDispersion, (d, v) => d.GlassDispersion = v);
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
                yield return Slider("GlassDesigner_FillOpacity", "Fill opacity", 0, 1, 0.01, d => d.GlassFillOpacity, (d, v) => d.GlassFillOpacity = v);
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
                    IsChecked = _design.GlassPattern == pattern,
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
                        Change(d => d.GlassPattern = pattern);
                };
                tiles.Children.Add(tile);
            }
            yield return Field("GlassDesigner_Pattern", "Pattern", tiles, wide: true);
            yield return ColorField("GlassDesigner_PatternColor", "Pattern color", d => d.GlassPatternColor, (d, v) => d.GlassPatternColor = v);
            yield return Slider("GlassDesigner_PatternOpacity", "Pattern opacity", 0, 1, 0.01, d => d.GlassPatternOpacity, (d, v) => d.GlassPatternOpacity = v);
        }

        private IEnumerable<Control> CreateMotionFields()
        {
            var renderers = Enum.GetValues<DaisyGlassBackgroundRenderer>()
                .Where(renderer => renderer != DaisyGlassBackgroundRenderer.Keep || _design.KeptBackground || _design.BackgroundRenderer == renderer)
                .ToArray();
            var shaderAvailable = DaisyGlassButtonDesign.ShaderBackgroundAvailable;
            yield return Chips("GlassDesigner_Renderer", "Animated background", renderers, d => d.BackgroundRenderer, (d, v) => d.BackgroundRenderer = v,
                wide: true, rebuildTab: true,
                isEnabled: renderer => renderer != DaisyGlassBackgroundRenderer.Shader || shaderAvailable,
                hint: shaderAvailable ? null : L("GlassDesigner_ShaderHint", "Shader needs Flowery.NET.Skia."));

            if (_design.BackgroundRenderer == DaisyGlassBackgroundRenderer.None)
                yield break;
            if (_design.BackgroundRenderer == DaisyGlassBackgroundRenderer.Keep)
            {
                yield return Slider("GlassDesigner_BackgroundOpacity", "Background opacity", 0, 1, 0.01, d => d.GlassBackgroundOpacity, (d, v) => d.GlassBackgroundOpacity = v);
                yield break;
            }

            yield return Chips("GlassDesigner_Effect", "Effect", Enum.GetValues<DaisyGlassAnimation>(), d => d.BackgroundEffect, (d, v) => d.BackgroundEffect = v);
            yield return Slider("GlassDesigner_Speed", "Speed", 0, 3, 0.05, d => d.BackgroundSpeed, (d, v) => d.BackgroundSpeed = v);
            yield return Chips("GlassDesigner_FrameRate", "Frame rate", [15d, 24d, 30d, 60d, 0d], d => d.BackgroundFrameRate, (d, v) => d.BackgroundFrameRate = v,
                label: rate => rate == 0 ? L("GlassDesigner_EveryFrame", "Every frame") : rate.ToString(CultureInfo.CurrentCulture));
            yield return Slider("GlassDesigner_BackgroundOpacity", "Background opacity", 0, 1, 0.01, d => d.GlassBackgroundOpacity, (d, v) => d.GlassBackgroundOpacity = v);
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

        private Border Chips<T>(string key, string fallback, IReadOnlyList<T> values, Func<DaisyGlassButtonDesign, T> get, Action<DaisyGlassButtonDesign, T> set,
            bool wide = false, bool rebuildTab = false, Func<T, bool>? isEnabled = null, string? hint = null, Func<T, string>? label = null)
        {
            var group = "gbd-chips-" + key + "-" + _groups;
            var chips = new WrapPanel { Orientation = Orientation.Horizontal };
            var current = get(_design);
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
                        Change(d => set(d, value), rebuildTab);
                };
                chips.Children.Add(chip);
            }
            return Field(key, fallback, chips, hint: hint, wide: wide);
        }

        private Border Slider(string key, string fallback, double minimum, double maximum, double step, Func<DaisyGlassButtonDesign, double> get,
            Action<DaisyGlassButtonDesign, double> set, string format = "0.##", string? hint = null)
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

        private Border Toggle(string key, string fallback, Func<DaisyGlassButtonDesign, bool> get, Action<DaisyGlassButtonDesign, bool> set)
        {
            var toggle = new DaisyToggle { Size = DaisySize.Medium, IsChecked = get(_design) };
            toggle.IsCheckedChanged += (_, _) => Change(d => set(d, toggle.IsChecked == true));
            return Field(key, fallback, toggle);
        }

        private Border ColorField(string key, string fallback, Func<DaisyGlassButtonDesign, Color> get, Action<DaisyGlassButtonDesign, Color> set)
        {
            var swatch = Swatch(get(_design));
            var hex = new TextBlock { Classes = { "gbd-value" }, Text = DaisyGlassButtonDesign.FormatColor(get(_design)) };
            swatch.Click += async (_, _) =>
            {
                if (await PickColorAsync(get(_design)) is not { } color)
                    return;
                ((Border)swatch.Content!).Background = new SolidColorBrush(color);
                hex.Text = DaisyGlassButtonDesign.FormatColor(color);
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
    /// The presets of <see cref="DaisyGlassButtonDesigner"/>.
    /// </summary>
    internal static class DaisyGlassButtonDesignPresets
    {
        public static IReadOnlyList<(string Name, DaisyGlassButtonDesign Design)> All { get; } =
        [
            ("Frost", new DaisyGlassButtonDesign
            {
                GlassTintOpacity = 0.2, GlassReflectOpacity = 0.3, FillKind = DaisyGlassFillKind.Preset, FillPreset = DaisyGlassGradient.Frost,
                GlassFillOpacity = 0.35
            }),
            ("Midnight", new DaisyGlassButtonDesign
            {
                Variant = DaisyButtonVariant.Neutral, GlassBlur = 40, GlassTint = Color.FromUInt32(0xFF0F172A), GlassTintOpacity = 0.45,
                GlassColorOpacity = 0.2, GlassReflectOpacity = 0.2
            }),
            ("Aurora", new DaisyGlassButtonDesign
            {
                FillKind = DaisyGlassFillKind.Preset, FillPreset = DaisyGlassGradient.Aurora, GlassFillOpacity = 0.55
            }),
            ("Sunset", new DaisyGlassButtonDesign
            {
                Shape = DaisyButtonShape.Wide, FillKind = DaisyGlassFillKind.Preset, FillPreset = DaisyGlassGradient.Sunset, GlassFillOpacity = 0.6
            }),
            ("Holo", new DaisyGlassButtonDesign
            {
                FillKind = DaisyGlassFillKind.Preset, FillPreset = DaisyGlassGradient.Holo, GlassFillOpacity = 0.5,
                GlassPattern = DaisyCardPattern.Scales, GlassPatternOpacity = 0.25
            }),
            ("Neon", new DaisyGlassButtonDesign
            {
                Variant = DaisyButtonVariant.Accent, ButtonStyle = DaisyButtonStyle.Outline, GlassTint = Color.FromUInt32(0xFF22D3EE),
                GlassTintOpacity = 0.1, GlassCurvature = 0.9, GlassDispersion = 0.5
            }),
            ("Ocean", new DaisyGlassButtonDesign
            {
                Variant = DaisyButtonVariant.Info, FillKind = DaisyGlassFillKind.Preset, FillPreset = DaisyGlassGradient.Ocean, GlassFillOpacity = 0.5
            }),
            ("Candy", new DaisyGlassButtonDesign
            {
                FillKind = DaisyGlassFillKind.Linear, FillAngle = 0, GlassFillOpacity = 0.65, GlassPattern = DaisyCardPattern.Dots,
                GlassPatternOpacity = 0.25,
                FillStops =
                [
                    new() { Color = Color.FromUInt32(0xFFF9A8D4), Offset = 0 },
                    new() { Color = Color.FromUInt32(0xFFFDBA74), Offset = 1 }
                ]
            }),
            ("Liquid Gold", new DaisyGlassButtonDesign
            {
                GlassTint = Color.FromUInt32(0xFFF59E0B), GlassTintOpacity = 0.1, BackgroundRenderer = DaisyGlassBackgroundRenderer.Gradient,
                BackgroundEffect = DaisyGlassAnimation.LiquidGold, GlassBackgroundOpacity = 0.7
            }),
            ("Plasma", new DaisyGlassButtonDesign
            {
                Variant = DaisyButtonVariant.Secondary, GlassColorOpacity = 0.2, BackgroundRenderer = DaisyGlassBackgroundRenderer.Gradient,
                BackgroundEffect = DaisyGlassAnimation.Plasma, BackgroundSpeed = 0.6
            })
        ];
    }
}
