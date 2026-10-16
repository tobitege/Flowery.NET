<!-- Supplementary documentation for DaisyBreadcrumbBar -->
<!-- This content is merged into auto-generated docs by generate_docs.py -->

# Overview

DaisyBreadcrumbBar shows a breadcrumb trail built from an `ItemsSource`, with chevrons between the items and the last item marked as the current location. When the items do not fit, the leading items collapse into an ellipsis button whose menu lists them; the last item always stays visible. It is the Avalonia counterpart of the `BreadcrumbBar`-based control in Flowery.WinUI and Flowery.Uno and has the same properties and event.

## When to Use (vs DaisyBreadcrumbs)

- Use `DaisyBreadcrumbBar` when you have the path as data (`ItemsSource`) and want the overflow ellipsis and keyboard support of a breadcrumb bar.
- Use `DaisyBreadcrumbs` for hand-written `DaisyBreadcrumbItem` children, icons per item, custom separators, or commands.

## Properties

| Property | Description |
| -------- | ----------- |
| `ItemsSource` (IEnumerable) | Path segments from the root to the current item. `INotifyCollectionChanged` sources update the bar. |
| `ItemTemplate` (IDataTemplate) | Optional template for each item, also used in the ellipsis menu. Without it the item's text is shown. |
| `Size` (`DaisySize`, default Medium) | Font size from the `DaisySize*FontSize` tokens. Follows the global size when not set. |
| `ItemClicked` (event) | Raised when an item is clicked in the bar or the ellipsis menu, with `Index` and `Item`. |

Each segment is a `DaisyBreadcrumbBarItem` (a `Button`) with `IsCurrent` and `IsEllipsis`, so its look can be changed with a style or control theme for that type. The ellipsis button is announced with the localized text `BreadcrumbBar_More` ("More").

## Quick Examples

```xml
<controls:DaisyBreadcrumbBar ItemsSource="{Binding Path}" ItemClicked="OnBreadcrumbClicked" />
<controls:DaisyBreadcrumbBar ItemsSource="{Binding Path}" Size="Small" />
```

```csharp
private void OnBreadcrumbClicked(object? sender, DaisyBreadcrumbBarItemClickedEventArgs e)
{
    // Navigate up to the clicked level.
    while (Path.Count > e.Index + 1)
        Path.RemoveAt(Path.Count - 1);
}
```

## Tips & Best Practices

- Use an `ObservableCollection` for the path so navigation updates the bar.
- Give the bar a bounded width (or place it in a stretched layout) so the overflow ellipsis can kick in.
- For manual breadcrumb items or custom separators, switch to `DaisyBreadcrumbs`.
