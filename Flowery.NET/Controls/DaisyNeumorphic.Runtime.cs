using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Helpers;

namespace Flowery.Controls
{
    public static partial class DaisyNeumorphic
    {
        /// <summary>
        /// What one type declares. Declarations are merged along the type hierarchy on lookup,
        /// so the order of the Register calls does not matter.
        /// </summary>
        private sealed class Declaration
        {
            public List<Surface> Surfaces { get; } = [];
            public HashSet<AvaloniaProperty> RefreshOn { get; } = [];
            public bool IsExcluded { get; set; }
            public bool IsSegmentContainer { get; set; }
            public bool IsShadowPassThrough { get; set; }
            public bool IsDisabledByDefault { get; set; }
            public bool SuppressEffect { get; set; }
        }

        /// <summary>
        /// The merged declarations of a concrete type and its base types. The surfaces of the most derived
        /// type that declares any replace those of its base types; flags and refresh properties accumulate.
        /// </summary>
        private sealed class Registration
        {
            public required IReadOnlyList<Surface> Surfaces { get; init; }
            public required IReadOnlySet<AvaloniaProperty> RefreshOn { get; init; }
            public bool IsExcluded { get; init; }
            public bool IsSegmentContainer { get; init; }
            public bool IsShadowPassThrough { get; init; }
            public bool EnabledByDefault { get; init; }
            public bool SuppressEffect { get; init; }
            public bool DrawsEffect => !IsExcluded && Surfaces.Count > 0;
        }

        private sealed record Surface(
            Func<Control, Border?> Resolve,
            Func<Control, Border?>? OuterShadowLayer,
            Func<Control, Border?>? InsetOverlay,
            DaisyNeumorphicMode? FixedMode,
            Func<Control, bool>? IsActive,
            bool RequiresBackground);

        private readonly record struct ShadowSettings(
            DaisyNeumorphicMode Mode,
            double Intensity,
            double Elevation,
            Color DarkColor,
            Color LightColor);

        private sealed class TrackedControl
        {
            public List<IDisposable> AppliedValues { get; } = [];
            public bool LayoutRetryUsed { get; set; }
        }

        /// <summary>
        /// Re-applies the effect when a watched surface border changes its background.
        /// </summary>
        private sealed class BackgroundWatch : IDisposable
        {
            private readonly Border _border;
            private readonly Control _owner;

            public BackgroundWatch(Border border, Control owner)
            {
                _border = border;
                _owner = owner;
                _border.PropertyChanged += OnBorderPropertyChanged;
            }

            public void Dispose() => _border.PropertyChanged -= OnBorderPropertyChanged;

            private void OnBorderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
            {
                if (e.Property == Border.BackgroundProperty)
                {
                    QueueRefresh(_owner);
                }
            }
        }

        private static readonly Dictionary<Type, Declaration> _declarations = [];
        private static readonly Dictionary<Type, Registration?> _registrationCache = [];
        private static readonly ConditionalWeakTable<TemplatedControl, INameScope> _templateNameScopes = new();
        private static readonly Dictionary<Control, TrackedControl> _tracked = [];
        private static readonly HashSet<Control> _pending = [];
        private static bool _refreshPosted;
        private static (Color Dark, Color Light)? _themeShadowColors;

        static DaisyNeumorphic()
        {
            Control.LoadedEvent.AddClassHandler<Control>(OnControlLoaded);
            Control.UnloadedEvent.AddClassHandler<Control>(OnControlUnloaded);
            TemplatedControl.TemplateAppliedEvent.AddClassHandler<TemplatedControl>(OnTemplateApplied);

            IsEnabledProperty.Changed.AddClassHandler<Control>(OnSettingChanged);
            ModeProperty.Changed.AddClassHandler<Control>(OnSettingChanged);
            IntensityProperty.Changed.AddClassHandler<Control>(OnSettingChanged);
            BlurRadiusProperty.Changed.AddClassHandler<Control>(OnSettingChanged);
            OffsetProperty.Changed.AddClassHandler<Control>(OnSettingChanged);
            DarkShadowColorProperty.Changed.AddClassHandler<Control>(OnSettingChanged);
            LightShadowColorProperty.Changed.AddClassHandler<Control>(OnSettingChanged);
            ScopeEnabledProperty.Changed.AddClassHandler<Control>(OnScopeChanged);
            ScopeModeProperty.Changed.AddClassHandler<Control>(OnScopeChanged);
            ScopeIntensityProperty.Changed.AddClassHandler<Control>(OnScopeChanged);
            DaisyAccessibility.HighContrastEnabledProperty.Changed.AddClassHandler<Control>(OnSettingChanged);

            DaisyThemeManager.ThemeChanged += OnThemeChanged;

            RegisterBuiltInSurfaces();
        }

