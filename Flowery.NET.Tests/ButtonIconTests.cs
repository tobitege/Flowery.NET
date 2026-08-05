using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Enums;
using Flowery.Localization;
using Xunit;

namespace Flowery.NET.Tests
{
    [Collection("LocalizationTests")]
    public class ButtonIconTests : IDisposable
    {
        private readonly CultureInfo _originalCulture;

        public ButtonIconTests()
        {
            _originalCulture = FloweryLocalization.CurrentCulture;
        }

        public void Dispose()
        {
            FloweryLocalization.SetCulture(_originalCulture);
        }

        [AvaloniaFact]
        public void DaisyButton_ResolvesSymbolAndSizeAwareSpacing()
        {
            var button = new DaisyButton
            {
                Content = "Save",
                IconSymbol = DaisyIconSymbol.Save,
                Size = Flowery.Controls.DaisySize.Large
            };

            Assert.NotNull(button.EffectiveIconData);
            Assert.True(button.HasUnifiedIcon);
            Assert.Equal(
                LayoutTestAssertions.GetUnoButtonIconSize(button.Size),
                button.EffectiveIconSize);
            Assert.Equal(
                LayoutTestAssertions.GetUnoButtonIconSpacing(button.Size),
                button.EffectiveIconSpacing);
        }

        [AvaloniaFact]
        public void DaisyButton_SizeAndShapeMetrics_MatchUnoAndCenterIcons()
        {
            var sizes = new[]
            {
                Flowery.Controls.DaisySize.ExtraSmall,
                Flowery.Controls.DaisySize.Small,
                Flowery.Controls.DaisySize.Medium,
                Flowery.Controls.DaisySize.Large,
                Flowery.Controls.DaisySize.ExtraLarge
            };
            var panel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 8
            };
            var controls = new List<(DaisyButton Standard, DaisyButton Square, DaisyButton Circle)>();

            foreach (var size in sizes)
            {
                var standard = new DaisyButton
                {
                    Content = size.ToString(),
                    IconSymbol = DaisyIconSymbol.Add,
                    Size = size
                };
                var square = new DaisyButton
                {
                    IconSymbol = DaisyIconSymbol.Add,
                    Shape = DaisyButtonShape.Square,
                    Size = size
                };
                var circle = new DaisyButton
                {
                    IconSymbol = DaisyIconSymbol.Add,
                    Shape = DaisyButtonShape.Circle,
                    Size = size
                };
                panel.Children.Add(standard);
                panel.Children.Add(square);
                panel.Children.Add(circle);
                controls.Add((standard, square, circle));
            }

