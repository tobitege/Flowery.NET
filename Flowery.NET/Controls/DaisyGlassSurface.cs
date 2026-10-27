using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Flowery.Controls.Custom;
using Flowery.Enums;

namespace Flowery.Controls
{
    /// <summary>
    /// Puts a standard control on glass that blurs whatever lies behind it, without a glass subclass per control.
    /// </summary>
    /// <remarks>
    /// <para>
    /// While <see cref="IsEnabledProperty"/> is true, the behavior looks for a <see cref="DaisyGlass"/> named
    /// <c>PART_Glass</c> in the template of the control. The glass themes, such as <c>DaisyGlassButtonTheme</c>, bring
    /// one; the behavior passes the glass settings of the control to it and keeps the text readable over it
    /// (<see cref="TextContrastProperty"/>, <see cref="EffectiveForegroundProperty"/>).
    /// </para>
    /// <para>
    /// A template without its own glass gets one: the behavior inserts a <see cref="DaisyGlass"/> right behind the
    /// surface part of the template (<see cref="PartNameProperty"/>), which then turns transparent. The surface has to
    /// sit in a <see cref="Panel"/> of the template, and it has to stretch or have a size, since the glass has no content
    /// of its own. The glass follows the corner radius, margin, alignment, size, minimum and maximum size, visibility and
    /// grid cell of the surface. It blurs the backdrop with <c>SkiaSharp</c> (<c>BitmapCapture</c> without
    /// Flowery.NET.Skia) unless the settings say otherwise. Turning the behavior off removes the glass and restores the
    /// surface; a new template gets one new glass. <see cref="DaisyOtpInput"/> passes the behavior on to each digit box.
    /// </para>
    /// <para>
    /// The glass settings are nullable: a setting that is not set keeps the value of the glass itself, from the
    /// <see cref="DaisyGlass"/> theme.
    /// </para>
    /// <para>
    /// The glass mode (<see cref="GlobalEnabled"/>) turns the behavior on for all input controls at once.
    /// </para>
    /// </remarks>
    public static class DaisyGlassSurface
    {
        // The surface parts the behavior looks for when PartName is not set, in this order.
        private static readonly string[] DefaultPartNames =
            ["PART_Background", "Background", "PART_Surface", "PART_SelectedContainer", "PART_SolidBackground"];

        private const string GlassPartName = "PART_Glass";

        // The class of an inserted glass, which the DaisyGlass theme gives a real blur.
        private const string SurfaceClass = "glass-surface";

        private static readonly ConditionalWeakTable<TemplatedControl, SurfaceState> States = new();

        #region Switch and parts

