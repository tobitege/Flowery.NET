using System;
using System.Globalization;

namespace Flowery.Localization
{
    /// <summary>
    /// Markup extension for localizing Flowery.NET library strings in XAML.
    /// Returns the localized string directly and updates when culture changes.
    /// Usage: {loc:Localize Button_Generate}
    /// </summary>
    /// <remarks>
    /// This is the library's own LocalizeExtension for Flowery.NET internal strings.
    /// For application-specific localization, inherit from <see cref="LocalizeExtensionBase"/>
    /// and plug in your own localization source.
    /// </remarks>
    public class LocalizeExtension : LocalizeExtensionBase
    {
        /// <summary>
        /// Initializes a new instance of the LocalizeExtension class.
        /// </summary>
        public LocalizeExtension()
        {
        }

        /// <summary>
        /// Initializes a new instance of the LocalizeExtension class with the specified key.
        /// </summary>
        /// <param name="key">The resource key to localize.</param>
        public LocalizeExtension(string key) : base(key)
        {
        }

        /// <inheritdoc/>
        protected override string GetLocalizedString(string key)
            => FloweryLocalization.GetString(key);

        /// <inheritdoc/>
        protected override void SubscribeToCultureChanged(EventHandler<CultureInfo> handler)
            => FloweryLocalization.CultureChanged += handler;

        /// <inheritdoc/>
        protected override object? GetBindingSource()
            => FloweryLocalization.Instance;
    }
}
