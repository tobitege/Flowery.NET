using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests
{
    public class DaisyNeumorphicTests
    {
        private static Window Show(Control content)
        {
            var window = new Window { Content = content, Width = 800, Height = 600 };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            return window;
        }

        private static void WithGlobalMode(DaisyNeumorphicMode mode, Action action)
        {
            var previous = DaisyNeumorphic.GlobalMode;
            try
            {
                DaisyNeumorphic.GlobalMode = mode;
                action();
            }
            finally
            {
                DaisyNeumorphic.GlobalMode = previous;
                Dispatcher.UIThread.RunJobs();
            }
        }

        private static Border Part(TemplatedControl control, string name) =>
            control.GetTemplateDescendants().OfType<Border>().FirstOrDefault(b => b.Name == name)
            ?? throw new InvalidOperationException($"Template part {name} not found.");

        [AvaloniaFact]
        public void Card_Surface_Gets_Raised_Shadows_And_Restores_Them()
        {
            var card = new DaisyCard { Content = "Card" };
            var window = Show(card);
            var surface = Part(card, "PART_SolidBackground");
            var original = surface.BoxShadow;

            WithGlobalMode(DaisyNeumorphicMode.Raised, () =>
            {
                Dispatcher.UIThread.RunJobs();
                var shadows = surface.BoxShadow;
                Assert.Equal(2, shadows.Count);
                Assert.True(shadows[0].OffsetX > 0);
                Assert.True(shadows[1].OffsetX < 0);
                Assert.False(shadows[0].IsInset);
            });

            Assert.Equal(original, surface.BoxShadow);
            window.Close();
        }

        [AvaloniaFact]
        public void Inset_And_Flat_Modes_Produce_Expected_Shadows()
        {
            var inset = new DaisyCard { Content = "Inset" };
            var flat = new DaisyCard { Content = "Flat" };
            DaisyNeumorphic.SetIsEnabled(inset, true);
            DaisyNeumorphic.SetMode(inset, DaisyNeumorphicMode.Inset);
            DaisyNeumorphic.SetIsEnabled(flat, true);
            DaisyNeumorphic.SetMode(flat, DaisyNeumorphicMode.Flat);
            var window = Show(new StackPanel { Children = { inset, flat } });

            var insetShadows = Part(inset, "PART_SolidBackground").BoxShadow;
            Assert.Equal(2, insetShadows.Count);
            Assert.True(insetShadows[0].IsInset);
            Assert.True(insetShadows[1].IsInset);

            var flatShadows = Part(flat, "PART_SolidBackground").BoxShadow;
            Assert.Equal(1, flatShadows.Count);
            Assert.False(flatShadows[0].IsInset);
            window.Close();
        }

        [AvaloniaFact]
        public void Local_Disable_And_Scope_Override_Global_Mode()
        {
            var optedOut = new DaisyCard { Content = "Off" };
            DaisyNeumorphic.SetIsEnabled(optedOut, false);
            var scoped = new DaisyCard { Content = "Scoped" };
            var scopePanel = new StackPanel { Children = { scoped } };
            DaisyNeumorphic.SetScopeEnabled(scopePanel, false);
            var window = Show(new StackPanel { Children = { optedOut, scopePanel } });

            WithGlobalMode(DaisyNeumorphicMode.Raised, () =>
            {
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(0, Part(optedOut, "PART_SolidBackground").BoxShadow.Count);
                Assert.False(DaisyNeumorphic.IsEffectivelyEnabled(scoped));

                DaisyNeumorphic.SetScopeEnabled(scopePanel, null);
                Dispatcher.UIThread.RunJobs();
                Assert.True(DaisyNeumorphic.IsEffectivelyEnabled(scoped));
                Assert.Equal(2, Part(scoped, "PART_SolidBackground").BoxShadow.Count);
            });

            window.Close();
        }

        [AvaloniaFact]
        public void High_Contrast_Disables_The_Effect()
        {
            var card = new DaisyCard { Content = "Card" };
            DaisyNeumorphic.SetIsEnabled(card, true);
            DaisyNeumorphic.SetMode(card, DaisyNeumorphicMode.Raised);
            var window = Show(card);
            Assert.Equal(2, Part(card, "PART_SolidBackground").BoxShadow.Count);

            DaisyAccessibility.SetHighContrastEnabled(card, true);
            Dispatcher.UIThread.RunJobs();
            Assert.False(DaisyNeumorphic.IsEffectivelyEnabled(card));
            Assert.NotEqual(2, Part(card, "PART_SolidBackground").BoxShadow.Count);
            window.Close();
        }

        /// <summary>
        /// Every registered control and the border that must carry its Raised shadows
        /// (the surface, or the unclipped shadow layer behind a clipped surface).
        /// </summary>
        public static TheoryData<string, string> RegisteredSurfaces() => new()
        {
            { nameof(DaisyCard), "PART_SolidBackground" },
            { nameof(DaisyPatternedCard), "PART_NeumorphicShadow" },
            { nameof(DaisyToggle), "PART_KnobShadow" }
        };

        [AvaloniaTheory]
        [MemberData(nameof(RegisteredSurfaces))]
        public void Registered_Control_Draws_Raised_Shadows_On_Its_Surface(string typeName, string partName)
        {
            var type = typeof(DaisyCard).Assembly.GetTypes().Single(t => t.Name == typeName);
            var control = (Control)Activator.CreateInstance(type)!;
            control.Width = 200;
            control.Height = 60;
            var resources = Avalonia.Application.Current!.Resources;
            var addedHeroBrush = false;
            switch (control)
            {
                case DaisyCountdown countdown:
                    countdown.Background = Brushes.Gray;
                    break;
                case DaisyTabs tabs:
                    tabs.Variant = DaisyTabVariant.Boxed;
                    break;
                case DaisyNumberFlow numberFlow:
                    numberFlow.ShowControls = true;
                    break;
                case DaisyModal modal:
                    modal.IsOpen = true;
                    break;
                case DaisyTimelineItem timelineItem:
                    timelineItem.IsBoxed = true;
                    timelineItem.EndContent = "Shipped";
                    break;
                case DaisyHero when !resources.ContainsKey("DaisyBase200Brush"):
                    // The hero paints its background from the theme palette, which the test app may lack.
                    resources["DaisyBase200Brush"] = Brushes.Gray;
                    addedHeroBrush = true;
                    break;
            }

            DaisyNeumorphic.SetIsEnabled(control, true);
            DaisyNeumorphic.SetMode(control, DaisyNeumorphicMode.Raised);
            var window = Show(control);
            try
            {
                Assert.True(DaisyNeumorphic.IsRegistered(type));
                var target = Part((TemplatedControl)control, partName);
                Assert.Equal(2, target.BoxShadow.Count);
                Assert.True(target.BoxShadow[0].OffsetX > 0);
                Assert.False(target.BoxShadow[0].IsInset);
                // Avalonia renders borders with uneven thickness as geometry and draws no BoxShadow for them.
                Assert.True(target.BorderThickness.IsUniform, $"{partName} has uneven BorderThickness {target.BorderThickness}");
            }
            finally
            {
                window.Close();
                if (addedHeroBrush)
                    resources.Remove("DaisyBase200Brush");
            }
        }

        [AvaloniaFact]
        public void NumberFlow_Draws_The_Effect_Only_With_Its_Controls()
        {
            var numberFlow = new DaisyNumberFlow { Value = 42 };
            DaisyNeumorphic.SetIsEnabled(numberFlow, true);
            DaisyNeumorphic.SetMode(numberFlow, DaisyNeumorphicMode.Raised);
            var window = Show(numberFlow);
            try
            {
                // Without ShowControls the container is transparent and borderless: there is no surface to raise.
                foreach (var shadow in Part(numberFlow, "OuterContainer").BoxShadow)
                    Assert.Equal(0, shadow.Color.A);

                numberFlow.ShowControls = true;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(2, Part(numberFlow, "OuterContainer").BoxShadow.Count);
            }
            finally
            {
                window.Close();
            }
        }
    }
}
