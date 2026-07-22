using System;
using Flowery.Capture.Platforms;
using Microsoft.Extensions.DependencyInjection;

namespace Flowery.Capture.Extensions;

/// <summary>
/// Extension methods for registering screen capture services with DI.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the appropriate screen capture service for the current platform.
    /// Windows uses GDI+ for high-quality capture; other platforms use CLI tools or RTB fallback.
    /// </summary>
    public static IServiceCollection AddScreenCapture(this IServiceCollection services)
    {
#if WINDOWS
        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<IScreenCaptureService, WindowsScreenCapture>();
            return services;
        }
#endif
        if (OperatingSystem.IsMacOS())
        {
            services.AddSingleton<IScreenCaptureService, MacOSScreenCapture>();
            return services;
        }

        if (OperatingSystem.IsLinux())
        {
            services.AddSingleton<IScreenCaptureService, LinuxScreenCapture>();
            return services;
        }

        services.AddSingleton<IScreenCaptureService, FallbackScreenCapture>();
        return services;
    }

    /// <summary>
    /// Adds a specific screen capture service implementation.
    /// </summary>
    public static IServiceCollection AddScreenCapture<TImplementation>(this IServiceCollection services)
        where TImplementation : class, IScreenCaptureService
    {
        services.AddSingleton<IScreenCaptureService, TImplementation>();
        return services;
    }

    /// <summary>
    /// Adds a screen capture service using a factory function.
    /// </summary>
    public static IServiceCollection AddScreenCapture(
        this IServiceCollection services,
        Func<IServiceProvider, IScreenCaptureService> factory)
    {
        services.AddSingleton(factory);
        return services;
    }
}

