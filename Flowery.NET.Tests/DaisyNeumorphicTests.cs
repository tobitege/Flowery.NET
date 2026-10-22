using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Controls.Custom;
using Flowery.Controls.Custom.Weather;
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
        public void GlobalMode_And_GlobalEnabled_Stay_In_Sync()
        {
            var previous = DaisyNeumorphic.GlobalMode;
            try
            {
                DaisyNeumorphic.GlobalMode = DaisyNeumorphicMode.Inset;
                Assert.True(DaisyNeumorphic.GlobalEnabled);

                DaisyNeumorphic.GlobalEnabled = false;
                Assert.Equal(DaisyNeumorphicMode.Inset, DaisyNeumorphic.GlobalMode);

                DaisyNeumorphic.GlobalMode = DaisyNeumorphicMode.None;
                Assert.False(DaisyNeumorphic.GlobalEnabled);

                DaisyNeumorphic.GlobalEnabled = true;
                Assert.Equal(DaisyNeumorphicMode.Inset, DaisyNeumorphic.GlobalMode);
            }
            finally
            {
                DaisyNeumorphic.GlobalMode = previous;
            }
        }

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
        public void Table_Is_One_Surface_And_Rows_And_Cells_Stay_Flat()
        {
            var toggle = new DaisyToggle { IsChecked = true };
            var headerRow = new DaisyTableRow();
            headerRow.Items.Add(new DaisyTableHeaderCell { Content = "Name" });
            headerRow.Items.Add(new DaisyTableHeaderCell { Content = "Enabled" });
            var head = new DaisyTableHead();
            head.Items.Add(headerRow);
            var row = new DaisyTableRow();
            var nameCell = new DaisyTableCell { Content = "Row" };
            row.Items.Add(nameCell);
            row.Items.Add(new DaisyTableCell { Content = toggle });
            var body = new DaisyTableBody();
            body.Items.Add(row);
            var table = new DaisyTable { Width = 300 };
            table.Items.Add(head);
            table.Items.Add(body);
            var window = Show(table);

            WithGlobalMode(DaisyNeumorphicMode.Raised, () =>
            {
                Dispatcher.UIThread.RunJobs();
                Assert.True(DaisyNeumorphic.IsEffectivelyEnabled(table));
                Assert.Equal(2, Part(table, "PART_NeumorphicShadow").BoxShadow.Count);

                Assert.False(DaisyNeumorphic.IsEffectivelyEnabled(row));
                Assert.False(DaisyNeumorphic.IsEffectivelyEnabled(nameCell));
                Assert.False(DaisyNeumorphic.IsEffectivelyEnabled(headerRow.Items.OfType<DaisyTableHeaderCell>().First()));
                Assert.False(DaisyNeumorphic.IsRegistered(typeof(DaisyTableRow)));
                Assert.False(DaisyNeumorphic.IsRegistered(typeof(DaisyTableCell)));
                Assert.False(DaisyNeumorphic.IsRegistered(typeof(DaisyTableHeaderCell)));

                // No row, cell or section border carries a shadow; controls inside cells keep their own effect.
                var tableParts = table.GetVisualDescendants().OfType<Border>()
                    .Where(b => b.TemplatedParent is DaisyTableRow or DaisyTableCell or DaisyTableHeaderCell
                        or DaisyTableHead or DaisyTableBody)
                    .ToList();
                Assert.NotEmpty(tableParts);
                Assert.All(tableParts, b => Assert.Equal(0, b.BoxShadow.Count));
                Assert.Equal(2, Part(toggle, "SwitchArea").BoxShadow.Count);
            });

            window.Close();
        }

        [AvaloniaFact]
        public void Table_Inset_Draws_On_The_Overlay_Above_The_Rows()
        {
            var table = new DaisyTable { Width = 200, Height = 100 };
            DaisyNeumorphic.SetIsEnabled(table, true);
            DaisyNeumorphic.SetMode(table, DaisyNeumorphicMode.Inset);
            var window = Show(table);

            var overlay = Part(table, "PART_NeumorphicInset");
            Assert.True(overlay.BoxShadow[0].IsInset);
            Assert.False(overlay.IsHitTestVisible);
            Assert.Equal(0, Part(table, "PART_Surface").BoxShadow.Count);
            Assert.Equal(0, Part(table, "PART_NeumorphicShadow").BoxShadow.Count);
            window.Close();
        }

        [AvaloniaFact]
        public void Join_And_ButtonGroup_Children_Stay_Flat()
        {
            var joinButton = new DaisyButton { Content = "A" };
            var join = new DaisyJoin { Children = { joinButton, new DaisyButton { Content = "B" } } };
            var groupButton = new DaisyButton { Content = "C" };
            var group = new DaisyButtonGroup();
            group.Items.Add(groupButton);
            var standalone = new DaisyButton { Content = "D" };
            var window = Show(new StackPanel { Children = { join, group, standalone } });

            WithGlobalMode(DaisyNeumorphicMode.Raised, () =>
            {
                Dispatcher.UIThread.RunJobs();
                Assert.False(DaisyNeumorphic.IsEffectivelyEnabled(joinButton));
                Assert.False(DaisyNeumorphic.IsEffectivelyEnabled(groupButton));
                Assert.True(DaisyNeumorphic.IsEffectivelyEnabled(group));
                Assert.True(DaisyNeumorphic.IsEffectivelyEnabled(standalone));
                Assert.Equal(0, Part(joinButton, "PART_Background").BoxShadow.Count);
                Assert.Equal(2, Part(standalone, "PART_Background").BoxShadow.Count);
                Assert.Equal(2, Part(group, "PART_NeumorphicShadow").BoxShadow.Count);
            });

            window.Close();
        }

        [AvaloniaFact]
        public void Ghost_Buttons_Fab_And_ChatBubbles_Stay_Flat_Under_Global_Mode()
        {
            var ghost = new DaisyButton { Content = "Ghost", Variant = DaisyButtonVariant.Ghost };
            var bubble = new DaisyChatBubble { Content = "Hi" };
            var fab = new DaisyFab();
            var window = Show(new StackPanel { Children = { ghost, bubble, fab } });

            WithGlobalMode(DaisyNeumorphicMode.Raised, () =>
            {
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(0, Part(ghost, "PART_Background").BoxShadow.Count);
                Assert.False(DaisyNeumorphic.IsEffectivelyEnabled(bubble));
                Assert.False(DaisyNeumorphic.IsEffectivelyEnabled(fab));

                ghost.Variant = DaisyButtonVariant.Primary;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(2, Part(ghost, "PART_Background").BoxShadow.Count);

                DaisyNeumorphic.SetIsEnabled(bubble, true);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(2, Part(bubble, "PART_Bubble").BoxShadow.Count);
            });

            window.Close();
        }

        [AvaloniaFact]
        public void Card_Drop_Shadow_Effect_Is_Suppressed_While_Active()
        {
            var card = new DaisyCard { Content = "Card" };
            var window = Show(card);
            Assert.NotNull(card.Effect);

            WithGlobalMode(DaisyNeumorphicMode.Raised, () =>
            {
                Dispatcher.UIThread.RunJobs();
                Assert.Null(card.Effect);
            });

            Assert.NotNull(card.Effect);
            window.Close();
        }

        [AvaloniaFact]
        public void Default_Clip_Is_Lifted_While_Active_And_Restored_Afterwards()
        {
            var input = new DaisyInput { Width = 200 };
            var progress = new DaisyProgress { Width = 200, Value = 50 };
            var window = Show(new StackPanel { Children = { input, progress } });
            Assert.True(input.ClipToBounds);

            WithGlobalMode(DaisyNeumorphicMode.Raised, () =>
            {
                Dispatcher.UIThread.RunJobs();
                Assert.False(input.ClipToBounds);
                // The track clips its indicator explicitly; that clip stays and the shadow goes to the layer.
                Assert.True(Part(progress, "PART_Track").ClipToBounds);
                Assert.Equal(2, Part(progress, "PART_NeumorphicShadow").BoxShadow.Count);
            });

            Assert.True(input.ClipToBounds);
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
            { nameof(DaisyExpandableCard), "PART_SolidBackground" },
            { nameof(DaisyAlert), "PART_Surface" },
            { nameof(DaisyAccordionItem), "PART_Surface" },
            { nameof(DaisyCollapse), "PART_Surface" },
            { nameof(DaisyChatBubble), "PART_Bubble" },
            { nameof(DaisyHero), "PART_BackgroundBorder" },
            { nameof(DaisyMockup), "PART_Surface" },
            { nameof(DaisyModal), "PART_Dialog" },
            { nameof(DaisyNavbar), "PART_RootBorder" },
            { nameof(DaisyDock), "PART_Surface" },
            { nameof(DaisyWeatherCard), "PART_Surface" },
            { nameof(DaisyWeatherMetrics), "PART_Surface" },
            { nameof(DaisyButton), "PART_Background" },
            { nameof(DaisyCopyButton), "PART_Background" },
            { nameof(DaisyBadge), "PART_Surface" },
            { nameof(DaisyKbd), "PART_NeumorphicShadow" },
            { nameof(DaisyToggle), "PART_KnobShadow" },
            { nameof(DaisyTimelineItem), "EndBox" },
            { nameof(DaisyWeatherForecastItem), "PART_Surface" },
            { nameof(DaisyAvatar), "PART_NeumorphicShadow" },
            { nameof(DaisyCountdown), "PART_Surface" },
            { nameof(DaisyNumberFlow), "OuterContainer" },
            { nameof(DaisyModifierKeys), "ShiftKeyShadow" },
            { nameof(DaisySlideToConfirm), "PART_Handle" },
            { nameof(DaisyInput), "PART_Background" },
            { nameof(DaisyTextArea), "PART_Background" },
            { nameof(DaisyMaskInput), "PART_Background" },
            { nameof(DaisyPasswordBox), "PART_Background" },
            { nameof(DaisyNumericUpDown), "PART_Background" },
            { nameof(DaisyFileInput), "PART_NeumorphicShadow" },
            { nameof(DaisyTagPicker), "PART_SelectedContainer" },
            { nameof(DaisySelect), "Background" },
            { nameof(DaisySizeDropdown), "Background" },
            { nameof(DaisyThemeDropdown), "Background" },
            { nameof(DaisyProductThemeDropdown), "Background" },
            { nameof(DaisyCalendarDatePicker), "Background" },
            { nameof(DaisyDropdown), "PART_Trigger" },
            { nameof(DaisyRange), "TrackBackground" },
            { nameof(DaisyProgress), "PART_NeumorphicShadow" },
            { nameof(DaisySkeleton), "PART_BackgroundBorder" },
            { nameof(DaisyDateTimelineItem), "PART_Background" },
            { nameof(DaisyTabs), "PART_HeaderContainer" },
            { nameof(DaisyTable), "PART_NeumorphicShadow" },
            { nameof(DaisyList), "PART_NeumorphicShadow" },
            { nameof(DaisyStats), "PART_NeumorphicShadow" },
            { nameof(DaisyButtonGroup), "PART_NeumorphicShadow" }
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

        [AvaloniaFact]
        public void SlideToConfirm_Track_Is_Always_Inset()
        {
            var slide = new DaisySlideToConfirm { Width = 240 };
            DaisyNeumorphic.SetIsEnabled(slide, true);
            DaisyNeumorphic.SetMode(slide, DaisyNeumorphicMode.Raised);
            var window = Show(slide);

            Assert.True(Part(slide, "PART_Track").BoxShadow[0].IsInset);
            Assert.False(Part(slide, "PART_Handle").BoxShadow[0].IsInset);
            window.Close();
        }

        [AvaloniaFact]
        public void Scope_Enables_Controls_But_Not_Segments()
        {
            var joinButton = new DaisyButton { Content = "A" };
            var join = new DaisyJoin { Children = { joinButton, new DaisyButton { Content = "B" } } };
            var standalone = new DaisyButton { Content = "C" };
            var scope = new StackPanel { Children = { join, standalone } };
            DaisyNeumorphic.SetScopeEnabled(scope, true);
            DaisyNeumorphic.SetScopeMode(scope, DaisyNeumorphicMode.Raised);
            var window = Show(scope);

            Assert.True(DaisyNeumorphic.IsEffectivelyEnabled(standalone));
            Assert.False(DaisyNeumorphic.IsEffectivelyEnabled(joinButton));
            Assert.Equal(0, Part(joinButton, "PART_Background").BoxShadow.Count);
            window.Close();
        }

        [AvaloniaFact]
        public void Scope_Mode_And_Intensity_Apply_To_The_Subtree_Below_Own_Values()
        {
            var scoped = new DaisyCard { Content = "Scoped" };
            var own = new DaisyCard { Content = "Own" };
            DaisyNeumorphic.SetMode(own, DaisyNeumorphicMode.Flat);
            DaisyNeumorphic.SetIntensity(own, 0.2);
            var scope = new StackPanel { Children = { scoped, own } };
            DaisyNeumorphic.SetScopeEnabled(scope, true);
            DaisyNeumorphic.SetScopeMode(scope, DaisyNeumorphicMode.Inset);
            DaisyNeumorphic.SetScopeIntensity(scope, 0.8);
            var window = Show(scope);

            Assert.Equal(DaisyNeumorphicMode.Inset, DaisyNeumorphic.GetEffectiveMode(scoped));
            Assert.Equal(0.8, DaisyNeumorphic.GetEffectiveIntensity(scoped));
            Assert.True(Part(scoped, "PART_SolidBackground").BoxShadow[0].IsInset);
            Assert.Equal(DaisyNeumorphicMode.Flat, DaisyNeumorphic.GetEffectiveMode(own));
            Assert.Equal(0.2, DaisyNeumorphic.GetEffectiveIntensity(own));

            DaisyNeumorphic.SetScopeMode(scope, DaisyNeumorphicMode.Raised);
            Dispatcher.UIThread.RunJobs();
            Assert.False(Part(scoped, "PART_SolidBackground").BoxShadow[0].IsInset);
            window.Close();
        }

        [AvaloniaFact]
        public void Toggle_Knob_Shadow_Follows_The_Knob()
        {
            var toggle = new DaisyToggle { Size = DaisySize.Large };
            var window = Show(toggle);
            var knob = toggle.GetTemplateDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().Single(e => e.Name == "Knob");
            var shadow = Part(toggle, "PART_KnobShadow");

            toggle.IsChecked = true;
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(knob.Width, shadow.Width);
            Assert.Equal(knob.Height, shadow.Height);
            Assert.Equal(Canvas.GetLeft(knob), Canvas.GetLeft(shadow));
            Assert.Equal(Canvas.GetTop(knob), Canvas.GetTop(shadow));
            window.Close();
        }

        [AvaloniaFact]
        public void Forecast_Clips_Normally_And_Lets_Tile_Shadows_Through_While_Drawn()
        {
            var days = new[]
            {
                new Flowery.Controls.Custom.Weather.Models.ForecastDay { Date = new DateTime(2026, 10, 22), HighTemperature = 18, LowTemperature = 9 },
                new Flowery.Controls.Custom.Weather.Models.ForecastDay { Date = new DateTime(2026, 10, 23), HighTemperature = 16, LowTemperature = 8 }
            };
            var forecast = new DaisyWeatherForecast { ItemsSource = days };
            var window = Show(forecast);
            Assert.True(forecast.ClipToBounds);

            var previous = DaisyNeumorphic.GlobalMode;
            try
            {
                DaisyNeumorphic.GlobalMode = DaisyNeumorphicMode.Raised;
                Dispatcher.UIThread.RunJobs();
                Assert.False(forecast.ClipToBounds);

                // Each tile holds its own lift: turning one tile off keeps the strip open for the other.
                var tiles = forecast.GetVisualDescendants().OfType<DaisyWeatherForecastItem>().ToList();
                DaisyNeumorphic.SetIsEnabled(tiles[0], false);
                Dispatcher.UIThread.RunJobs();
                Assert.False(forecast.ClipToBounds);

                DaisyNeumorphic.SetIsEnabled(tiles[1], false);
                Dispatcher.UIThread.RunJobs();
                Assert.True(forecast.ClipToBounds);
            }
            finally
            {
                DaisyNeumorphic.GlobalMode = previous;
                Dispatcher.UIThread.RunJobs();
                window.Close();
            }

            Assert.True(forecast.ClipToBounds);
        }

        [AvaloniaFact]
        public void Selection_Control_Wells_Are_Always_Inset()
        {
            var toggle = new DaisyToggle();
            var checkBox = new DaisyCheckBox();
            var radio = new DaisyRadio();
            foreach (var control in new Control[] { toggle, checkBox, radio })
            {
                DaisyNeumorphic.SetIsEnabled(control, true);
                DaisyNeumorphic.SetMode(control, DaisyNeumorphicMode.Raised);
            }

            var window = Show(new StackPanel { Children = { toggle, checkBox, radio } });

            Assert.True(Part(toggle, "SwitchArea").BoxShadow[0].IsInset);
            Assert.False(Part(toggle, "PART_KnobShadow").BoxShadow[0].IsInset);
            Assert.True(Part(checkBox, "PART_Border").BoxShadow[0].IsInset);
            Assert.True(Part(radio, "PART_Border").BoxShadow[0].IsInset);
            window.Close();
        }

        [AvaloniaFact]
        public void Forecast_Tiles_Each_Draw_The_Effect()
        {
            var forecast = new DaisyWeatherForecast
            {
                ItemsSource = new[]
                {
                    new Flowery.Controls.Custom.Weather.Models.ForecastDay { Date = new DateTime(2026, 10, 22), HighTemperature = 18, LowTemperature = 9 },
                    new Flowery.Controls.Custom.Weather.Models.ForecastDay { Date = new DateTime(2026, 10, 23), HighTemperature = 16, LowTemperature = 8 }
                }
            };
            var window = Show(forecast);
            var previous = DaisyNeumorphic.GlobalMode;
            try
            {
                DaisyNeumorphic.GlobalMode = DaisyNeumorphicMode.Raised;
                Dispatcher.UIThread.RunJobs();

                var tiles = forecast.GetVisualDescendants().OfType<DaisyWeatherForecastItem>().ToList();
                Assert.Equal(2, tiles.Count);
                Assert.All(tiles, tile => Assert.Equal(2, Assert.IsType<Border>(tile.GetVisualChildren().First()).BoxShadow.Count));
            }
            finally
            {
                DaisyNeumorphic.GlobalMode = previous;
                window.Close();
            }
        }

        [AvaloniaFact]
        public void Shadow_Colors_Follow_The_Theme_Unless_Set_Globally()
        {
            // Own resources win over the merged palettes, so the test does not depend on which palette
            // other tests left loaded. SetCurrentTheme only renames the theme and raises ThemeChanged.
            var previousTheme = DaisyThemeManager.CurrentThemeName;
            var resources = Avalonia.Application.Current!.Resources;
            var base300 = Color.FromRgb(200, 100, 50);
            resources["DaisyBase300Color"] = base300;
            var card = new DaisyCard { Content = "Card" };
            var window = Show(card);
            try
            {
                DaisyThemeManager.SetCurrentTheme("Dark");
                DaisyThemeManager.SetCurrentTheme("Light");
                Assert.Equal(Color.FromRgb(100, 50, 25), DaisyNeumorphic.GetEffectiveDarkShadowColor(card));
                Assert.Equal(Colors.White, DaisyNeumorphic.GetEffectiveLightShadowColor(card));

                DaisyThemeManager.SetCurrentTheme("Dark");
                Assert.Equal(Color.FromRgb(60, 30, 15), DaisyNeumorphic.GetEffectiveDarkShadowColor(card));

                DaisyNeumorphic.GlobalDarkShadowColor = Colors.Red;
                DaisyThemeManager.SetCurrentTheme("Light");
                Assert.Equal(Colors.Red, DaisyNeumorphic.GetEffectiveDarkShadowColor(card));

                DaisyNeumorphic.SetDarkShadowColor(card, Colors.Blue);
                Assert.Equal(Colors.Blue, DaisyNeumorphic.GetEffectiveDarkShadowColor(card));
            }
            finally
            {
                DaisyNeumorphic.GlobalDarkShadowColor = null;
                resources.Remove("DaisyBase300Color");
                if (previousTheme != null) DaisyThemeManager.SetCurrentTheme(previousTheme);
                window.Close();
            }
        }

        [AvaloniaFact]
        public void Template_Swap_Moves_The_Shadows_To_The_New_Surface()
        {
            var input = new DaisyInput { Width = 200, Label = "Name" };
            DaisyNeumorphic.SetIsEnabled(input, true);
            DaisyNeumorphic.SetMode(input, DaisyNeumorphicMode.Raised);
            var window = Show(input);
            var oldSurface = Part(input, "PART_Background");
            Assert.Equal(2, oldSurface.BoxShadow.Count);

            input.LabelPosition = DaisyLabelPosition.Floating;
            Dispatcher.UIThread.RunJobs();

            var newSurface = Part(input, "PART_Background");
            Assert.NotSame(oldSurface, newSurface);
            Assert.Equal(0, oldSurface.BoxShadow.Count);
            Assert.Equal(2, newSurface.BoxShadow.Count);
            window.Close();
        }

        [AvaloniaFact]
        public void Sidebar_Selector_And_Global_Mode_Stay_In_Sync()
        {
            var sidebar = new FloweryComponentSidebar();
            var window = Show(sidebar);
            var previous = DaisyNeumorphic.GlobalMode;
            try
            {
                sidebar.NeumorphicMode = DaisyNeumorphicMode.Inset;
                Assert.Equal(DaisyNeumorphicMode.Inset, DaisyNeumorphic.GlobalMode);

                DaisyNeumorphic.GlobalMode = DaisyNeumorphicMode.Flat;
                Assert.Equal(DaisyNeumorphicMode.Flat, sidebar.NeumorphicMode);
                Assert.Contains(DaisyNeumorphicMode.Raised, sidebar.AvailableNeumorphicModes);
            }
            finally
            {
                DaisyNeumorphic.GlobalMode = previous;
                window.Close();
            }
        }

        [AvaloniaFact]
        public void Background_Watch_Applies_And_Removes_The_Effect()
        {
            var countdown = new DaisyCountdown();
            DaisyNeumorphic.SetIsEnabled(countdown, true);
            DaisyNeumorphic.SetMode(countdown, DaisyNeumorphicMode.Raised);
            var window = Show(countdown);
            var root = Assert.IsType<Border>(countdown.GetVisualChildren().First());
            Assert.Equal(0, root.BoxShadow.Count);

            countdown.Background = Brushes.Gray;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, root.BoxShadow.Count);

            countdown.Background = null;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(0, root.BoxShadow.Count);
            window.Close();
        }

        private sealed class ForeignSurfaceHost : TemplatedControl
        {
            public Border? Foreign { get; init; }
        }

        private class MergeBase : TemplatedControl;

        private sealed class MergeInherited : MergeBase;

        private sealed class MergeExcluded : MergeBase;

        private static readonly Lazy<bool> TestRegistrations = new(() =>
        {
            DaisyNeumorphic.RegisterSurface<ForeignSurfaceHost>(c => c.Foreign);
            // Exclusion first, surface second: the exclusion must still win.
            DaisyNeumorphic.RegisterExclusion<MergeExcluded>();
            DaisyNeumorphic.RegisterSurface<MergeExcluded>(_ => null);
            DaisyNeumorphic.RegisterSurface<MergeBase>(_ => null);
            return true;
        });

        [AvaloniaFact]
        public void Registration_Is_Inherited_And_Independent_Of_Call_Order()
        {
            Assert.True(TestRegistrations.Value);
            Assert.True(DaisyNeumorphic.IsRegistered(typeof(MergeBase)));
            Assert.True(DaisyNeumorphic.IsRegistered(typeof(MergeInherited)));
            Assert.False(DaisyNeumorphic.IsRegistered(typeof(MergeExcluded)));
        }

        [AvaloniaFact]
        public void Surface_Outside_The_Control_Leaves_Foreign_Clips_Alone()
        {
            Assert.True(TestRegistrations.Value);
            var foreign = new Border { Width = 50, Height = 20 };
            var host = new ForeignSurfaceHost { Foreign = foreign, Width = 50, Height = 20 };
            DaisyNeumorphic.SetIsEnabled(host, true);
            DaisyNeumorphic.SetMode(host, DaisyNeumorphicMode.Raised);
            var outer = new ContentControl { Content = new StackPanel { Children = { host, foreign } } };
            var window = Show(outer);
            Assert.True(outer.ClipToBounds);

            Assert.Equal(2, foreign.BoxShadow.Count);
            Assert.True(outer.ClipToBounds);
            window.Close();
        }

        [AvaloniaFact]
        public void Elevation_Follows_Control_Size()
        {
            var small = new DaisyButton { Size = DaisySize.ExtraSmall };
            var large = new DaisyButton { Size = DaisySize.ExtraLarge };

            Assert.Equal(10, DaisyNeumorphic.GetEffectiveElevation(small));
            Assert.Equal(16, DaisyNeumorphic.GetEffectiveElevation(large));
        }
    }
}
