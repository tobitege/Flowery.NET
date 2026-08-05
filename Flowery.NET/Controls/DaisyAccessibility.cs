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
            if (GetAccessibleText(control) is { Length: > 0 } accessibleText)
            {
                return accessibleText;
            }

            if (control is StyledElement styledElement
                && styledElement.IsSet(AutomationProperties.NameProperty)
                && AutomationProperties.GetName(styledElement) is { Length: > 0 } automationName)
            {
                return automationName;
            }

            return defaultText;
        }

        /// <summary>
        /// Copies the form-related automation metadata from a composite control to one of its
        /// focusable template children. An explicit name and a child ID suffix can be supplied.
        /// </summary>
        /// <param name="source">The composite control that owns the public automation metadata.</param>
        /// <param name="target">The focusable template child exposed to UI Automation.</param>
        /// <param name="name">An optional accessible name for the child.</param>
        /// <param name="helpText">Optional help text; the source help text is used when omitted.</param>
        /// <param name="automationIdSuffix">Optional suffix appended to the source automation ID.</param>
        public static void ApplyAutomationProperties(
            StyledElement source,
            StyledElement target,
            string? name = null,
            string? helpText = null,
            string? automationIdSuffix = null)
        {
            target.SetValue(
                AutomationProperties.NameProperty,
                name ?? AutomationProperties.GetName(source));
            target.SetValue(
                AutomationProperties.HelpTextProperty,
                helpText ?? AutomationProperties.GetHelpText(source));
            target.SetValue(
                AutomationProperties.LabeledByProperty,
                AutomationProperties.GetLabeledBy(source));
            target.SetValue(
                AutomationProperties.IsRequiredForFormProperty,
                AutomationProperties.GetIsRequiredForForm(source));

            var automationId = AutomationProperties.GetAutomationId(source);
            if (!string.IsNullOrWhiteSpace(automationId)
                && !string.IsNullOrWhiteSpace(automationIdSuffix))
            {
                automationId = $"{automationId}-{automationIdSuffix}";
            }

            target.SetValue(AutomationProperties.AutomationIdProperty, automationId);
        }
    }
}
