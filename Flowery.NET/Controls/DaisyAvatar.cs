using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Flowery.Controls
{
    public class DaisyAvatar : ContentControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyAvatar);

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyAvatar, DaisySize>(nameof(Size), DaisySize.Medium);

        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public static readonly StyledProperty<DaisyAvatarShape> ShapeProperty =
            AvaloniaProperty.Register<DaisyAvatar, DaisyAvatarShape>(nameof(Shape), DaisyAvatarShape.Circle);

        public DaisyAvatarShape Shape
        {
            get => GetValue(ShapeProperty);
            set => SetValue(ShapeProperty, value);
        }

        [Obsolete("Use Shape property instead. IsRounded=true maps to Shape=Circle, IsRounded=false maps to Shape=Rounded.")]
        public static readonly StyledProperty<bool> IsRoundedProperty =
            AvaloniaProperty.Register<DaisyAvatar, bool>(nameof(IsRounded), true);

        [Obsolete("Use Shape property instead.")]
        public bool IsRounded
        {
            get => GetValue(IsRoundedProperty);
            set => SetValue(IsRoundedProperty, value);
        }

        public static readonly StyledProperty<DaisyStatus> StatusProperty =
            AvaloniaProperty.Register<DaisyAvatar, DaisyStatus>(nameof(Status), DaisyStatus.None);

        public DaisyStatus Status
        {
            get => GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        public static readonly StyledProperty<bool> IsPlaceholderProperty =
            AvaloniaProperty.Register<DaisyAvatar, bool>(nameof(IsPlaceholder), false);

        public bool IsPlaceholder
        {
            get => GetValue(IsPlaceholderProperty);
            set => SetValue(IsPlaceholderProperty, value);
        }

        public static readonly StyledProperty<bool> HasRingProperty =
            AvaloniaProperty.Register<DaisyAvatar, bool>(nameof(HasRing), false);

        public bool HasRing
        {
            get => GetValue(HasRingProperty);
            set => SetValue(HasRingProperty, value);
        }

        public static readonly StyledProperty<DaisyColor> RingColorProperty =
            AvaloniaProperty.Register<DaisyAvatar, DaisyColor>(nameof(RingColor), DaisyColor.Primary);

        public DaisyColor RingColor
        {
            get => GetValue(RingColorProperty);
            set => SetValue(RingColorProperty, value);
        }
    }

    public enum DaisyStatus
    {
        None,
        Online,
        Offline
    }

    public enum DaisyAvatarShape
    {
        Square,
        Rounded,
        Circle
    }

    public enum DaisyColor
    {
        Primary,
        Secondary,
        Accent,
        Neutral,
        Info,
        Success,
        Warning,
        Error
    }
}
