using Avalonia;
using Avalonia.Controls;

namespace Flowery.Controls
{
    internal static class DaisyTokenResourceExtensions
    {
        public static string ToTokenSizeKey(this DaisySize size) => size.ToString();

        public static T GetResourceOrDefault<T>(this Control control, string key, T fallback)
        {
            if (control.TryFindResource(key, out var resource))
            {
                if (resource is T typed)
                    return typed;
            }

            return fallback;
        }
    }
}

