namespace Flowery.NET.Kanban.Controls;

internal static class FlowKanbanResources
{
    internal static double GetCardSpacing(DaisySize size) => size switch
    {
        DaisySize.ExtraSmall => 4,
        DaisySize.Small => 6,
        DaisySize.Medium => 8,
        DaisySize.Large => 10,
        DaisySize.ExtraLarge => 12,
        _ => 8
    };

    internal static Thickness GetCardPadding(DaisySize size) => size switch
    {
        DaisySize.ExtraSmall => new Thickness(6),
        DaisySize.Small => new Thickness(8),
        DaisySize.Medium => new Thickness(12),
        DaisySize.Large => new Thickness(14),
        DaisySize.ExtraLarge => new Thickness(16),
        _ => new Thickness(12)
    };

    internal static Thickness GetColumnPadding(DaisySize size) => size switch
    {
        DaisySize.ExtraSmall => new Thickness(6),
        DaisySize.Small => new Thickness(8),
        DaisySize.Medium => new Thickness(12),
        DaisySize.Large => new Thickness(14),
        DaisySize.ExtraLarge => new Thickness(16),
        _ => new Thickness(12)
    };

    internal static double GetCardTitleFontSize(DaisySize size) => size switch
    {
        DaisySize.ExtraSmall => 10,
        DaisySize.Small => 12,
        DaisySize.Medium => 14,
        DaisySize.Large => 16,
        DaisySize.ExtraLarge => 18,
        _ => 14
    };

    internal static double GetColumnHeaderFontSize(DaisySize size) => size switch
    {
        DaisySize.ExtraSmall => 12,
        DaisySize.Small => 14,
        DaisySize.Medium => 18,
        DaisySize.Large => 20,
        DaisySize.ExtraLarge => 24,
        _ => 18
    };

    internal static CornerRadius GetCardCornerRadius(DaisySize size) => size switch
    {
        DaisySize.ExtraSmall => new CornerRadius(4),
        DaisySize.Small => new CornerRadius(6),
        DaisySize.Medium => new CornerRadius(8),
        DaisySize.Large => new CornerRadius(10),
        DaisySize.ExtraLarge => new CornerRadius(12),
        _ => new CornerRadius(8)
    };

    internal static CornerRadius GetColumnCornerRadius(DaisySize size) => size switch
    {
        DaisySize.ExtraSmall => new CornerRadius(6),
        DaisySize.Small => new CornerRadius(8),
        DaisySize.Medium => new CornerRadius(12),
        DaisySize.Large => new CornerRadius(14),
        DaisySize.ExtraLarge => new CornerRadius(16),
        _ => new CornerRadius(12)
    };
}
