using Avalonia;

namespace Flowery.NET.Kanban.Controls;

internal static class FlowKanbanProperty
{
    public static IDisposable Observe<TValue>(
        AvaloniaObject owner,
        AvaloniaProperty property,
        Action<TValue> changed)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentNullException.ThrowIfNull(changed);

        EventHandler<AvaloniaPropertyChangedEventArgs> handler = (_, args) =>
        {
            if (args.Property == property)
            {
                changed(args.GetNewValue<TValue>());
            }
        };
        owner.PropertyChanged += handler;
        return new PropertySubscription(() => owner.PropertyChanged -= handler);
    }

    private sealed class PropertySubscription(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose()
        {
            Interlocked.Exchange(ref _dispose, null)?.Invoke();
        }
    }
}
