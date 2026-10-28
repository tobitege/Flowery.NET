using System;
using System.Collections.Generic;
using Avalonia;
using Flowery.Effects;

namespace Flowery.NET.Gallery.Effects
{
    public sealed class CarouselGlTransitionHost : CarouselGlNativeHost
    {
        // Avalonia resources: Android and the browser have no application files.
        private static readonly IReadOnlyDictionary<string, string> Images = new Dictionary<string, string>
        {
            ["from"] = "avares://Flowery.NET.Gallery/Assets/wallpaper_blue.jpg",
            ["to"] = "avares://Flowery.NET.Gallery/Assets/wallpaper_green.jpg"
        };
        private static readonly Lazy<IReadOnlyDictionary<string, string>> Shaders = new(LoadShaders);

        public static readonly StyledProperty<string> TransitionNameProperty =
            AvaloniaProperty.Register<CarouselGlTransitionHost, string>(
                nameof(TransitionName),
                string.Empty);

        public static readonly StyledProperty<double> IntervalSecondsProperty =
            AvaloniaProperty.Register<CarouselGlTransitionHost, double>(
                nameof(IntervalSeconds),
                0d);

        public static readonly StyledProperty<double> TransitionSecondsProperty =
            AvaloniaProperty.Register<CarouselGlTransitionHost, double>(
                nameof(TransitionSeconds),
                0.6d);

        // The property metadata callbacks of WinUI: class handlers of the changes.
        static CarouselGlTransitionHost()
        {
            TransitionNameProperty.Changed.AddClassHandler<CarouselGlTransitionHost>((sender, args) => OnParametersChanged(sender, args));
            IntervalSecondsProperty.Changed.AddClassHandler<CarouselGlTransitionHost>((sender, args) => OnParametersChanged(sender, args));
            TransitionSecondsProperty.Changed.AddClassHandler<CarouselGlTransitionHost>((sender, args) => OnParametersChanged(sender, args));
        }

        public string TransitionName
        {
            get => GetValue(TransitionNameProperty);
            set => SetValue(TransitionNameProperty, value);
        }

        public double IntervalSeconds
        {
            get => GetValue(IntervalSecondsProperty);
            set => SetValue(IntervalSecondsProperty, value);
        }

        public double TransitionSeconds
        {
            get => GetValue(TransitionSecondsProperty);
            set => SetValue(TransitionSecondsProperty, value);
        }

        public static IReadOnlyDictionary<string, string> AvailableShaders => Shaders.Value;
        protected override IReadOnlyDictionary<string, string> ShaderSources => Shaders.Value;

        // EngineScript: the engine object that ports flowery-carousel-gl-transitions.js.
        protected override CarouselGlEngine CreateEngine() => new FloweryCarouselGLTransitions();

        protected override CarouselGlConfiguration? CreateConfiguration() => string.IsNullOrWhiteSpace(TransitionName)
            ? null
            : new CarouselGlConfiguration("transitions", "transition", TransitionName, null, Images, IntervalSeconds, TransitionSeconds);

        private static void OnParametersChanged(CarouselGlTransitionHost sender, AvaloniaPropertyChangedEventArgs args) =>
            sender.RefreshRenderer();

        private static IReadOnlyDictionary<string, string> LoadShaders()
        {
            var assembly = typeof(CarouselGlTransitionHost).Assembly;
            const string prefix = "Flowery.GL.Shaders.";
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var resource in assembly.GetManifestResourceNames())
            {
                if (!resource.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var name = resource[prefix.Length..];
                if (name.EndsWith(".sksl", StringComparison.Ordinal)) name = name[..^5];
                result.Add(name, ReadEmbeddedText(assembly, resource));
            }
            if (result.Count != 100) throw new InvalidOperationException("The complete GL transition catalog requires 100 shaders.");
            return result;
        }
    }
}