        #region Registration

        /// <summary>
        /// Registers a surface border of <typeparamref name="TControl"/> (and derived types) that draws the effect.
        /// </summary>
        /// <param name="resolveSurface">Returns the surface border, or null while it does not exist yet.</param>
        public static void RegisterSurface<TControl>(Func<TControl, Border?> resolveSurface) where TControl : Control
        {
            ArgumentNullException.ThrowIfNull(resolveSurface);
            RegisterSurface(new DaisyNeumorphicSurface<TControl> { Resolve = resolveSurface });
        }

        /// <summary>
        /// Registers a surface of <typeparamref name="TControl"/> (and derived types) that draws the effect.
        /// Call it more than once to register several surfaces of one control. Surfaces registered for a
        /// derived type replace those of its base types.
        /// </summary>
        public static void RegisterSurface<TControl>(DaisyNeumorphicSurface<TControl> surface) where TControl : Control
        {
            ArgumentNullException.ThrowIfNull(surface);

            var resolve = surface.Resolve;
            var outer = surface.OuterShadowLayer;
            var inset = surface.InsetOverlay;
            var isActive = surface.IsActive;
            var declaration = Declare(typeof(TControl));
            declaration.Surfaces.Add(new Surface(
                c => resolve((TControl)c),
                outer == null ? null : c => outer((TControl)c),
                inset == null ? null : c => inset((TControl)c),
                surface.FixedMode,
                isActive == null ? null : c => isActive((TControl)c),
                surface.RequiresBackground));
        }

        /// <summary>
        /// Re-applies the effect of <typeparamref name="TControl"/> (and derived types) when one of these control
        /// properties changes, e.g. the properties read by <see cref="DaisyNeumorphicSurface{TControl}.IsActive"/>.
        /// The control's DaisySize Size property is always included.
        /// </summary>
        public static void RegisterRefreshTriggers<TControl>(params AvaloniaProperty[] properties) where TControl : Control
        {
            ArgumentNullException.ThrowIfNull(properties);
            Declare(typeof(TControl)).RefreshOn.UnionWith(properties);
        }

        /// <summary>
        /// Excludes <typeparamref name="TControl"/> (and derived types) from the effect, even if a base type is registered.
        /// </summary>
        public static void RegisterExclusion<TControl>() where TControl : Control =>
            Declare(typeof(TControl)).IsExcluded = true;

        /// <summary>
        /// Marks <typeparamref name="TControl"/> as a container whose child controls are flat segments
        /// of one surface (e.g. joins and button groups). Registered controls inside it never draw the effect;
        /// like an exclusion, a scope cannot override this.
        /// </summary>
        public static void RegisterSegmentContainer<TControl>() where TControl : Control =>
            Declare(typeof(TControl)).IsSegmentContainer = true;

        /// <summary>
        /// Marks <typeparamref name="TControl"/> as a container that lets the shadows of its registered children
        /// through: while a child draws the effect, the container's default clip (and the default clips between
        /// the child and the container) are lifted, and restored when the effect turns off.
        /// </summary>
        public static void RegisterShadowPassThrough<TControl>() where TControl : Control =>
            Declare(typeof(TControl)).IsShadowPassThrough = true;

        /// <summary>
        /// Makes <typeparamref name="TControl"/> ignore <see cref="GlobalEnabled"/>; it only draws the effect
        /// when enabled on the control itself or by a scope.
        /// </summary>
        public static void RegisterDisabledByDefault<TControl>() where TControl : Control =>
            Declare(typeof(TControl)).IsDisabledByDefault = true;

