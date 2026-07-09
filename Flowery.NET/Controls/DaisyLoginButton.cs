using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Flowery.Helpers;
using Flowery.Localization;

namespace Flowery.Controls
{
    public enum DaisyLoginBrand
    {
        Email,
        GitHub,
        Google,
        Facebook,
        X,
        Kakao,
        Apple,
        Amazon,
        Microsoft,
        Line,
        Slack,
        LinkedIn,
        VK,
        WeChat,
        MetaMask
    }

    /// <summary>
    /// A DaisyButton preconfigured with a brand icon, localized label and optional brand colors.
    /// </summary>
    public class DaisyLoginButton : DaisyButton
    {
        private bool _isUpdatingPresentation;
        private bool _hasExplicitIconSpacing;

        private static readonly string[] BrandResourceKeys =
        {
            "DaisyBase200Brush",
            "DaisyBase300Brush",
            "DaisyBaseContentBrush"
        };

        public static readonly StyledProperty<DaisyLoginBrand> BrandProperty =
            AvaloniaProperty.Register<DaisyLoginButton, DaisyLoginBrand>(
                nameof(Brand),
                DaisyLoginBrand.Email);

        public DaisyLoginBrand Brand
        {
            get => GetValue(BrandProperty);
            set => SetValue(BrandProperty, value);
        }

        public static readonly StyledProperty<string?> LoginTextProperty =
            AvaloniaProperty.Register<DaisyLoginButton, string?>(nameof(LoginText));

        public string? LoginText
        {
            get => GetValue(LoginTextProperty);
            set => SetValue(LoginTextProperty, value);
        }

        public static readonly StyledProperty<double> IconSizeProperty =
            AvaloniaProperty.Register<DaisyLoginButton, double>(nameof(IconSize), double.NaN);

        public double IconSize
        {
            get => GetValue(IconSizeProperty);
            set => SetValue(IconSizeProperty, value);
        }

        public static readonly StyledProperty<bool> UseBrandColorsProperty =
            AvaloniaProperty.Register<DaisyLoginButton, bool>(nameof(UseBrandColors), true);

        public bool UseBrandColors
        {
            get => GetValue(UseBrandColorsProperty);
            set => SetValue(UseBrandColorsProperty, value);
        }

        public DaisyLoginButton()
        {
            BorderThickness = new Thickness(1);
            UpdatePresentation();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            FloweryLocalization.CultureChanged += OnCultureChanged;
            UpdatePresentation();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            FloweryLocalization.CultureChanged -= OnCultureChanged;
            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IconSpacingProperty && !_isUpdatingPresentation)
                _hasExplicitIconSpacing = true;

            if (change.Property == BrandProperty ||
                change.Property == LoginTextProperty ||
                change.Property == IconSizeProperty ||
                change.Property == UseBrandColorsProperty ||
                change.Property == SizeProperty ||
                change.Property == ForegroundProperty)
            {
                UpdatePresentation();
            }
        }

        private void OnCultureChanged(object? sender, CultureInfo culture)
        {
            if (LoginText == null)
                UpdatePresentation();
        }

        private void UpdatePresentation()
        {
            if (_isUpdatingPresentation)
                return;

            _isUpdatingPresentation = true;
            try
            {
                var definition = BrandDefinitions[Brand];
                ApplyBrandResources(definition);
                IconLeft = CreateBrandIcon(definition);
                IconRight = null;
                if (!_hasExplicitIconSpacing)
                    IconSpacing = GetBrandIconSpacing();
                Content = LoginText ?? FloweryLocalization.GetStringInternal(
                    definition.DefaultTextKey,
                    definition.DefaultText);
            }
            finally
            {
                _isUpdatingPresentation = false;
            }
        }

