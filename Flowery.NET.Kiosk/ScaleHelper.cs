namespace Flowery.NET.Kiosk;

/// <summary>
/// The preset values of <see cref="FloweryScaleConfig"/> for a given scale factor, as Flowery.WinUI's ScaleHelper gives
/// them; only the presets the kiosk uses.
/// </summary>
internal static class ScaleHelper
{
    public static double GetScaledPreset(FloweryScalePreset preset, double scaleFactor)
    {
        var (baseValue, minValue) = FloweryScaleConfig.GetPresetValues(preset);
        return minValue.HasValue
            ? FloweryScaleManager.ApplyScale(baseValue, minValue.Value, scaleFactor)
            : FloweryScaleManager.ApplyScale(baseValue, scaleFactor);
    }

    public static Thickness GetScaledThickness(FloweryScalePreset preset, double scaleFactor) =>
        new(GetScaledPreset(preset, scaleFactor));

    public static double FontSubheading(double scaleFactor) => GetScaledPreset(FloweryScalePreset.FontSubheading, scaleFactor);
    public static double SpacingSmall(double scaleFactor) => GetScaledPreset(FloweryScalePreset.SpacingSmall, scaleFactor);
    public static double SpacingXS(double scaleFactor) => GetScaledPreset(FloweryScalePreset.SpacingXS, scaleFactor);
}
