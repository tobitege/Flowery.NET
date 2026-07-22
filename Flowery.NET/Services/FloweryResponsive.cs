using System;
using Avalonia;
using Avalonia.Controls;

namespace Flowery.Services;

/// <summary>
/// Standard responsive breakpoints aligned with common CSS frameworks.
/// Use these constants to create consistent responsive behavior across your app.
/// </summary>
public static class FloweryBreakpoints
{
    /// <summary>Extra small screens (phones in portrait): 0-479px</summary>
    public const double ExtraSmall = 480;

    /// <summary>Small screens (phones in landscape, small tablets): 480-639px</summary>
    public const double Small = 640;

    /// <summary>Medium screens (tablets): 640-767px</summary>
    public const double Medium = 768;

    /// <summary>Large screens (small laptops): 768-1023px</summary>
    public const double Large = 1024;

    /// <summary>Extra large screens (desktops): 1024-1279px</summary>
    public const double ExtraLarge = 1280;

    /// <summary>2XL screens (large desktops): 1280px+</summary>
    public const double TwoXL = 1536;

    /// <summary>
    /// Gets the breakpoint name for a given width.
    /// </summary>
    public static string GetBreakpointName(double width) => width switch
    {
        < ExtraSmall => "xs",
        < Small => "sm",
        < Medium => "md",
        < Large => "lg",
        < ExtraLarge => "xl",
        _ => "2xl"
    };

    /// <summary>
    /// Checks if the width is at or above the specified breakpoint.
    /// </summary>
    public static bool IsAtLeast(double width, double breakpoint) => width >= breakpoint;

    /// <summary>
    /// Checks if the width is below the specified breakpoint.
    /// </summary>
    public static bool IsBelow(double width, double breakpoint) => width < breakpoint;
}

/// <summary>
/// Provides responsive layout functionality via attached properties.
/// Attach to a container (e.g., ScrollViewer) and child elements can bind to the calculated ResponsiveMaxWidth.
/// </summary>
/// <example>
/// XAML usage:
/// <code>
/// &lt;ScrollViewer services:FloweryResponsive.IsEnabled="True"
///               services:FloweryResponsive.BaseMaxWidth="430"&gt;
///     &lt;StackPanel MaxWidth="{Binding (services:FloweryResponsive.ResponsiveMaxWidth), 
///                                     RelativeSource={RelativeSource AncestorType=ScrollViewer}}"&gt;
///         &lt;!-- Content --&gt;
///     &lt;/StackPanel&gt;
/// &lt;/ScrollViewer&gt;
/// </code>
/// </example>
public static class FloweryResponsive
{
    /// <summary>
    /// Default padding to subtract from available width (accounts for margins, scrollbars, etc.)
    /// </summary>
    public const double DefaultPadding = 48;

    /// <summary>
    /// Minimum width to maintain usability.
    /// </summary>
    public const double MinimumWidth = 200;

    /// <summary>
    /// The base/maximum width for content when space is available.
    /// </summary>
    public static readonly AttachedProperty<double> BaseMaxWidthProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("BaseMaxWidth", typeof(FloweryResponsive), 430);

    /// <summary>
    /// Enable responsive behavior on this container.
    /// When enabled, the control will automatically calculate ResponsiveMaxWidth based on its bounds.
    /// </summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(FloweryResponsive), false);

    /// <summary>
    /// The calculated responsive MaxWidth based on available space.
    /// Bind to this property from child elements to get responsive sizing.
    /// </summary>
    public static readonly AttachedProperty<double> ResponsiveMaxWidthProperty =
        AvaloniaProperty.RegisterAttached<Control, double>("ResponsiveMaxWidth", typeof(FloweryResponsive), 430);

    /// <summary>
    /// The current breakpoint name (xs, sm, md, lg, xl, 2xl) based on control width.
    /// Useful for conditional styling or visibility.
    /// </summary>
    public static readonly AttachedProperty<string> CurrentBreakpointProperty =
        AvaloniaProperty.RegisterAttached<Control, string>("CurrentBreakpoint", typeof(FloweryResponsive), "lg");

    static FloweryResponsive()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
    }

    public static double GetBaseMaxWidth(Control element) => element.GetValue(BaseMaxWidthProperty);
    public static void SetBaseMaxWidth(Control element, double value) => element.SetValue(BaseMaxWidthProperty, value);

    public static bool GetIsEnabled(Control element) => element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(Control element, bool value) => element.SetValue(IsEnabledProperty, value);

    public static double GetResponsiveMaxWidth(Control element) => element.GetValue(ResponsiveMaxWidthProperty);
    public static void SetResponsiveMaxWidth(Control element, double value) => element.SetValue(ResponsiveMaxWidthProperty, value);

    public static string GetCurrentBreakpoint(Control element) => element.GetValue(CurrentBreakpointProperty);
    public static void SetCurrentBreakpoint(Control element, string value) => element.SetValue(CurrentBreakpointProperty, value);

    private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            control.PropertyChanged += OnControlPropertyChanged;
            UpdateResponsiveProperties(control);
        }
        else
        {
            control.PropertyChanged -= OnControlPropertyChanged;
        }
    }

    private static void OnControlPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is Control control && e.Property == Visual.BoundsProperty)
        {
            UpdateResponsiveProperties(control);
        }
    }

    private static void UpdateResponsiveProperties(Control control)
    {
        var bounds = control.Bounds;
        var baseMaxWidth = GetBaseMaxWidth(control);
        var availableWidth = bounds.Width - DefaultPadding;

        // Calculate responsive width: use available width if smaller than base, otherwise use base
        var responsiveWidth = availableWidth > 0
            ? Math.Min(baseMaxWidth, Math.Max(MinimumWidth, availableWidth))
            : baseMaxWidth;

        SetResponsiveMaxWidth(control, responsiveWidth);
        SetCurrentBreakpoint(control, FloweryBreakpoints.GetBreakpointName(bounds.Width));
    }
}