        private void ApplyBrandResources(BrandDefinition definition)
        {
            if (!UseBrandColors)
            {
                foreach (var key in BrandResourceKeys)
                    Resources.Remove(key);

                ClearValue(BorderBrushProperty);
                return;
            }

            Resources["DaisyBase200Brush"] = new SolidColorBrush(definition.Background);
            Resources["DaisyBase300Brush"] = new SolidColorBrush(definition.HoverBackground);
            Resources["DaisyBaseContentBrush"] = new SolidColorBrush(definition.Foreground);
            BorderBrush = new SolidColorBrush(definition.Border);
        }

        private Viewbox CreateBrandIcon(BrandDefinition definition)
        {
            var iconSize = GetBrandIconSize();
            var canvas = new Canvas
            {
                Width = definition.ViewBoxWidth,
                Height = definition.ViewBoxHeight
            };

            foreach (var part in definition.Parts)
            {
                var pathData = FloweryPathHelpers.GetIconPathData(part.PathKey);
                if (pathData == null)
                    continue;

                var path = new Path
                {
                    Data = FloweryPathHelpers.ParseGeometry(pathData)
                };

                var foreground = UseBrandColors
                    ? new SolidColorBrush(definition.Foreground)
                    : Foreground;

                if (part.StrokeThickness > 0)
                {
                    path.Fill = Brushes.Transparent;
                    path.Stroke = part.Stroke.HasValue && UseBrandColors
                        ? new SolidColorBrush(part.Stroke.Value)
                        : foreground;
                    path.StrokeThickness = part.StrokeThickness;
                    path.StrokeLineCap = part.RoundCaps ? PenLineCap.Round : PenLineCap.Flat;
                    path.StrokeJoin = part.RoundJoin ? PenLineJoin.Round : PenLineJoin.Miter;
                }
                else
                {
                    path.Fill = part.Fill.HasValue && UseBrandColors
                        ? new SolidColorBrush(part.Fill.Value)
                        : foreground;
                }

                canvas.Children.Add(path);
            }

            return new Viewbox
            {
                Width = iconSize,
                Height = iconSize,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Child = canvas
            };
        }

        private double GetBrandIconSize()
        {
            if (!double.IsNaN(IconSize))
                return IconSize;

            switch (Size)
            {
                case DaisySize.ExtraSmall:
                    return 12.0;
                case DaisySize.Small:
                    return 14.0;
                case DaisySize.Large:
                    return 18.0;
                case DaisySize.ExtraLarge:
                    return 20.0;
                default:
                    return 16.0;
            }
        }

        private double GetBrandIconSpacing()
        {
            switch (Size)
            {
                case DaisySize.ExtraSmall:
                    return 4.0;
                case DaisySize.Small:
                    return 6.0;
                case DaisySize.ExtraLarge:
                    return 10.0;
                default:
                    return 8.0;
            }
        }

