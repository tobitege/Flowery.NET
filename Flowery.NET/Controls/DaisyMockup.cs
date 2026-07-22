using System;
using Avalonia;
using Avalonia.Controls;

namespace Flowery.Controls
{
    public enum DaisyMockupVariant
    {
        Code,
        Window,
        Browser
    }

    public class DaisyMockup : ContentControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyMockup);

        public static readonly StyledProperty<DaisyMockupVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyMockup, DaisyMockupVariant>(nameof(Variant), DaisyMockupVariant.Code);

        public static readonly StyledProperty<string> UrlProperty =
            AvaloniaProperty.Register<DaisyMockup, string>(nameof(Url), "https://daisyui.com");

        public DaisyMockupVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public string Url
        {
            get => GetValue(UrlProperty);
            set => SetValue(UrlProperty, value);
        }
    }
}
