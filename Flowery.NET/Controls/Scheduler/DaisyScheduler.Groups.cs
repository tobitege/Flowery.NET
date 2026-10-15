using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Flowery.Controls.Scheduler.Models;

namespace Flowery.Controls;

public partial class DaisyScheduler
{
    public static readonly StyledProperty<ResourceKind?> ResourceKindFilterProperty =
        AvaloniaProperty.Register<DaisyScheduler, ResourceKind?>(nameof(ResourceKindFilter), null,
            validate: static value => value is null || Enum.IsDefined(value.Value));

    /// <summary>Limits resource lanes to this kind. Null shows every kind.</summary>
    public ResourceKind? ResourceKindFilter
    {
        get => GetValue(ResourceKindFilterProperty);
        set => SetValue(ResourceKindFilterProperty, value);
    }

    public static readonly StyledProperty<IReadOnlyList<ResourceGroupModel>?> ResourceGroupsProperty =
        AvaloniaProperty.Register<DaisyScheduler, IReadOnlyList<ResourceGroupModel>?>(nameof(ResourceGroups), null);

    /// <summary>Named, collapsible groups in the horizontal Timeline day view. Reassign after changing models.</summary>
    public IReadOnlyList<ResourceGroupModel>? ResourceGroups
    {
        get => GetValue(ResourceGroupsProperty);
        set => SetValue(ResourceGroupsProperty, value);
    }

    public static readonly StyledProperty<IReadOnlyList<object>?> CollapsedResourceGroupIdsProperty =
        AvaloniaProperty.Register<DaisyScheduler, IReadOnlyList<object>?>(nameof(CollapsedResourceGroupIds), null,
            defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Collapsed group ids. Null/empty opens all groups. Group input models are never changed by toggling.</summary>
    public IReadOnlyList<object>? CollapsedResourceGroupIds
    {
        get => GetValue(CollapsedResourceGroupIdsProperty);
        set => SetValue(CollapsedResourceGroupIdsProperty, value);
    }

    private sealed record TimelineGroup(string? Name, IReadOnlyList<ResourceModel> Resources,
        DateTime? Date = null, ResourceGroupModel? Model = null);

    private IEnumerable<TimelineGroup> TimelineGroups(IReadOnlyList<ResourceModel> resources, DateTime start)
    {
        if (CurrentView == SchedulerView.Timeline && ResourceGroups is { Count: > 0 } groups)
        {
            foreach (var group in groups)
                yield return new TimelineGroup(group.Name, resources.Where(group.Matches).ToArray(), Model: group);
        }
        else if (CurrentView == SchedulerView.Planner && GroupMode == SchedulerGroupMode.ByDay)
        {
            for (var day = 0; day < 7; day++)
                yield return new TimelineGroup(start.AddDays(day).ToString("D", Culture), resources, start.AddDays(day));
        }
        else
            yield return new TimelineGroup(null, resources);
    }

    private Control GroupHeading(TimelineGroup group)
    {
        if (group.Model is not { } model)
            return Label(group.Name ?? string.Empty);
        var collapsed = CollapsedResourceGroupIds?.Contains(model.Id) == true;
        var button = new DaisyButton
        {
            Content = (collapsed ? "▸ " : "▾ ") + group.Name + " (" + group.Resources.Count.ToString(Culture) + ")",
            Variant = DaisyButtonVariant.Ghost, Size = Size, Tag = model, HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left
        };
        AutomationProperties.SetName(button, model.Name);
        button.Click += OnResourceGroupToggle;
        return button;
    }

    private void OnResourceGroupToggle(object? sender, RoutedEventArgs e)
    {
        if (sender is not DaisyButton { Tag: ResourceGroupModel group })
            return;
        var collapsed = (CollapsedResourceGroupIds ?? Array.Empty<object>()).ToList();
        if (!collapsed.Remove(group.Id))
            collapsed.Add(group.Id);
        SetCurrentValue(CollapsedResourceGroupIdsProperty, collapsed.ToArray());
    }
}
