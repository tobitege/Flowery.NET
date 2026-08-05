namespace Flowery.NET.Kanban.Controls;

internal static class FlowKanbanDispatcher
{
    internal static void Post(Action action)
    {
        Dispatcher.UIThread.Post(action);
    }

    internal static async Task InvokeAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(action);
    }

    internal static DispatcherTimer CreateTimer(EventHandler tickHandler)
    {
        var timer = new DispatcherTimer();
        timer.Tick += tickHandler;
        return timer;
    }
}
