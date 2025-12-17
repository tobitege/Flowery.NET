using Avalonia;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using System;
using System.Globalization;

namespace Flowery.Localization
{
    /// <summary>
    /// Abstract base class for localization markup extensions.
    /// Provides the core implementation for XAML localization with runtime culture switching.
    /// Consuming applications should inherit from this class and provide their localization source.
    /// </summary>
    /// <example>
    /// <code>
    /// // In your application, create a concrete implementation:
    /// public class LocalizeExtension : LocalizeExtensionBase
    /// {
    ///     protected override string GetLocalizedString(string key) 
    ///         => MyAppLocalization.GetString(key);
    ///     
    ///     protected override void SubscribeToCultureChanged(EventHandler&lt;CultureInfo&gt; handler)
    ///         => MyAppLocalization.CultureChanged += handler;
    /// }
    /// 
    /// // Then use in XAML:
    /// // xmlns:loc="clr-namespace:MyApp.Localization"
    /// // &lt;TextBlock Text="{loc:Localize Button_Save}"/&gt;
    /// </code>
    /// </example>
    public abstract class LocalizeExtensionBase : MarkupExtension
    {
        /// <summary>
        /// The resource key to look up in the localization source.
        /// </summary>
        [ConstructorArgument("key")]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the LocalizeExtensionBase class.
        /// </summary>
        protected LocalizeExtensionBase()
        {
        }

        /// <summary>
        /// Initializes a new instance of the LocalizeExtensionBase class with the specified key.
        /// </summary>
        /// <param name="key">The resource key to localize.</param>
        protected LocalizeExtensionBase(string key)
        {
            Key = key;
        }

        /// <summary>
        /// Gets the localized string for the specified key from the application's localization source.
        /// </summary>
        /// <param name="key">The resource key.</param>
        /// <returns>The localized string, or a fallback value if not found.</returns>
        protected abstract string GetLocalizedString(string key);

        /// <summary>
        /// Subscribes to the culture changed event from the application's localization source.
        /// The handler will be called whenever the UI culture changes.
        /// </summary>
        /// <param name="handler">The event handler to subscribe.</param>
        protected abstract void SubscribeToCultureChanged(EventHandler<CultureInfo> handler);

        /// <summary>
        /// Gets the indexer source object for binding fallback.
        /// Override this to return your localization singleton instance.
        /// Default returns null (no indexer binding fallback).
        /// </summary>
        protected virtual object? GetBindingSource() => null;

        /// <summary>
        /// Provides the localized value for the target property.
        /// Automatically subscribes to culture changes to update the value at runtime.
        /// </summary>
        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
                return "[Missing Key]";

            // Get the target property we're binding to
            var provideValueTarget = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;

            if (provideValueTarget?.TargetObject is AvaloniaObject targetObject &&
                provideValueTarget.TargetProperty is AvaloniaProperty targetProperty)
            {
                var initialValue = GetLocalizedString(Key);

                // Subscribe to culture changes to update the property
                SubscribeToCultureChanged((s, culture) =>
                {
                    targetObject.SetValue(targetProperty, GetLocalizedString(Key));
                });

                return initialValue;
            }

            // Fallback to binding if we can't get the target
            var bindingSource = GetBindingSource();
            if (bindingSource != null)
            {
                return new Binding($"[{Key}]")
                {
                    Source = bindingSource,
                    Mode = BindingMode.OneWay
                };
            }

            // Final fallback: just return the localized string
            return GetLocalizedString(Key);
        }
    }
}
