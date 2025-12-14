using Avalonia;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using System;

namespace Flowery.NET.Gallery.Localization
{
    /// <summary>
    /// Markup extension for localizing Gallery strings in XAML.
    /// Returns the localized string directly and updates when culture changes.
    /// </summary>
    public class LocalizeExtension : MarkupExtension
    {
        [ConstructorArgument("key")]
        public string Key { get; set; } = string.Empty;

        public LocalizeExtension()
        {
        }

        public LocalizeExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
                return "[Missing Key]";

            // Get the target property we're binding to
            var provideValueTarget = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
            
            if (provideValueTarget?.TargetObject is AvaloniaObject targetObject && 
                provideValueTarget.TargetProperty is AvaloniaProperty targetProperty)
            {
                var initialValue = GalleryLocalization.GetString(Key);
                
                // Subscribe to culture changes to update the property
                GalleryLocalization.CultureChanged += (s, culture) =>
                {
                    targetObject.SetValue(targetProperty, GalleryLocalization.GetString(Key));
                };
                
                return initialValue;
            }

            // Fallback to binding if we can't get the target
            return new Binding($"[{Key}]")
            {
                Source = GalleryLocalization.Instance,
                Mode = BindingMode.OneWay
            };
        }
    }
}
