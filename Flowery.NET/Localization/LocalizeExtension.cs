using Avalonia;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using System;

namespace Flowery.Localization
{
    /// <summary>
    /// Markup extension for localizing strings in XAML.
    /// Returns the localized string directly and updates when culture changes.
    /// Usage: {loc:Localize Button_Generate}
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
                var initialValue = FloweryLocalization.GetString(Key);
                
                // Subscribe to culture changes to update the property
                FloweryLocalization.CultureChanged += (s, culture) =>
                {
                    targetObject.SetValue(targetProperty, FloweryLocalization.GetString(Key));
                };
                
                return initialValue;
            }

            // Fallback to binding if we can't get the target
            return new Binding($"[{Key}]")
            {
                Source = FloweryLocalization.Instance,
                Mode = BindingMode.OneWay
            };
        }
    }
}