        private static readonly IReadOnlyDictionary<DaisyLoginBrand, BrandDefinition> BrandDefinitions =
            new Dictionary<DaisyLoginBrand, BrandDefinition>
            {
                [DaisyLoginBrand.Email] = new BrandDefinition(
                    "LoginButton_Email", "Login with Email",
                    "#FFFFFF", "#000000", "#E5E5E5",
                    24, 24,
                    new BrandIconPart("DaisyIconBrandEmailOutline", strokeThickness: 2, roundCaps: true, roundJoin: true),
                    new BrandIconPart("DaisyIconBrandEmailFlap", strokeThickness: 2, roundCaps: true, roundJoin: true)),
                [DaisyLoginBrand.GitHub] = new BrandDefinition(
                    "LoginButton_GitHub", "Login with GitHub",
                    "#000000", "#FFFFFF", "#000000",
                    24, 24,
                    new BrandIconPart("DaisyIconBrandGitHub")),
                [DaisyLoginBrand.Google] = new BrandDefinition(
                    "LoginButton_Google", "Login with Google",
                    "#FFFFFF", "#000000", "#E5E5E5",
                    512, 512,
                    new BrandIconPart("DaisyIconBrandGoogleGreen", fillHex: "#34A853"),
                    new BrandIconPart("DaisyIconBrandGoogleBlue", fillHex: "#4285F4"),
                    new BrandIconPart("DaisyIconBrandGoogleYellow", fillHex: "#FBBC02"),
                    new BrandIconPart("DaisyIconBrandGoogleRed", fillHex: "#EA4335")),
                [DaisyLoginBrand.Facebook] = new BrandDefinition(
                    "LoginButton_Facebook", "Login with Facebook",
                    "#1A77F2", "#FFFFFF", "#005FD8",
                    32, 32,
                    new BrandIconPart("DaisyIconBrandFacebook")),
                [DaisyLoginBrand.X] = new BrandDefinition(
                    "LoginButton_X", "Login with X",
                    "#000000", "#FFFFFF", "#000000",
                    300, 271,
                    new BrandIconPart("DaisyIconBrandX")),
                [DaisyLoginBrand.Kakao] = new BrandDefinition(
                    "LoginButton_Kakao", "카카오 로그인",
                    "#FEE502", "#181600", "#F1D800",
                    512, 512,
                    new BrandIconPart("DaisyIconBrandKakao")),
                [DaisyLoginBrand.Apple] = new BrandDefinition(
                    "LoginButton_Apple", "Login with Apple",
                    "#000000", "#FFFFFF", "#000000",
                    1195, 1195,
                    new BrandIconPart("DaisyIconBrandApple")),
                [DaisyLoginBrand.Amazon] = new BrandDefinition(
                    "LoginButton_Amazon", "Login with Amazon",
                    "#FF9900", "#000000", "#E17D00",
                    16, 16,
                    new BrandIconPart("DaisyIconBrandAmazon")),
                [DaisyLoginBrand.Microsoft] = new BrandDefinition(
                    "LoginButton_Microsoft", "Login with Microsoft",
                    "#2F2F2F", "#FFFFFF", "#000000",
                    512, 512,
                    new BrandIconPart("DaisyIconBrandMicrosoftRed", fillHex: "#F24F23"),
                    new BrandIconPart("DaisyIconBrandMicrosoftGreen", fillHex: "#7EBA03"),
                    new BrandIconPart("DaisyIconBrandMicrosoftBlue", fillHex: "#3CA4EF"),
                    new BrandIconPart("DaisyIconBrandMicrosoftYellow", fillHex: "#F9BA00")),
                [DaisyLoginBrand.Line] = new BrandDefinition(
                    "LoginButton_Line", "LINEでログイン",
                    "#03C755", "#FFFFFF", "#00B544",
                    16, 16,
                    new BrandIconPart("DaisyIconBrandLine")),
                [DaisyLoginBrand.Slack] = new BrandDefinition(
                    "LoginButton_Slack", "Login with Slack",
                    "#622069", "#FFFFFF", "#591660",
                    512, 512,
                    new BrandIconPart("DaisyIconBrandSlackBlue", strokeHex: "#36C5F0", strokeThickness: 78, roundCaps: true, roundJoin: true),
                    new BrandIconPart("DaisyIconBrandSlackGreen", strokeHex: "#2EB67D", strokeThickness: 78, roundCaps: true, roundJoin: true),
                    new BrandIconPart("DaisyIconBrandSlackYellow", strokeHex: "#ECB22E", strokeThickness: 78, roundCaps: true, roundJoin: true),
                    new BrandIconPart("DaisyIconBrandSlackPink", strokeHex: "#E01E5A", strokeThickness: 78, roundCaps: true, roundJoin: true)),
                [DaisyLoginBrand.LinkedIn] = new BrandDefinition(
                    "LoginButton_LinkedIn", "Login with LinkedIn",
                    "#0967C2", "#FFFFFF", "#0059B3",
                    32, 32,
                    new BrandIconPart("DaisyIconBrandLinkedIn")),
                [DaisyLoginBrand.VK] = new BrandDefinition(
                    "LoginButton_VK", "Login with VK",
                    "#47698F", "#FFFFFF", "#35567B",
                    2240, 2240,
                    new BrandIconPart("DaisyIconBrandVK")),
                [DaisyLoginBrand.WeChat] = new BrandDefinition(
                    "LoginButton_WeChat", "Login with WeChat",
                    "#5EBB2B", "#FFFFFF", "#4EAA0C",
                    32, 32,
                    new BrandIconPart("DaisyIconBrandWeChat")),
                [DaisyLoginBrand.MetaMask] = new BrandDefinition(
                    "LoginButton_MetaMask", "Login with MetaMask",
                    "#FFFFFF", "#000000", "#E5E5E5",
                    507.83, 470.86,
                    new BrandIconPart("DaisyIconBrandMetaMask01", fillHex: "#E2761B"),
                    new BrandIconPart("DaisyIconBrandMetaMask02", fillHex: "#E4761B"),
                    new BrandIconPart("DaisyIconBrandMetaMask03", fillHex: "#E4761B"),
                    new BrandIconPart("DaisyIconBrandMetaMask04", fillHex: "#D7C1B3"),
                    new BrandIconPart("DaisyIconBrandMetaMask05", fillHex: "#233447"),
                    new BrandIconPart("DaisyIconBrandMetaMask06", fillHex: "#CD6116"),
                    new BrandIconPart("DaisyIconBrandMetaMask07", fillHex: "#E4751F"),
                    new BrandIconPart("DaisyIconBrandMetaMask08", fillHex: "#F6851B"),
                    new BrandIconPart("DaisyIconBrandMetaMask09", fillHex: "#C0AD9E"),
                    new BrandIconPart("DaisyIconBrandMetaMask10", fillHex: "#161616"),
                    new BrandIconPart("DaisyIconBrandMetaMask11", fillHex: "#763D16"),
                    new BrandIconPart("DaisyIconBrandMetaMask12", fillHex: "#F6851B"))
            };