        /// <summary>
        /// Removes the control's own <see cref="Visual.Effect"/> (e.g. a drop shadow) while the effect is drawn,
        /// so the two shadows do not stack.
        /// </summary>
        public static void RegisterEffectSuppression<TControl>() where TControl : Control =>
            Declare(typeof(TControl)).SuppressEffect = true;

        /// <summary>
        /// Gets whether <paramref name="controlType"/> has at least one registered surface and is not excluded.
        /// </summary>
        public static bool IsRegistered(Type controlType) => GetRegistration(controlType) is { DrawsEffect: true };

        private static Declaration Declare(Type type)
        {
            if (!_declarations.TryGetValue(type, out var declaration))
            {
                declaration = new Declaration();
                _declarations[type] = declaration;
            }

            _registrationCache.Clear();
            return declaration;
        }

        private static Registration? GetRegistration(Type type)
        {
            if (!_registrationCache.TryGetValue(type, out var registration))
            {
                registration = MergeDeclarations(type);
                _registrationCache[type] = registration;
            }

            return registration;
        }

        private static Registration? MergeDeclarations(Type type)
        {
            var chain = new List<Declaration>();
            for (var current = type; current != null; current = current.BaseType)
            {
                if (_declarations.TryGetValue(current, out var declaration))
                {
                    chain.Add(declaration);
                }
            }

            if (chain.Count == 0)
                return null;

            // chain[0] is the most derived declaration.
            var refreshOn = new HashSet<AvaloniaProperty>(chain.SelectMany(d => d.RefreshOn));
            if (AvaloniaPropertyRegistry.Instance.FindRegistered(type, "Size") is StyledProperty<DaisySize> sizeProperty)
            {
                refreshOn.Add(sizeProperty);
            }

            return new Registration
            {
                Surfaces = chain.FirstOrDefault(d => d.Surfaces.Count > 0)?.Surfaces ?? [],
                RefreshOn = refreshOn,
                IsExcluded = chain.Any(d => d.IsExcluded),
                IsSegmentContainer = chain.Any(d => d.IsSegmentContainer),
                IsShadowPassThrough = chain.Any(d => d.IsShadowPassThrough),
                EnabledByDefault = !chain.Any(d => d.IsDisabledByDefault),
                SuppressEffect = chain.Any(d => d.SuppressEffect)
            };
        }

        private static bool IsInsideSegmentContainer(Control control)
        {
            foreach (var ancestor in control.GetVisualAncestors())
            {
                if (ancestor is not Control c || GetRegistration(c.GetType()) is not { } registration)
                    continue;

                if (registration.IsSegmentContainer)
                    return true;

                // The nearest registered surface is the child's own context; stop there.
                if (registration.DrawsEffect)
                    return false;
            }

            return false;
        }

        #endregion

        #region Refresh

        /// <summary>
        /// Re-applies the effect to a control, e.g. after it replaced its surface border in code.
        /// </summary>
        public static void Refresh(Control control) => QueueRefresh(control);

        /// <summary>
        /// Re-applies the effect to all loaded registered controls.
        /// </summary>
        public static void RefreshAll()
        {
            foreach (var control in _tracked.Keys)
            {
                QueueRefresh(control);
            }
        }

        private static void OnControlLoaded(Control control, RoutedEventArgs e)
        {
            if (_tracked.ContainsKey(control) || !IsRegistered(control.GetType()))
                return;

            _tracked[control] = new TrackedControl();
            control.PropertyChanged += OnTrackedControlPropertyChanged;
            QueueRefresh(control);
        }

        private static void OnControlUnloaded(Control control, RoutedEventArgs e)
        {
            if (!_tracked.Remove(control, out var state))
                return;

            control.PropertyChanged -= OnTrackedControlPropertyChanged;
            control.LayoutUpdated -= OnRetryLayoutUpdated;
            _pending.Remove(control);
            ClearAppliedValues(state);
        }

