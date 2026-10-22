using System;
using System.Diagnostics;

namespace Flowery.Services
{
    /// <summary>
    /// Raises library-wide events one handler at a time. A plain multicast invoke stops at the first handler that
    /// throws, so every control subscribed after it would miss a theme change.
    /// </summary>
    internal static class FloweryEventRaiser
    {
        internal static void Raise<TArgs>(EventHandler<TArgs>? handlers, object? sender, TArgs args, string eventName)
        {
            if (handlers is null)
                return;

            foreach (EventHandler<TArgs> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(sender, args);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[{eventName}] {handler.Method.DeclaringType?.Name}.{handler.Method.Name} failed: {ex}");
                }
            }
        }
    }
}
