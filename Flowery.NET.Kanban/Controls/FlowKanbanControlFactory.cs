namespace Flowery.NET.Kanban.Controls;

internal enum FlowKanbanTextRole
{
    Title,
    Body,
    Caption
}

internal static class FlowKanbanControlFactory
{
    internal static void AddTab(DaisyTabs tabs, string header, Control content)
    {
        tabs.Items.Add(new TabItem
        {
            Header = header,
            Content = content
        });
    }

    internal static void SetTextAreaRows(DaisyTextArea textArea, int minimumRows, int maximumRows)
    {
        const double rowHeight = 22;
        const double verticalChrome = 16;
        textArea.MinHeight = Math.Max(1, minimumRows) * rowHeight + verticalChrome;
        textArea.MaxHeight = Math.Max(minimumRows, maximumRows) * rowHeight + verticalChrome;
    }

    internal static TextBlock CreateTextBlock(string text, FlowKanbanTextRole role)
    {
        return new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush"),
            FontSize = role switch
            {
                FlowKanbanTextRole.Title => 20,
                FlowKanbanTextRole.Caption => 12,
                _ => 14
            },
            FontWeight = role == FlowKanbanTextRole.Title ? FontWeight.SemiBold : FontWeight.Normal
        };
    }

    internal static StreamGeometry? GetIconGeometry(string resourceKey)
    {
        var pathData = FloweryPathHelpers.GetIconPathData(resourceKey);
        return string.IsNullOrWhiteSpace(pathData) ? null : StreamGeometry.Parse(pathData);
    }
}