        private static void OnTemplateApplied(TemplatedControl control, TemplateAppliedEventArgs e)
        {
            if (!IsRegistered(control.GetType()))
                return;

            _templateNameScopes.AddOrUpdate(control, e.NameScope);

            if (!_tracked.TryGetValue(control, out var state))
                return;

            ClearAppliedValues(state);
            state.LayoutRetryUsed = false;
            QueueRefresh(control);
        }

        private static void OnTrackedControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (sender is Control control && GetRegistration(control.GetType()) is { } registration && registration.RefreshOn.Contains(e.Property))
            {
                QueueRefresh(control);
            }
        }

        private static void OnSettingChanged(Control control, AvaloniaPropertyChangedEventArgs e) => QueueRefresh(control);

        private static void OnScopeChanged(Control scopeRoot, AvaloniaPropertyChangedEventArgs e)
        {
            foreach (var control in _tracked.Keys)
            {
                if (ReferenceEquals(control, scopeRoot) || scopeRoot.IsVisualAncestorOf(control))
                {
                    QueueRefresh(control);
                }
            }
        }

        private static void OnRetryLayoutUpdated(object? sender, EventArgs e)
        {
            if (sender is not Control control)
                return;

            control.LayoutUpdated -= OnRetryLayoutUpdated;
            QueueRefresh(control);
        }

        private static void QueueRefresh(Control control)
        {
            if (!_tracked.ContainsKey(control) || !_pending.Add(control) || _refreshPosted)
                return;

            _refreshPosted = true;
            Dispatcher.UIThread.Post(ProcessPending, DispatcherPriority.Loaded);
        }

        private static void ProcessPending()
        {
            _refreshPosted = false;
            if (_pending.Count == 0)
                return;

            var batch = _pending.ToArray();
            _pending.Clear();

            foreach (var control in batch)
            {
                Apply(control);
            }
        }

        private static void Apply(Control control)
        {
            if (!_tracked.TryGetValue(control, out var state))
                return;

            ClearAppliedValues(state);

            if (GetRegistration(control.GetType()) is not { DrawsEffect: true } registration || !IsEffectivelyEnabled(control))
                return;

            var settings = new ShadowSettings(
                GetEffectiveMode(control),
                GetEffectiveIntensity(control),
                GetEffectiveElevation(control),
                GetEffectiveDarkShadowColor(control),
                GetEffectiveLightShadowColor(control));
            var surfaceMissing = false;

            foreach (var surface in registration.Surfaces)
            {
                surfaceMissing |= !ApplySurface(control, state, surface, settings);
            }

            if (registration.SuppressEffect && control.SetValue(Visual.EffectProperty, null, BindingPriority.Animation) is { } suppressed)
            {
                state.AppliedValues.Add(suppressed);
            }

            // Surfaces built in code may appear after the first layout pass; retry once per template.
            if (surfaceMissing && !state.LayoutRetryUsed)
            {
                state.LayoutRetryUsed = true;
                control.LayoutUpdated += OnRetryLayoutUpdated;
            }
        }

        /// <summary>
        /// Draws one surface. Returns false only when the surface border does not exist yet;
        /// an inactive surface counts as applied.
        /// </summary>
        private static bool ApplySurface(Control control, TrackedControl state, Surface surface, ShadowSettings settings)
        {
            if (surface.IsActive?.Invoke(control) == false)
                return true;

            if (surface.Resolve(control) is not { } host)
                return false;

            if (surface.RequiresBackground)
            {
                state.AppliedValues.Add(new BackgroundWatch(host, control));
                if (!HasVisibleBackground(host))
                    return true;
            }

            var mode = surface.FixedMode ?? settings.Mode;
            var layer = mode == DaisyNeumorphicMode.Inset
                ? surface.InsetOverlay?.Invoke(control)
                : surface.OuterShadowLayer?.Invoke(control);
            var target = layer ?? host;
            var (offset, blur) = DaisyNeumorphicHelper.GetShadowMetrics(
                settings.Elevation,
                mode,
                GetOffset(control),
                control.IsSet(OffsetProperty),
                GetBlurRadius(control),
                control.IsSet(BlurRadiusProperty));
            var shadows = DaisyNeumorphicHelper.CreateShadows(mode, settings.Intensity, offset, blur, settings.DarkColor, settings.LightColor);

            if (target.SetValue(Border.BoxShadowProperty, shadows, BindingPriority.Animation) is { } applied)
            {
                state.AppliedValues.Add(applied);
            }

            UnclipToControl(target, control, state);
            return true;
        }