            var window = new Window { Content = panel };
            window.Show();
            panel.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            try
            {
                for (var index = 0; index < sizes.Length; index++)
                {
                    var size = sizes[index];
                    var expectedHeight = LayoutTestAssertions.GetUnoButtonHeight(size);
                    var expectedIconSize = LayoutTestAssertions.GetUnoButtonIconSize(size);
                    var (standard, square, circle) = controls[index];

                    Assert.Equal(expectedHeight, standard.Bounds.Height, precision: 3);
                    Assert.Equal(LayoutTestAssertions.GetUnoButtonFontSize(size), standard.FontSize);
                    Assert.Equal(LayoutTestAssertions.GetUnoButtonPadding(size), standard.Padding);
                    Assert.Equal(expectedIconSize, standard.EffectiveIconSize);
                    Assert.Equal(LayoutTestAssertions.GetUnoButtonIconSpacing(size), standard.EffectiveIconSpacing);
                    Assert.True(standard.Bounds.Width > standard.Bounds.Height);
                    LayoutTestAssertions.HasHorizontalPadding(standard);

                    foreach (var shapedButton in new[] { square, circle })
                    {
                        LayoutTestAssertions.HasSize(shapedButton, expectedHeight, expectedHeight);
                        Assert.Equal(new Avalonia.Thickness(0), shapedButton.Padding);
                        Assert.Equal(expectedIconSize, shapedButton.EffectiveIconSize);

                        var icon = shapedButton.GetVisualDescendants()
                            .OfType<Viewbox>()
                            .Single(control => string.Equals(
                                control.Name,
                                "PART_UnifiedIcon",
                                StringComparison.Ordinal));
                        LayoutTestAssertions.IsCentered(shapedButton, icon);
                    }
                }
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void DaisyIconText_PrefersCustomGeometryOverSymbol()
        {
            var customGeometry = StreamGeometry.Parse("M0,0 L10,10");
            var iconText = new DaisyIconText
            {
                IconSymbol = DaisyIconSymbol.Add,
                IconData = customGeometry
            };

            Assert.Same(customGeometry, iconText.EffectiveIconData);
            Assert.True(iconText.HasIcon);
        }

        [AvaloniaFact]
        public void AllIconSymbols_ResolveToGeometry()
        {
            foreach (DaisyIconSymbol symbol in Enum.GetValues(typeof(DaisyIconSymbol)))
            {
                var iconText = new DaisyIconText { IconSymbol = symbol };
                Assert.NotNull(iconText.EffectiveIconData);
            }
        }

        [AvaloniaFact]
        public void DaisyLoginButton_UsesLocalizedLabelAndBrandIcon()
        {
            FloweryLocalization.SetCulture("de");
            var button = new DaisyLoginButton
            {
                Brand = DaisyLoginBrand.Google
            };

            Assert.Equal("Mit Google anmelden", button.Content);
            Assert.NotNull(button.IconLeft);
        }

        [AvaloniaFact]
        public void AllLoginBrands_CreateAnIcon()
        {
            foreach (DaisyLoginBrand brand in Enum.GetValues(typeof(DaisyLoginBrand)))
            {
                var button = new DaisyLoginButton { Brand = brand };
                Assert.NotNull(button.IconLeft);
            }
        }

        [AvaloniaFact]
        public void ButtonIconTemplates_LoadForAllPlacements()
        {
            var panel = new StackPanel();
            foreach (IconPlacement placement in Enum.GetValues(typeof(IconPlacement)))
            {
                panel.Children.Add(new DaisyButton
                {
                    Content = placement.ToString(),
                    IconSymbol = DaisyIconSymbol.Add,
                    IconPlacement = placement
                });
                panel.Children.Add(new DaisyIconText
                {
                    Text = placement.ToString(),
                    IconSymbol = DaisyIconSymbol.Add,
                    IconPlacement = placement
                });
            }

            var window = new Window { Content = panel };
            window.Show();

            Assert.Equal(8, panel.Children.Count);
            window.Close();
        }

        [AvaloniaFact]
        public void DaisyIconText_AllPlacementsCenterVisibleContent()
        {
            var panel = new StackPanel();
            var controls = new List<DaisyIconText>();
            foreach (IconPlacement placement in Enum.GetValues(typeof(IconPlacement)))
            {
                var control = new DaisyIconText
                {
                    Text = placement.ToString(),
                    IconSymbol = DaisyIconSymbol.Add,
                    IconPlacement = placement
                };
                panel.Children.Add(control);
                controls.Add(control);
            }

            var window = new Window { Content = panel };
            window.Show();

            try
            {
                Dispatcher.UIThread.RunJobs();
                panel.UpdateLayout();

                Assert.All(controls, AssertVisibleContentCentered);
            }
            finally
            {
                window.Close();
            }
        }

        private static void AssertVisibleContentCentered(DaisyIconText control)
        {
            var icon = control.GetVisualDescendants()
                .OfType<Viewbox>()
                .Single(descendant => string.Equals(
                    descendant.Name,
                    "PART_IconViewbox",
                    StringComparison.Ordinal));
            var text = control.GetVisualDescendants()
                .OfType<Control>()
                .Single(descendant => string.Equals(
                    descendant.Name,
                    "PART_TextBlock",
                    StringComparison.Ordinal));
            var iconPosition = LayoutTestAssertions.GetPosition(icon, control);
            var textPosition = LayoutTestAssertions.GetPosition(text, control);
            var left = Math.Min(iconPosition.X, textPosition.X);
            var top = Math.Min(iconPosition.Y, textPosition.Y);
            var right = Math.Max(
                iconPosition.X + icon.Bounds.Width,
                textPosition.X + text.Bounds.Width);
            var bottom = Math.Max(
                iconPosition.Y + icon.Bounds.Height,
                textPosition.Y + text.Bounds.Height);

            Assert.InRange(Math.Abs(control.Bounds.Width / 2 - (left + right) / 2), 0, 0.5);
            Assert.InRange(Math.Abs(control.Bounds.Height / 2 - (top + bottom) / 2), 0, 0.5);
        }
    }
}