        private sealed class BrandDefinition
        {
            public BrandDefinition(
                string defaultTextKey,
                string defaultText,
                string backgroundHex,
                string foregroundHex,
                string hoverBackgroundHex,
                double viewBoxWidth,
                double viewBoxHeight,
                params BrandIconPart[] parts)
            {
                DefaultTextKey = defaultTextKey;
                DefaultText = defaultText;
                Background = FloweryColorHelpers.ColorFromHex(backgroundHex);
                Foreground = FloweryColorHelpers.ColorFromHex(foregroundHex);
                HoverBackground = FloweryColorHelpers.ColorFromHex(hoverBackgroundHex);
                Border = HoverBackground;
                ViewBoxWidth = viewBoxWidth;
                ViewBoxHeight = viewBoxHeight;
                Parts = parts;
            }

            public string DefaultTextKey { get; }
            public string DefaultText { get; }
            public Color Background { get; }
            public Color Foreground { get; }
            public Color HoverBackground { get; }
            public Color Border { get; }
            public double ViewBoxWidth { get; }
            public double ViewBoxHeight { get; }
            public IReadOnlyList<BrandIconPart> Parts { get; }
        }

        private sealed class BrandIconPart
        {
            public BrandIconPart(
                string pathKey,
                string? fillHex = null,
                string? strokeHex = null,
                double strokeThickness = 0,
                bool roundCaps = false,
                bool roundJoin = false)
            {
                PathKey = pathKey;
                Fill = fillHex == null ? null : FloweryColorHelpers.ColorFromHex(fillHex);
                Stroke = strokeHex == null ? null : FloweryColorHelpers.ColorFromHex(strokeHex);
                StrokeThickness = strokeThickness;
                RoundCaps = roundCaps;
                RoundJoin = roundJoin;
            }

            public string PathKey { get; }
            public Color? Fill { get; }
            public Color? Stroke { get; }
            public double StrokeThickness { get; }
            public bool RoundCaps { get; }
            public bool RoundJoin { get; }
        }
    }
}
