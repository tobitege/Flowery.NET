namespace Flowery.NET.Kanban.Controls;

internal static class FlowKanbanLayoutMetrics
{
    internal static double GetFirstFinitePositive(params double?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate is double value && value > 0 && double.IsFinite(value))
            {
                return value;
            }
        }

        return 0;
    }
}
