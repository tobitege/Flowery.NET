using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Flowery.Services;

namespace Flowery.Controls.Custom
{
    /// <summary>
    /// Displays modifier key states (Shift, Ctrl, Alt, CapsLock, NumLock, ScrollLock).
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyModifierKeys : TemplatedControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyModifierKeys);

        private const double BaseTextFontSize = 12.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 10.0, scaleFactor);
        }

        public static readonly StyledProperty<bool> IsShiftPressedProperty =
            AvaloniaProperty.Register<DaisyModifierKeys, bool>(nameof(IsShiftPressed));

        public static readonly StyledProperty<bool> IsCtrlPressedProperty =
            AvaloniaProperty.Register<DaisyModifierKeys, bool>(nameof(IsCtrlPressed));

        public static readonly StyledProperty<bool> IsAltPressedProperty =
            AvaloniaProperty.Register<DaisyModifierKeys, bool>(nameof(IsAltPressed));

        public static readonly StyledProperty<bool> IsCapsLockOnProperty =
            AvaloniaProperty.Register<DaisyModifierKeys, bool>(nameof(IsCapsLockOn));

        public static readonly StyledProperty<bool> IsNumLockOnProperty =
            AvaloniaProperty.Register<DaisyModifierKeys, bool>(nameof(IsNumLockOn));

        public static readonly StyledProperty<bool> IsScrollLockOnProperty =
            AvaloniaProperty.Register<DaisyModifierKeys, bool>(nameof(IsScrollLockOn));

        public static readonly StyledProperty<bool> ShowShiftProperty =
            AvaloniaProperty.Register<DaisyModifierKeys, bool>(nameof(ShowShift), true);

        public static readonly StyledProperty<bool> ShowCtrlProperty =
            AvaloniaProperty.Register<DaisyModifierKeys, bool>(nameof(ShowCtrl), true);

        public static readonly StyledProperty<bool> ShowAltProperty =
            AvaloniaProperty.Register<DaisyModifierKeys, bool>(nameof(ShowAlt), true);

        public static readonly StyledProperty<bool> ShowCapsLockProperty =
            AvaloniaProperty.Register<DaisyModifierKeys, bool>(nameof(ShowCapsLock), true);

        public static readonly StyledProperty<bool> ShowNumLockProperty =
            AvaloniaProperty.Register<DaisyModifierKeys, bool>(nameof(ShowNumLock), true);

        public static readonly StyledProperty<bool> ShowScrollLockProperty =
            AvaloniaProperty.Register<DaisyModifierKeys, bool>(nameof(ShowScrollLock), false);

        public bool IsShiftPressed
        {
            get => GetValue(IsShiftPressedProperty);
            set => SetValue(IsShiftPressedProperty, value);
        }

        public bool IsCtrlPressed
        {
            get => GetValue(IsCtrlPressedProperty);
            set => SetValue(IsCtrlPressedProperty, value);
        }

        public bool IsAltPressed
        {
            get => GetValue(IsAltPressedProperty);
            set => SetValue(IsAltPressedProperty, value);
        }

        public bool IsCapsLockOn
        {
            get => GetValue(IsCapsLockOnProperty);
            set => SetValue(IsCapsLockOnProperty, value);
        }

        public bool IsNumLockOn
        {
            get => GetValue(IsNumLockOnProperty);
            set => SetValue(IsNumLockOnProperty, value);
        }

        public bool IsScrollLockOn
        {
            get => GetValue(IsScrollLockOnProperty);
            set => SetValue(IsScrollLockOnProperty, value);
        }

        public bool ShowShift
        {
            get => GetValue(ShowShiftProperty);
            set => SetValue(ShowShiftProperty, value);
        }

        public bool ShowCtrl
        {
            get => GetValue(ShowCtrlProperty);
            set => SetValue(ShowCtrlProperty, value);
        }

        public bool ShowAlt
        {
            get => GetValue(ShowAltProperty);
            set => SetValue(ShowAltProperty, value);
        }

        public bool ShowCapsLock
        {
            get => GetValue(ShowCapsLockProperty);
            set => SetValue(ShowCapsLockProperty, value);
        }

        public bool ShowNumLock
        {
            get => GetValue(ShowNumLockProperty);
            set => SetValue(ShowNumLockProperty, value);
        }

        public bool ShowScrollLock
        {
            get => GetValue(ShowScrollLockProperty);
            set => SetValue(ShowScrollLockProperty, value);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                topLevel.KeyDown += OnTopLevelKeyDown;
                topLevel.KeyUp += OnTopLevelKeyUp;
            }

            SyncFromOS();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                topLevel.KeyDown -= OnTopLevelKeyDown;
                topLevel.KeyUp -= OnTopLevelKeyUp;
            }

            base.OnDetachedFromVisualTree(e);
        }

        private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
        {
            UpdateModifierStates(e.KeyModifiers);
            UpdateLockKeyStates(e.Key);
        }

        private void OnTopLevelKeyUp(object? sender, KeyEventArgs e)
        {
            UpdateModifierStates(e.KeyModifiers);
        }

        private void UpdateModifierStates(KeyModifiers modifiers)
        {
            IsShiftPressed = modifiers.HasFlag(KeyModifiers.Shift);
            IsCtrlPressed = modifiers.HasFlag(KeyModifiers.Control);
            IsAltPressed = modifiers.HasFlag(KeyModifiers.Alt);
        }

        private void UpdateLockKeyStates(Key? pressedKey = null)
        {
            if (KeyboardHelper.HasNativeSupport)
            {
                IsCapsLockOn = KeyboardHelper.IsCapsLockOn;
                IsNumLockOn = KeyboardHelper.IsNumLockOn;
                IsScrollLockOn = KeyboardHelper.IsScrollLockOn;
            }
            else
            {
                if (pressedKey == Key.CapsLock)
                    IsCapsLockOn = !IsCapsLockOn;
                else if (pressedKey == Key.NumLock)
                    IsNumLockOn = !IsNumLockOn;
                else if (pressedKey == Key.Scroll)
                    IsScrollLockOn = !IsScrollLockOn;
            }
        }

        private void SyncFromOS()
        {
            if (KeyboardHelper.HasNativeSupport)
            {
                IsShiftPressed = KeyboardHelper.IsShiftPressed;
                IsCtrlPressed = KeyboardHelper.IsCtrlPressed;
                IsAltPressed = KeyboardHelper.IsAltPressed;
                IsCapsLockOn = KeyboardHelper.IsCapsLockOn;
                IsNumLockOn = KeyboardHelper.IsNumLockOn;
                IsScrollLockOn = KeyboardHelper.IsScrollLockOn;
            }
        }
    }
}
