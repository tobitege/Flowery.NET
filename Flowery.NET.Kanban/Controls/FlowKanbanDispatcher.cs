namespace Flowery.NET.Kanban.Controls;

internal static class FlowKanbanDispatcher
{
    internal static void Post(Action action)
    {
        Dispatcher.UIThread.Post(action);
    }

    // The methods below are called from other threads as well. They take the dispatcher of the control instead of
    // reading Dispatcher.UIThread there: between isolated headless test sessions, the thread that reads it first owns
    // the next UI dispatcher.
    internal static void RunOrPost(Dispatcher dispatcher, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Post(action);
    }

    internal static async Task InvokeAsync(Dispatcher dispatcher, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        await dispatcher.InvokeAsync(action);
    }

    internal static async Task<T> InvokeAsync<T>(Dispatcher dispatcher, Func<T> function)
    {
        ArgumentNullException.ThrowIfNull(function);
        if (dispatcher.CheckAccess())
            return function();

        return await dispatcher.InvokeAsync(function);
    }

    internal static DispatcherTimer CreateTimer(EventHandler tickHandler)
    {
        var timer = new DispatcherTimer();
        timer.Tick += tickHandler;
        return timer;
    }
}