        /// <summary>
        /// Defines the IsEnabled attached property: whether the control is on glass. <c>null</c> (the default) and
        /// <c>false</c> leave the control as it is. The glass themes set it to true.
        /// </summary>
        public static readonly AttachedProperty<bool?> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<Control, bool?>("IsEnabled", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the PartName attached property: the name of the surface part that gets the glass behind it. Without
        /// it, the behavior tries <c>PART_Background</c>, <c>Background</c>, <c>PART_Surface</c>,
        /// <c>PART_SelectedContainer</c> and <c>PART_SolidBackground</c> (cards). With a <c>PART_Glass</c> in the template, the surface part is the tint layer
        /// above that glass, which keeps its color.
        /// </summary>
        public static readonly AttachedProperty<string?> PartNameProperty =
            AvaloniaProperty.RegisterAttached<Control, string?>("PartName", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the FollowForeground attached property: whether the text, placeholder, floating label and icon parts
        /// of a field follow <see cref="TemplatedControl.Foreground"/> on glass, instead of the theme brushes made for
        /// opaque fields. Useful over colorful backdrops.
        /// </summary>
        public static readonly AttachedProperty<bool> FollowForegroundProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>("FollowForeground", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the ClearSurface attached property (default true): whether the surface part turns transparent over
        /// an inserted glass, whatever its styles set. A glass theme that gives the surface translucent colors of its
        /// own, such as <c>DaisyGlassInputTheme</c>, turns it off.
        /// </summary>
        public static readonly AttachedProperty<bool> ClearSurfaceProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>("ClearSurface", typeof(DaisyGlassSurface), true);

        /// <summary>
        /// Defines the TextContrast attached property (default true): whether the text keeps WCAG contrast over what
        /// lies behind the glass. The glass measures its backdrop, and the text color moves toward white or black,
        /// keeping its hue, until it has a contrast of 4.5 (3 for large text) against the glass over that backdrop.
        /// Needs a <c>PART_Glass</c> in the template with a tint layer over it (the surface part), as in the glass button
        /// and badge themes, and a blur mode that sees the backdrop (<c>BitmapCapture</c> or <c>SkiaSharp</c>); a
        /// <see cref="TemplatedControl.Foreground"/> set on the control itself is kept as it is. Other glass measures no
        /// backdrop.
        /// </summary>
        public static readonly AttachedProperty<bool> TextContrastProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>("TextContrast", typeof(DaisyGlassSurface), true);

        /// <summary>
        /// Defines the EffectiveForeground attached property, which the behavior sets: the brush the glass themes draw
        /// text and icons with. It is <see cref="TemplatedControl.Foreground"/>, adjusted for contrast while
        /// <see cref="TextContrastProperty"/> is on.
        /// </summary>
        public static readonly AttachedProperty<IBrush?> EffectiveForegroundProperty =
            AvaloniaProperty.RegisterAttached<Control, IBrush?>("EffectiveForeground", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the ColorOpacity attached property: the opacity of the variant color over the glass, including its
        /// hover color, in the glass themes of buttons and badges.
        /// </summary>
        public static readonly AttachedProperty<double> ColorOpacityProperty =
            AvaloniaProperty.RegisterAttached<Control, double>("ColorOpacity", typeof(DaisyGlassSurface), 0.35);

        /// <summary>
        /// Defines the TextShadowOpacity attached property: the opacity of the shadow under the text and icons of the
        /// glass themes, which keeps them readable on any backdrop.
        /// </summary>
        public static readonly AttachedProperty<double> TextShadowOpacityProperty =
            AvaloniaProperty.RegisterAttached<Control, double>("TextShadowOpacity", typeof(DaisyGlassSurface), 0.5);

        public static bool? GetIsEnabled(Control element) => element.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(Control element, bool? value) => element.SetValue(IsEnabledProperty, value);

        public static string? GetPartName(Control element) => element.GetValue(PartNameProperty);
        public static void SetPartName(Control element, string? value) => element.SetValue(PartNameProperty, value);

        public static bool GetFollowForeground(Control element) => element.GetValue(FollowForegroundProperty);
        public static void SetFollowForeground(Control element, bool value) => element.SetValue(FollowForegroundProperty, value);

        public static bool GetClearSurface(Control element) => element.GetValue(ClearSurfaceProperty);
        public static void SetClearSurface(Control element, bool value) => element.SetValue(ClearSurfaceProperty, value);

        public static bool GetTextContrast(Control element) => element.GetValue(TextContrastProperty);
        public static void SetTextContrast(Control element, bool value) => element.SetValue(TextContrastProperty, value);

        public static IBrush? GetEffectiveForeground(Control element) => element.GetValue(EffectiveForegroundProperty);

        public static double GetColorOpacity(Control element) => element.GetValue(ColorOpacityProperty);
        public static void SetColorOpacity(Control element, double value) => element.SetValue(ColorOpacityProperty, value);

        public static double GetTextShadowOpacity(Control element) => element.GetValue(TextShadowOpacityProperty);
        public static void SetTextShadowOpacity(Control element, double value) => element.SetValue(TextShadowOpacityProperty, value);

        #endregion

        #region Glass settings

        /// <summary>
        /// Defines the EnableBackdropBlur attached property: whether the glass blurs the backdrop. When false, the glass
        /// is simulated with overlays. Maps to <see cref="DaisyGlass.EnableBackdropBlur"/>.
        /// </summary>
        public static readonly AttachedProperty<bool?> EnableBackdropBlurProperty =
            AvaloniaProperty.RegisterAttached<Control, bool?>("EnableBackdropBlur", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the BlurMode attached property: how the backdrop is blurred. Maps to <see cref="DaisyGlass.BlurMode"/>.
        /// </summary>
        public static readonly AttachedProperty<GlassBlurMode?> BlurModeProperty =
            AvaloniaProperty.RegisterAttached<Control, GlassBlurMode?>("BlurMode", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the Blur attached property: the blur amount of the backdrop. Maps to <see cref="DaisyGlass.GlassBlur"/>.
        /// </summary>
        public static readonly AttachedProperty<double?> BlurProperty =
            AvaloniaProperty.RegisterAttached<Control, double?>("Blur", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the Saturation attached property: the saturation of the blurred backdrop (0 = grayscale, 1 = normal).
        /// Maps to <see cref="DaisyGlass.GlassSaturation"/>.
        /// </summary>
        public static readonly AttachedProperty<double?> SaturationProperty =
            AvaloniaProperty.RegisterAttached<Control, double?>("Saturation", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the Opacity attached property: the opacity of the simulated glass, used when the backdrop is not
        /// blurred. Maps to <see cref="DaisyGlass.GlassOpacity"/>.
        /// </summary>
        public static readonly AttachedProperty<double?> OpacityProperty =
            AvaloniaProperty.RegisterAttached<Control, double?>("Opacity", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the Tint attached property: the tint color of the glass; without it, the theme tint
        /// (<c>DaisyGlassTintColor</c>). Maps to <see cref="DaisyGlass.GlassTint"/>.
        /// </summary>
        public static readonly AttachedProperty<Color?> TintProperty =
            AvaloniaProperty.RegisterAttached<Control, Color?>("Tint", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the TintOpacity attached property: the opacity of the tint over the blurred backdrop. Maps to
        /// <see cref="DaisyGlass.GlassTintOpacity"/>.
        /// </summary>
        public static readonly AttachedProperty<double?> TintOpacityProperty =
            AvaloniaProperty.RegisterAttached<Control, double?>("TintOpacity", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the ReflectDegree attached property: the angle of the reflection in degrees. Maps to
        /// <see cref="DaisyGlass.GlassReflectDegree"/>.
        /// </summary>
        public static readonly AttachedProperty<double?> ReflectDegreeProperty =
            AvaloniaProperty.RegisterAttached<Control, double?>("ReflectDegree", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the ReflectOpacity attached property: the opacity of the reflection. Maps to
        /// <see cref="DaisyGlass.GlassReflectOpacity"/>.
        /// </summary>
        public static readonly AttachedProperty<double?> ReflectOpacityProperty =
            AvaloniaProperty.RegisterAttached<Control, double?>("ReflectOpacity", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the Depth attached property: how far the liquid-glass refraction reaches inward from the edge. Maps
        /// to <see cref="DaisyGlass.GlassDepth"/>.
        /// </summary>
        public static readonly AttachedProperty<double?> DepthProperty =
            AvaloniaProperty.RegisterAttached<Control, double?>("Depth", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the Curvature attached property: the convex lens curvature of the liquid-glass highlight. Maps to
        /// <see cref="DaisyGlass.GlassCurvature"/>.
        /// </summary>
        public static readonly AttachedProperty<double?> CurvatureProperty =
            AvaloniaProperty.RegisterAttached<Control, double?>("Curvature", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the Bend attached property: the inner rim bend of the liquid-glass meniscus. Maps to
        /// <see cref="DaisyGlass.GlassBend"/>.
        /// </summary>
        public static readonly AttachedProperty<double?> BendProperty =
            AvaloniaProperty.RegisterAttached<Control, double?>("Bend", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the Dispersion attached property: the chromatic edge split of the liquid-glass rim. Maps to
        /// <see cref="DaisyGlass.GlassDispersion"/>.
        /// </summary>
        public static readonly AttachedProperty<double?> DispersionProperty =
            AvaloniaProperty.RegisterAttached<Control, double?>("Dispersion", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the Fill attached property: a brush, such as a gradient, painted over the blurred backdrop. It takes
        /// precedence over <see cref="GradientProperty"/>. Maps to <see cref="DaisyGlass.GlassFill"/>.
        /// </summary>
        public static readonly AttachedProperty<IBrush?> FillProperty =
            AvaloniaProperty.RegisterAttached<Control, IBrush?>("Fill", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the Gradient attached property: a gradient preset painted over the blurred backdrop when
        /// <see cref="FillProperty"/> is not set. Maps to <see cref="DaisyGlass.GlassGradient"/>.
        /// </summary>
        public static readonly AttachedProperty<DaisyGlassGradient?> GradientProperty =
            AvaloniaProperty.RegisterAttached<Control, DaisyGlassGradient?>("Gradient", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the FillOpacity attached property: the opacity of the fill over the blurred backdrop. Maps to
        /// <see cref="DaisyGlass.GlassFillOpacity"/>.
        /// </summary>
        public static readonly AttachedProperty<double?> FillOpacityProperty =
            AvaloniaProperty.RegisterAttached<Control, double?>("FillOpacity", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the Pattern attached property: a pattern drawn over the fill, using the same patterns as
        /// <see cref="DaisyPatternedCard"/>. Maps to <see cref="DaisyGlass.GlassPattern"/>.
        /// </summary>
        public static readonly AttachedProperty<DaisyCardPattern?> PatternProperty =
            AvaloniaProperty.RegisterAttached<Control, DaisyCardPattern?>("Pattern", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the PatternColor attached property: the color of the pattern. Maps to
        /// <see cref="DaisyGlass.GlassPatternColor"/>.
        /// </summary>
        public static readonly AttachedProperty<Color?> PatternColorProperty =
            AvaloniaProperty.RegisterAttached<Control, Color?>("PatternColor", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the PatternOpacity attached property: the opacity of a typical stroke of the pattern. Maps to
        /// <see cref="DaisyGlass.GlassPatternOpacity"/>.
        /// </summary>
        public static readonly AttachedProperty<double?> PatternOpacityProperty =
            AvaloniaProperty.RegisterAttached<Control, double?>("PatternOpacity", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the Background attached property: an animated background drawn over the fill and below the pattern.
        /// Maps to <see cref="DaisyGlass.GlassBackground"/>.
        /// </summary>
        public static readonly AttachedProperty<DaisyGlassBackground?> BackgroundProperty =
            AvaloniaProperty.RegisterAttached<Control, DaisyGlassBackground?>("Background", typeof(DaisyGlassSurface));

        /// <summary>
        /// Defines the BackgroundOpacity attached property: the opacity of the animated background. Maps to
        /// <see cref="DaisyGlass.GlassBackgroundOpacity"/>.
        /// </summary>
        public static readonly AttachedProperty<double?> BackgroundOpacityProperty =
            AvaloniaProperty.RegisterAttached<Control, double?>("BackgroundOpacity", typeof(DaisyGlassSurface));

        public static bool? GetEnableBackdropBlur(Control element) => element.GetValue(EnableBackdropBlurProperty);
        public static void SetEnableBackdropBlur(Control element, bool? value) => element.SetValue(EnableBackdropBlurProperty, value);

        public static GlassBlurMode? GetBlurMode(Control element) => element.GetValue(BlurModeProperty);
        public static void SetBlurMode(Control element, GlassBlurMode? value) => element.SetValue(BlurModeProperty, value);

        public static double? GetBlur(Control element) => element.GetValue(BlurProperty);
        public static void SetBlur(Control element, double? value) => element.SetValue(BlurProperty, value);

        public static double? GetSaturation(Control element) => element.GetValue(SaturationProperty);
        public static void SetSaturation(Control element, double? value) => element.SetValue(SaturationProperty, value);

        public static double? GetOpacity(Control element) => element.GetValue(OpacityProperty);
        public static void SetOpacity(Control element, double? value) => element.SetValue(OpacityProperty, value);

        public static Color? GetTint(Control element) => element.GetValue(TintProperty);
        public static void SetTint(Control element, Color? value) => element.SetValue(TintProperty, value);

        public static double? GetTintOpacity(Control element) => element.GetValue(TintOpacityProperty);
        public static void SetTintOpacity(Control element, double? value) => element.SetValue(TintOpacityProperty, value);

        public static double? GetReflectDegree(Control element) => element.GetValue(ReflectDegreeProperty);
        public static void SetReflectDegree(Control element, double? value) => element.SetValue(ReflectDegreeProperty, value);

        public static double? GetReflectOpacity(Control element) => element.GetValue(ReflectOpacityProperty);
        public static void SetReflectOpacity(Control element, double? value) => element.SetValue(ReflectOpacityProperty, value);

        public static double? GetDepth(Control element) => element.GetValue(DepthProperty);
        public static void SetDepth(Control element, double? value) => element.SetValue(DepthProperty, value);

        public static double? GetCurvature(Control element) => element.GetValue(CurvatureProperty);
        public static void SetCurvature(Control element, double? value) => element.SetValue(CurvatureProperty, value);

        public static double? GetBend(Control element) => element.GetValue(BendProperty);
        public static void SetBend(Control element, double? value) => element.SetValue(BendProperty, value);

        public static double? GetDispersion(Control element) => element.GetValue(DispersionProperty);
        public static void SetDispersion(Control element, double? value) => element.SetValue(DispersionProperty, value);

        public static IBrush? GetFill(Control element) => element.GetValue(FillProperty);
        public static void SetFill(Control element, IBrush? value) => element.SetValue(FillProperty, value);

        public static DaisyGlassGradient? GetGradient(Control element) => element.GetValue(GradientProperty);
        public static void SetGradient(Control element, DaisyGlassGradient? value) => element.SetValue(GradientProperty, value);

        public static double? GetFillOpacity(Control element) => element.GetValue(FillOpacityProperty);
        public static void SetFillOpacity(Control element, double? value) => element.SetValue(FillOpacityProperty, value);

        public static DaisyCardPattern? GetPattern(Control element) => element.GetValue(PatternProperty);
        public static void SetPattern(Control element, DaisyCardPattern? value) => element.SetValue(PatternProperty, value);

        public static Color? GetPatternColor(Control element) => element.GetValue(PatternColorProperty);
        public static void SetPatternColor(Control element, Color? value) => element.SetValue(PatternColorProperty, value);

        public static double? GetPatternOpacity(Control element) => element.GetValue(PatternOpacityProperty);
        public static void SetPatternOpacity(Control element, double? value) => element.SetValue(PatternOpacityProperty, value);

        public static DaisyGlassBackground? GetBackground(Control element) => element.GetValue(BackgroundProperty);
        public static void SetBackground(Control element, DaisyGlassBackground? value) => element.SetValue(BackgroundProperty, value);

        public static double? GetBackgroundOpacity(Control element) => element.GetValue(BackgroundOpacityProperty);
        public static void SetBackgroundOpacity(Control element, double? value) => element.SetValue(BackgroundOpacityProperty, value);

        // Each glass setting and the DaisyGlass property it maps to.
        private static readonly Dictionary<AvaloniaProperty, AvaloniaProperty> SettingMap = new()
        {
            [EnableBackdropBlurProperty] = DaisyGlass.EnableBackdropBlurProperty,
            [BlurModeProperty] = DaisyGlass.BlurModeProperty,
            [BlurProperty] = DaisyGlass.GlassBlurProperty,
            [SaturationProperty] = DaisyGlass.GlassSaturationProperty,
            [OpacityProperty] = DaisyGlass.GlassOpacityProperty,
            [TintProperty] = DaisyGlass.GlassTintProperty,
            [TintOpacityProperty] = DaisyGlass.GlassTintOpacityProperty,
            [ReflectDegreeProperty] = DaisyGlass.GlassReflectDegreeProperty,
            [ReflectOpacityProperty] = DaisyGlass.GlassReflectOpacityProperty,
            [DepthProperty] = DaisyGlass.GlassDepthProperty,
            [CurvatureProperty] = DaisyGlass.GlassCurvatureProperty,
            [BendProperty] = DaisyGlass.GlassBendProperty,
            [DispersionProperty] = DaisyGlass.GlassDispersionProperty,
            [FillProperty] = DaisyGlass.GlassFillProperty,
            [GradientProperty] = DaisyGlass.GlassGradientProperty,
            [FillOpacityProperty] = DaisyGlass.GlassFillOpacityProperty,
            [PatternProperty] = DaisyGlass.GlassPatternProperty,
            [PatternColorProperty] = DaisyGlass.GlassPatternColorProperty,
            [PatternOpacityProperty] = DaisyGlass.GlassPatternOpacityProperty,
            [BackgroundProperty] = DaisyGlass.GlassBackgroundProperty,
            [BackgroundOpacityProperty] = DaisyGlass.GlassBackgroundOpacityProperty
        };

        /// <summary>
        /// The glass settings, each with the <see cref="DaisyGlass"/> property it maps to.
        /// </summary>
        internal static IReadOnlyDictionary<AvaloniaProperty, AvaloniaProperty> Settings => SettingMap;

        #endregion

        #region Glass mode

        // The input controls the glass mode puts on glass.
        private static readonly Type[] GlobalTypes =
        [
            typeof(DaisyInput), typeof(DaisyTextArea), typeof(DaisyPasswordBox), typeof(DaisyMaskInput),
            typeof(DaisyNumericUpDown), typeof(DaisySelect), typeof(DaisyCalendarDatePicker), typeof(DaisyFileInput),
            typeof(DaisyTagPicker), typeof(DaisyOtpInput)
        ];

        private static bool _globalEnabled;
        private static Styles? _globalStyles;
        private static readonly CurrentValue<GlassBlurMode> GlobalBlurModes = new(GlassBlurMode.SkiaSharp);

        /// <summary>
        /// Fired when <see cref="GlobalEnabled"/> or <see cref="GlobalBlurMode"/> changes.
        /// </summary>
        public static event EventHandler? GlobalChanged;

        /// <summary>
        /// The glass mode: when true, the input controls (<see cref="DaisyInput"/>, <see cref="DaisyTextArea"/>,
        /// <see cref="DaisyPasswordBox"/>, <see cref="DaisyMaskInput"/>, <see cref="DaisyNumericUpDown"/>,
        /// <see cref="DaisySelect"/>, <see cref="DaisyCalendarDatePicker"/>, <see cref="DaisyFileInput"/>,
        /// <see cref="DaisyTagPicker"/> and <see cref="DaisyOtpInput"/>) are on glass, and the glass tint
        /// (<c>DaisyGlassTintColor</c>) follows <c>Base100</c> of the active theme unless the theme sets its own tint.
        /// It works with every theme. An <see cref="IsEnabledProperty"/> set on a control wins over it.
        /// </summary>
        public static bool GlobalEnabled
        {
            get => _globalEnabled;
            set
            {
                if (_globalEnabled == value)
                    return;
                _globalEnabled = value;
                UpdateGlobalStyles();
            }
        }

        /// <summary>
        /// The blur mode of the glass the glass mode puts behind the input controls (default <c>SkiaSharp</c>, which
        /// falls back to <c>BitmapCapture</c> without Flowery.NET.Skia). A <see cref="BlurModeProperty"/> set on a
        /// control wins over it.
        /// </summary>
        public static GlassBlurMode GlobalBlurMode
        {
            get => GlobalBlurModes.Value;
            set
            {
                if (GlobalBlurModes.Value == value)
                    return;
                GlobalBlurModes.Value = value;
                GlobalChanged?.Invoke(null, EventArgs.Empty);
            }
        }

        // A value that tells its subscribers the current value and every change.
        private sealed class CurrentValue<T>(T value) : IObservable<T>
        {
            private readonly List<IObserver<T>> _observers = [];
            private T _value = value;

            public T Value
            {
                get => _value;
                set
                {
                    _value = value;
                    foreach (var observer in _observers.ToArray())
                        observer.OnNext(value);
                }
            }

            public IDisposable Subscribe(IObserver<T> observer)
            {
                _observers.Add(observer);
                observer.OnNext(_value);
                return new Unsubscriber(_observers, observer);
            }

            private sealed class Unsubscriber(List<IObserver<T>> observers, IObserver<T> observer) : IDisposable
            {
                public void Dispose() => observers.Remove(observer);
            }
        }

        // The glass mode is made of app styles: a value set on a control, and the styles of its window, win over them.
        // The blur mode reaches the styles through a binding, so a change keeps the styles and the glasses in place.
        private static void UpdateGlobalStyles()
        {
            if (Application.Current is { } app)
            {
                if (_globalEnabled && _globalStyles == null)
                {
                    _globalStyles = [];
                    foreach (var type in GlobalTypes)
                        _globalStyles.Add(new Style(x => x.Is(type)) { Setters = { new Setter(IsEnabledProperty, true) } });
                    _globalStyles.Add(new Style(x => x.OfType<DaisyGlass>().Class(SurfaceClass))
                    {
                        Setters = { new Setter(DaisyGlass.BlurModeProperty, GlobalBlurModes.ToBinding()) }
                    });
                    app.Styles.Add(_globalStyles);
                }
                else if (!_globalEnabled && _globalStyles != null)
                {
                    app.Styles.Remove(_globalStyles);
                    _globalStyles = null;
                }

                DaisyThemeManager.RefreshGlassResources();
            }
            GlobalChanged?.Invoke(null, EventArgs.Empty);
        }

        #endregion

        static DaisyGlassSurface()
        {
            IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
        }

        /// <summary>
        /// Returns the glass the behavior currently drives on <paramref name="control"/>: the <c>PART_Glass</c> of its
        /// template or the glass it inserted; <c>null</c> while the control is not on glass.
        /// </summary>
        public static DaisyGlass? GetGlass(Control control) =>
            control is TemplatedControl templated && States.TryGetValue(templated, out var state) ? state.ActiveGlass : null;

        /// <summary>
        /// Puts <paramref name="target"/>, a part that a control builds itself such as a digit box of
        /// <see cref="DaisyOtpInput"/>, on glass whenever <paramref name="source"/> is: the switch, the options and the
        /// glass settings of the source pass on to the target, whose surface part is <paramref name="partName"/>.
        /// Dispose the result when the target goes.
        /// </summary>
        internal static IDisposable Forward(Control source, Control target, string partName)
        {
            target.SetValue(PartNameProperty, partName);
            var bindings = new List<IDisposable>();
            foreach (var property in new AvaloniaProperty[] { IsEnabledProperty, FollowForegroundProperty, ClearSurfaceProperty }
                         .Concat(SettingMap.Keys))
            {
                bindings.Add(target.Bind(property, source.GetObservable(property)));
            }
            return new CompositeDisposable(bindings);
        }

        private sealed class CompositeDisposable(List<IDisposable> items) : IDisposable
        {
            public void Dispose()
            {
                foreach (var item in items)
                    item.Dispose();
                items.Clear();
            }
        }

        private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs e)
        {
            // Once tracked, a control stays tracked: while off, its EffectiveForeground still follows Foreground and a
            // glass in its template stays hidden.
            if (control is TemplatedControl templated && !States.TryGetValue(templated, out _))
            {
                var state = new SurfaceState(templated);
                States.Add(templated, state);
                state.Apply();
            }
        }

        private sealed class SurfaceState
        {
            private const double PlaceholderOpacity = 0.65;

            private static readonly IBrush FloatingLabelBackground = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)).ToImmutable();

            private readonly TemplatedControl _owner;
            private readonly List<IDisposable> _templateValues = [];
            private INameScope? _nameScope;
            private DaisyGlass? _glass;
            private Panel? _insertedInto;
            private GlassTextContrast? _contrast;
            private bool? _appliedEnabled;

            public SurfaceState(TemplatedControl owner)
            {
                _owner = owner;
                _owner.TemplateApplied += OnTemplateApplied;
                _owner.PropertyChanged += OnOwnerPropertyChanged;
            }

            public DaisyGlass? ActiveGlass { get; private set; }

            private void OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
            {
                _nameScope = e.NameScope;
                Apply();
            }

            private void OnOwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
            {
                var property = e.Property;
                // The class handler that created this state already applied the change that created it.
                if (property == IsEnabledProperty && GetIsEnabled(_owner) == _appliedEnabled)
                    return;
                if (property == IsEnabledProperty || property == PartNameProperty || property == FollowForegroundProperty
                    || property == ClearSurfaceProperty)
                {
                    Apply();
                    return;
                }
                if (ActiveGlass != null && SettingMap.TryGetValue(property, out var glassProperty))
                {
                    ApplySetting(ActiveGlass, glassProperty, e.NewValue);
                    return;
                }
                GlassTextContrast.OnOwnerPropertyChanged(_contrast, _owner, e, TextContrastProperty, SetEffectiveForeground);
            }

            private void SetEffectiveForeground(IBrush? brush) => _owner.SetValue(EffectiveForegroundProperty, brush);

            /// <summary>
            /// Puts the current template on glass, or takes it off, after undoing what the previous call did.
            /// </summary>
            public void Apply()
            {
                Reset();

                _appliedEnabled = GetIsEnabled(_owner);
                var glass = Find<DaisyGlass>(GlassPartName);
                var surface = FindSurface();
                if (_appliedEnabled != true)
                {
                    // A glass theme turned off shows its surface without the glass.
                    if (glass != null)
                        _templateValues.Add(glass.SetValue(Visual.IsVisibleProperty, false, BindingPriority.Animation)!);
                    _contrast = GlassTextContrast.Attach(null, _owner, null, null, false, SetEffectiveForeground);
                    return;
                }

                var inserted = false;
                if (glass == null && surface is { Parent: Panel host })
                {
                    glass = new DaisyGlass { Name = GlassPartName, Padding = default, IsHitTestVisible = false };
                    glass.Classes.Add(SurfaceClass);
                    MirrorLayout(glass, surface);
                    host.Children.Insert(host.Children.IndexOf(surface), glass);
                    _glass = glass;
                    _insertedInto = host;
                    inserted = true;
                    if (GetClearSurface(_owner) && SetBackground(surface, Brushes.Transparent) is { } transparent)
                        _templateValues.Add(transparent);
                }

                ActiveGlass = glass;
                if (glass != null)
                {
                    foreach (var (setting, glassProperty) in SettingMap)
                        ApplySetting(glass, glassProperty, _owner.GetValue(setting));
                }

                // Only a template with a tint layer over its glass, as on glass buttons and badges, draws its own text on
                // the glass; there the contrast reads the tint. Other glass shows app content, which the contrast cannot
                // reach, so its glass measures no backdrop. An inserted glass has a transparent surface.
                var tint = inserted ? null : surface as Border;
                _contrast = GlassTextContrast.Attach(null, _owner, tint != null ? glass : null, tint,
                    GetTextContrast(_owner), SetEffectiveForeground);

                if (glass != null && GetFollowForeground(_owner))
                    FollowForeground();
            }

            private void Reset()
            {
                _contrast?.Dispose();
                _contrast = null;
                foreach (var value in _templateValues)
                    value.Dispose();
                _templateValues.Clear();
                if (_glass != null)
                    _insertedInto?.Children.Remove(_glass);
                _glass = null;
                _insertedInto = null;
                ActiveGlass = null;
            }

            private static void ApplySetting(DaisyGlass glass, AvaloniaProperty glassProperty, object? value)
            {
                if (value == null)
                    glass.ClearValue(glassProperty);
                else
                    glass.SetValue(glassProperty, value);
            }

            private void MirrorLayout(DaisyGlass glass, Control surface)
            {
                if (surface is Border)
                    _templateValues.Add(glass.Bind(TemplatedControl.CornerRadiusProperty, surface.GetObservable(Border.CornerRadiusProperty)));
                foreach (var property in new AvaloniaProperty[]
                         {
                             Layoutable.MarginProperty, Layoutable.HorizontalAlignmentProperty, Layoutable.VerticalAlignmentProperty,
                             Layoutable.WidthProperty, Layoutable.HeightProperty, Layoutable.MinWidthProperty,
                             Layoutable.MinHeightProperty, Layoutable.MaxWidthProperty, Layoutable.MaxHeightProperty,
                             Visual.IsVisibleProperty,
                             Grid.RowProperty, Grid.ColumnProperty, Grid.RowSpanProperty, Grid.ColumnSpanProperty
                         })
                {
                    _templateValues.Add(glass.Bind(property, surface.GetObservable(property)));
                }
            }

            private static IDisposable? SetBackground(Control surface, IBrush brush) => surface switch
            {
                Border border => border.SetValue(Border.BackgroundProperty, brush, BindingPriority.Animation),
                Panel panel => panel.SetValue(Panel.BackgroundProperty, brush, BindingPriority.Animation),
                TemplatedControl templated => templated.SetValue(TemplatedControl.BackgroundProperty, brush, BindingPriority.Animation),
                _ => null
            };

            // The fields color their text, icons, placeholder and floating label with theme brushes made for opaque
            // fields, as values inside the template. On glass they follow Foreground; a style cannot reach a value set in
            // a template part, so the parts are bound here, and the bindings go with the template.
            private void FollowForeground()
            {
                var foreground = _owner.GetObservable(TemplatedControl.ForegroundProperty);
                if (Find<TextBlock>("PART_Watermark") is { } watermark)
                {
                    _templateValues.Add(watermark.Bind(TextBlock.ForegroundProperty, foreground));
                    if (_owner is not DaisyInput { LabelPosition: DaisyLabelPosition.Floating })
                        _templateValues.Add(watermark.SetValue(Visual.OpacityProperty, PlaceholderOpacity, BindingPriority.Animation)!);
                }
                if (Find<TextPresenter>("PART_TextPresenter") is { } text)
                    _templateValues.Add(text.Bind(TextElement.ForegroundProperty, foreground));
                foreach (var name in new[] { "PART_StartIcon", "PART_EndIcon" })
                {
                    if (Find<PathIcon>(name) is { } icon)
                        _templateValues.Add(icon.Bind(TemplatedControl.ForegroundProperty, foreground));
                }
                if (Find<TextBlock>("PART_FloatingLabel") is { } label)
                    _templateValues.Add(label.Bind(TextBlock.ForegroundProperty, foreground));
                if (Find<Border>("PART_FloatingLabelContainer") is { } labelChip)
                    _templateValues.Add(labelChip.SetValue(Border.BackgroundProperty, FloatingLabelBackground, BindingPriority.Animation)!);
            }

            private Control? FindSurface()
            {
                if (GetPartName(_owner) is { Length: > 0 } partName)
                    return Find<Control>(partName);
                foreach (var name in DefaultPartNames)
                {
                    if (Find<Control>(name) is { } part)
                        return part;
                }
                return null;
            }

            // The name scope of the template, once it was applied after the behavior started; otherwise the template
            // children of the control.
            private T? Find<T>(string name) where T : Control
            {
                if (_nameScope != null)
                    return _nameScope.Find(name) as T;
                return _owner.GetVisualDescendants().OfType<T>()
                    .FirstOrDefault(c => c.Name == name && ReferenceEquals(c.TemplatedParent, _owner));
            }
        }
    }
}
