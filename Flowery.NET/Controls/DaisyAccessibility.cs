using Avalonia;
using Avalonia.Automation;
using Avalonia.Styling;

namespace Flowery.Controls
{
    /// <summary>
    /// Provides shared accessibility functionality for Daisy controls.
    /// Use the attached <see cref="AccessibleTextProperty"/> to set screen reader text,
    /// and call <see cref="SetupAccessibility"/> in the control's static constructor.
    /// </summary>
    public static class DaisyAccessibility
    {
        /// <summary>
        /// Attached property for accessible text announced by screen readers.
        /// </summary>
        public static readonly AttachedProperty<string?> AccessibleTextProperty =
            AvaloniaProperty.RegisterAttached<AvaloniaObject, string?>(
                "AccessibleText",
                typeof(DaisyAccessibility));

        /// <summary>
        /// Gets the accessible text for the specified control.
        /// </summary>
        public static string? GetAccessibleText(AvaloniaObject obj)
        {
            return obj.GetValue(AccessibleTextProperty);
        }

        /// <summary>
        /// Sets the accessible text for the specified control.
        /// </summary>
        public static void SetAccessibleText(AvaloniaObject obj, string? value)
        {
            obj.SetValue(AccessibleTextProperty, value);
        }

        /// <summary>
        /// Sets up accessibility for a control type. Call this in the control's static constructor.
        /// Registers a property changed handler that syncs AccessibleText to AutomationProperties.Name.
        /// </summary>
        /// <typeparam name="T">The control type.</typeparam>
        /// <param name="defaultText">The default accessible text for this control type.</param>
        public static void SetupAccessibility<T>(string defaultText) where T : StyledElement
        {
            AutomationProperties.NameProperty.OverrideDefaultValue<T>(defaultText);

            AccessibleTextProperty.Changed.AddClassHandler<T>((control, e) =>
            {
                var newValue = e.GetNewValue<string?>();
                AutomationProperties.SetName(control, newValue ?? defaultText);
            });
        }

        /// <summary>
        /// Gets the effective accessible text for a control, falling back to the default if not set.
        /// </summary>
        /// <param name="control">The control to get text for.</param>
        /// <param name="defaultText">The default text if AccessibleText is null.</param>
        /// <returns>The accessible text to announce.</returns>
        public static string GetEffectiveAccessibleText(AvaloniaObject control, string defaultText)
        {
            return GetAccessibleText(control) ?? defaultText;
        }
    }
}