        private static bool HasVisibleBackground(Border border) =>
            border.Background is { } brush && brush is not ISolidColorBrush { Color.A: 0 };

        /// <summary>
        /// Templated controls clip to their bounds by default, which also cuts off outer shadows drawn inside
        /// them. While the effect is drawn, this lifts that default clip on the elements between the shadow
        /// border and the control, and on up to the nearest shadow pass-through container if there is one.
        /// Clips set explicitly by a theme, style or code stay in place, and nothing else outside the control
        /// is touched.
        /// </summary>
        private static void UnclipToControl(Visual target, Control control, TrackedControl state)
        {
            if (!control.IsVisualAncestorOf(target))
                return;

            var last = FindShadowPassThrough(control) ?? control;
            for (var current = target.GetVisualParent(); current != null; current = current.GetVisualParent())
            {
                LiftDefaultClip(current, state);

                if (ReferenceEquals(current, last))
                    break;
            }
        }

        /// <summary>
        /// Finds the nearest ancestor registered with <see cref="RegisterShadowPassThrough{TControl}"/>,
        /// stopping at the nearest ancestor that draws the effect itself.
        /// </summary>
        private static Control? FindShadowPassThrough(Control control)
        {
            foreach (var ancestor in control.GetVisualAncestors())
            {
                if (ancestor is not Control c || GetRegistration(c.GetType()) is not { } registration)
                    continue;

                if (registration.IsShadowPassThrough)
                    return c;

                if (registration.DrawsEffect)
                    return null;
            }

            return null;
        }

        /// <summary>
        /// Lifts a clip that only comes from the property's default. A clip with a base value (theme, style,
        /// template or code) is explicit and stays. Every drawing control holds its own value, so the clip
        /// returns only when the last of them turns the effect off.
        /// </summary>
        private static void LiftDefaultClip(Visual visual, TrackedControl state)
        {
            if (visual.GetBaseValue(Visual.ClipToBoundsProperty).HasValue
                || !Visual.ClipToBoundsProperty.GetDefaultValue(visual.GetType()))
                return;

            if (visual.SetValue(Visual.ClipToBoundsProperty, false, BindingPriority.Animation) is { } applied)
            {
                state.AppliedValues.Add(applied);
            }
        }

        private static void ClearAppliedValues(TrackedControl state)
        {
            foreach (var applied in state.AppliedValues)
            {
                applied.Dispose();
            }

            state.AppliedValues.Clear();
        }

        #endregion

        #region Template Parts

        /// <summary>
        /// Finds a named border in a control's template.
        /// </summary>
        internal static Border? TemplatePart(Control control, string name)
        {
            if (control is not TemplatedControl templated)
                return null;

            return _templateNameScopes.TryGetValue(templated, out var nameScope)
                ? nameScope.Find(name) as Border
                : templated.GetTemplateDescendants().OfType<Border>().FirstOrDefault(b => b.Name == name);
        }

        #endregion

        #region Theme Integration

        private static void OnThemeChanged(object? sender, string themeName)
        {
            _themeShadowColors = null;
            RefreshAll();
        }

        /// <summary>
        /// Shadow colors derived from the current theme: the dark shadow is the theme's Base300 color
        /// darkened (stronger for dark themes), the light shadow is white.
        /// </summary>
        private static (Color Dark, Color Light) GetThemeShadowColors()
        {
            if (_themeShadowColors is { } cached)
                return cached;

            var base300 = Color.FromRgb(128, 128, 128);
            if (Application.Current?.TryGetResource("DaisyBase300Color", null, out var value) == true && value is Color color)
            {
                base300 = color;
            }

            var colors = (DaisyNeumorphicHelper.Darken(base300, DaisyThemeManager.IsCurrentThemeDark ? 0.7 : 0.5), Colors.White);
            _themeShadowColors = colors;
            return colors;
        }

        #endregion
    }
}
