using Flowery.Controls.Custom;
using Flowery.Controls.Custom.Weather;

namespace Flowery.Controls
{
    public static partial class DaisyNeumorphic
    {
        private const string SurfacePart = "PART_Surface";
        private const string ShadowLayer = "PART_NeumorphicShadow";
        private const string InsetOverlay = "PART_NeumorphicInset";

        /// <summary>
        /// Registers the surfaces of the built-in Daisy controls.
        /// </summary>
        /// <remarks>
        /// Only controls with a visible surface are registered. Controls without one (spinners, text,
        /// icons, custom-rendered pickers, plain layout containers) stay flat. Rows, cells and segments
        /// of composite controls are excluded so the container reads as one surface.
        /// </remarks>
        private static void RegisterBuiltInSurfaces()
        {
            // Cards and surfaces. DaisyExpandableCard inherits the DaisyCard registration.
            RegisterSurface<DaisyCard>(c => TemplatePart(c, "PART_SolidBackground"));
            RegisterEffectSuppression<DaisyCard>();
            RegisterSurface(new DaisyNeumorphicSurface<DaisyPatternedCard>
            {
                Resolve = c => TemplatePart(c, "PART_SolidBackground"),
                OuterShadowLayer = c => TemplatePart(c, ShadowLayer)
            });
            RegisterSurface<DaisyAlert>(c => TemplatePart(c, SurfacePart));
            RegisterSurface<DaisyAccordionItem>(c => TemplatePart(c, SurfacePart));
            RegisterSurface<DaisyCollapse>(c => TemplatePart(c, SurfacePart));
            RegisterSurface<DaisyChatBubble>(c => TemplatePart(c, "PART_Bubble"));
            RegisterDisabledByDefault<DaisyChatBubble>();
            RegisterSurface(new DaisyNeumorphicSurface<DaisyHero>
            {
                Resolve = c => TemplatePart(c, "PART_BackgroundBorder"),
                RequiresBackground = true
            });
            RegisterSurface<DaisyMockup>(c => TemplatePart(c, SurfacePart));
            RegisterSurface<DaisyModal>(c => TemplatePart(c, "PART_Dialog"));
            RegisterSurface<DaisyNavbar>(c => TemplatePart(c, "PART_RootBorder"));
            RegisterEffectSuppression<DaisyNavbar>();
            RegisterSurface<DaisyDock>(c => TemplatePart(c, SurfacePart));
            RegisterSurface<DaisyWeatherCard>(c => TemplatePart(c, SurfacePart));
            RegisterEffectSuppression<DaisyWeatherCard>();
            RegisterSurface(new DaisyNeumorphicSurface<DaisyWeatherMetrics> { Resolve = c => TemplatePart(c, SurfacePart), RequiresBackground = true });
            RegisterSurface<DaisyWeatherForecastItem>(c => TemplatePart(c, SurfacePart));
            // The strip clips by default; let the tile shadows through while they are drawn.
            RegisterShadowPassThrough<DaisyWeatherForecast>();
            // Only the boxed variant shows these borders; hidden borders draw nothing.
            RegisterSurface<DaisyTimelineItem>(c => TemplatePart(c, "StartBox"));
            RegisterSurface<DaisyTimelineItem>(c => TemplatePart(c, "EndBox"));

            // Buttons and badges
            RegisterSurface(new DaisyNeumorphicSurface<DaisyButton>
            {
                Resolve = c => TemplatePart(c, "PART_Background"),
                IsActive = c => c.Variant is not (DaisyButtonVariant.Ghost or DaisyButtonVariant.Link)
            });
            RegisterRefreshTriggers<DaisyButton>(DaisyButton.VariantProperty);
            RegisterSurface<DaisyBadge>(c => TemplatePart(c, SurfacePart));
            RegisterSurface(new DaisyNeumorphicSurface<DaisyKbd>
            {
                Resolve = c => TemplatePart(c, SurfacePart),
                OuterShadowLayer = c => TemplatePart(c, ShadowLayer),
                InsetOverlay = c => TemplatePart(c, InsetOverlay)
            });
            RegisterSurface(new DaisyNeumorphicSurface<DaisyAvatar>
            {
                Resolve = c => TemplatePart(c, "Mask"),
                OuterShadowLayer = c => TemplatePart(c, ShadowLayer)
            });
            RegisterSurface(new DaisyNeumorphicSurface<DaisyCountdown> { Resolve = c => TemplatePart(c, SurfacePart), RequiresBackground = true });
            RegisterSurface<DaisyNumberFlow>(c => TemplatePart(c, "OuterContainer"));
            // Key caps have uneven border thickness, which draws no BoxShadow; the shadows go to plain layers.
            foreach (var key in new[] { "ShiftKey", "CtrlKey", "AltKey", "CapsLockKey", "NumLockKey", "ScrollLockKey" })
            {
                RegisterSurface(new DaisyNeumorphicSurface<DaisyModifierKeys>
                {
                    Resolve = c => TemplatePart(c, key),
                    OuterShadowLayer = c => TemplatePart(c, key + "Shadow"),
                    InsetOverlay = c => TemplatePart(c, key + "Inset")
                });
            }
            RegisterSurface(new DaisyNeumorphicSurface<DaisySlideToConfirm>
            {
                Resolve = c => TemplatePart(c, "PART_Track"),
                FixedMode = DaisyNeumorphicMode.Inset
            });
            RegisterSurface(new DaisyNeumorphicSurface<DaisySlideToConfirm>
            {
                Resolve = c => TemplatePart(c, "PART_Handle"),
                FixedMode = DaisyNeumorphicMode.Raised
            });

            // Selection controls: wells are always inset, the toggle knob always raised.
            RegisterSurface(new DaisyNeumorphicSurface<DaisyToggle>
            {
                Resolve = c => TemplatePart(c, "SwitchArea"),
                FixedMode = DaisyNeumorphicMode.Inset
            });
            RegisterSurface(new DaisyNeumorphicSurface<DaisyToggle>
            {
                Resolve = c => TemplatePart(c, "PART_KnobShadow"),
                FixedMode = DaisyNeumorphicMode.Raised
            });
            RegisterSurface(new DaisyNeumorphicSurface<DaisyCheckBox>
            {
                Resolve = c => TemplatePart(c, "PART_Border"),
                FixedMode = DaisyNeumorphicMode.Inset
            });
            RegisterSurface(new DaisyNeumorphicSurface<DaisyRadio>
            {
                Resolve = c => TemplatePart(c, "PART_Border"),
                FixedMode = DaisyNeumorphicMode.Inset
            });

            // Inputs and pickers. DaisyTextArea inherits the DaisyInput registration.
            RegisterSurface<DaisyInput>(c => TemplatePart(c, "PART_Background"));
            RegisterSurface<DaisyMaskInput>(c => TemplatePart(c, "PART_Background"));
            RegisterSurface<DaisyPasswordBox>(c => TemplatePart(c, "PART_Background"));
            RegisterSurface<DaisyNumericUpDown>(c => TemplatePart(c, "PART_Background"));
            RegisterSurface(new DaisyNeumorphicSurface<DaisyFileInput>
            {
                Resolve = c => TemplatePart(c, SurfacePart),
                OuterShadowLayer = c => TemplatePart(c, ShadowLayer)
            });
            RegisterSurface<DaisyTagPicker>(c => TemplatePart(c, "PART_SelectedContainer"));
            RegisterSurface<DaisySelect>(c => TemplatePart(c, "Background"));
            RegisterSurface<DaisySizeDropdown>(c => TemplatePart(c, "Background"));
            RegisterSurface<DaisyThemeDropdown>(c => TemplatePart(c, "Background"));
            RegisterSurface<DaisyProductThemeDropdown>(c => TemplatePart(c, "Background"));
            RegisterSurface<DaisyCalendarDatePicker>(c => TemplatePart(c, "Background"));
            RegisterSurface<DaisyDropdown>(c => TemplatePart(c, "PART_Trigger"));
            RegisterSurface<DaisyRange>(c => TemplatePart(c, "TrackBackground"));
            RegisterSurface(new DaisyNeumorphicSurface<DaisyProgress>
            {
                Resolve = c => TemplatePart(c, "PART_Track"),
                OuterShadowLayer = c => TemplatePart(c, ShadowLayer)
            });
            RegisterSurface<DaisySkeleton>(c => TemplatePart(c, "PART_BackgroundBorder"));
            RegisterSurface<DaisyDateTimelineItem>(c => TemplatePart(c, "PART_Background"));
            RegisterSurface(new DaisyNeumorphicSurface<DaisyTabs>
            {
                Resolve = c => TemplatePart(c, "PART_HeaderContainer"),
                RequiresBackground = true
            });

            // Composite controls: the container is the surface; rows, cells and segments stay flat.
            RegisterSurface(new DaisyNeumorphicSurface<DaisyTable>
            {
                Resolve = c => TemplatePart(c, SurfacePart),
                OuterShadowLayer = c => TemplatePart(c, ShadowLayer),
                InsetOverlay = c => TemplatePart(c, InsetOverlay)
            });
            RegisterExclusion<DaisyTableRow>();
            RegisterExclusion<DaisyTableCell>();
            RegisterExclusion<DaisyTableHeaderCell>();
            RegisterSurface(new DaisyNeumorphicSurface<DaisyList>
            {
                Resolve = c => TemplatePart(c, SurfacePart),
                OuterShadowLayer = c => TemplatePart(c, ShadowLayer)
            });
            RegisterExclusion<DaisyListRow>();
            RegisterExclusion<DaisyListColumn>();
            RegisterSurface(new DaisyNeumorphicSurface<DaisyStats>
            {
                Resolve = c => TemplatePart(c, SurfacePart),
                OuterShadowLayer = c => TemplatePart(c, ShadowLayer)
            });
            RegisterEffectSuppression<DaisyStats>();
            RegisterSurface(new DaisyNeumorphicSurface<DaisyButtonGroup>
            {
                Resolve = c => TemplatePart(c, "PART_Background"),
                OuterShadowLayer = c => TemplatePart(c, ShadowLayer)
            });
            RegisterSegmentContainer<DaisyButtonGroup>();
            RegisterSegmentContainer<DaisyJoin>();
            RegisterSegmentContainer<DaisyPagination>();

            // The FAB container has no surface; its action buttons draw the effect themselves.
            RegisterExclusion<DaisyFab>();
        }
    }
}
