namespace Flowery.NET.Kanban.Controls;

internal static class FlowKanbanDispatcher
{
    internal static void Post(Action action)
    {
        Dispatcher.UIThread.Post(action);
    }

    internal static void RunOrPost(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }

    internal static async Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(action);
    }

    internal static async Task<T> InvokeAsync<T>(Func<T> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        if (Dispatcher.UIThread.CheckAccess())
            return function();

        return await Dispatcher.UIThread.InvokeAsync(function);
    }

    internal static DispatcherTimer CreateTimer(EventHandler tickHandler)
    {
        var timer = new DispatcherTimer();
        timer.Tick += tickHandler;
        return timer;
    }
}
