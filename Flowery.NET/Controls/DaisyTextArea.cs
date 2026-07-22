using System;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Flowery.Controls
{
    /// <summary>
    /// A TextBox control styled after DaisyUI's Textarea component (multiline).
    /// Extends DaisyInput with textarea-specific features like character counting,
    /// auto-growing, resize control, and action buttons.
    /// </summary>
    public class DaisyTextArea : DaisyInput
    {
        protected override Type StyleKeyOverride => typeof(DaisyTextArea);

        public DaisyTextArea()
        {
            AcceptsReturn = true;
            AcceptsTab = true;
            TextWrapping = TextWrapping.Wrap;
        }

        private DaisyButton? _actionButton;

        protected override void OnApplyTemplate(Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            // Unsubscribe from previous button if any
            if (_actionButton != null)
            {
                _actionButton.Click -= OnActionButtonClick;
            }

            // Find and subscribe to the action button
            _actionButton = e.NameScope.Find<DaisyButton>("PART_ActionButton");
            if (_actionButton != null)
            {
                _actionButton.Click += OnActionButtonClick;
            }
        }

        private void OnActionButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            OnActionButtonClicked();
        }

        #region Character Counter Properties

        /// <summary>
        /// Defines the <see cref="ShowCharacterCount"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowCharacterCountProperty =
            AvaloniaProperty.Register<DaisyTextArea, bool>(nameof(ShowCharacterCount), false);

        /// <summary>
        /// Gets or sets whether to display the character count below the textarea.
        /// When true, shows "X / MaxLength" or just "X characters" if MaxLength is not set.
        /// </summary>
        public bool ShowCharacterCount
        {
            get => GetValue(ShowCharacterCountProperty);
            set => SetValue(ShowCharacterCountProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="CharacterCount"/> property.
        /// </summary>
        public static readonly DirectProperty<DaisyTextArea, int> CharacterCountProperty =
            AvaloniaProperty.RegisterDirect<DaisyTextArea, int>(
                nameof(CharacterCount),
                o => o.CharacterCount);

        private int _characterCount;

        /// <summary>
        /// Gets the current character count for display in the template.
        /// </summary>
        public int CharacterCount
        {
            get => _characterCount;
            private set => SetAndRaise(CharacterCountProperty, ref _characterCount, value);
        }

        #endregion

        #region Auto-Grow Properties

        /// <summary>
        /// Defines the <see cref="IsAutoGrow"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsAutoGrowProperty =
            AvaloniaProperty.Register<DaisyTextArea, bool>(nameof(IsAutoGrow), false);

        /// <summary>
        /// Gets or sets whether the textarea automatically expands its height based on content.
        /// </summary>
        public bool IsAutoGrow
        {
            get => GetValue(IsAutoGrowProperty);
            set => SetValue(IsAutoGrowProperty, value);
        }

        #endregion

        #region Resize Control Properties

        /// <summary>
        /// Defines the <see cref="CanResize"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> CanResizeProperty =
            AvaloniaProperty.Register<DaisyTextArea, bool>(nameof(CanResize), true);

        /// <summary>
        /// Gets or sets whether the textarea can be resized by the user.
        /// When false, hides the resize grip.
        /// </summary>
        public bool CanResize
        {
            get => GetValue(CanResizeProperty);
            set => SetValue(CanResizeProperty, value);
        }

        #endregion

        #region Action Button Properties

        /// <summary>
        /// Defines the <see cref="ActionButtonContent"/> property.
        /// </summary>
        public static readonly StyledProperty<object?> ActionButtonContentProperty =
            AvaloniaProperty.Register<DaisyTextArea, object?>(nameof(ActionButtonContent), null);

        /// <summary>
        /// Gets or sets the content of the action button displayed below the textarea.
        /// When set, a button is displayed below the textarea (e.g., "Submit Feedback").
        /// </summary>
        public object? ActionButtonContent
        {
            get => GetValue(ActionButtonContentProperty);
            set => SetValue(ActionButtonContentProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ActionButtonCommand"/> property.
        /// </summary>
        public static readonly StyledProperty<ICommand?> ActionButtonCommandProperty =
            AvaloniaProperty.Register<DaisyTextArea, ICommand?>(nameof(ActionButtonCommand), null);

        /// <summary>
        /// Gets or sets the command executed when the action button is clicked.
        /// </summary>
        public ICommand? ActionButtonCommand
        {
            get => GetValue(ActionButtonCommandProperty);
            set => SetValue(ActionButtonCommandProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ActionButtonCommandParameter"/> property.
        /// </summary>
        public static readonly StyledProperty<object?> ActionButtonCommandParameterProperty =
            AvaloniaProperty.Register<DaisyTextArea, object?>(nameof(ActionButtonCommandParameter), null);

        /// <summary>
        /// Gets or sets the command parameter for the action button.
        /// </summary>
        public object? ActionButtonCommandParameter
        {
            get => GetValue(ActionButtonCommandParameterProperty);
            set => SetValue(ActionButtonCommandParameterProperty, value);
        }

        /// <summary>
        /// Occurs when the action button is clicked.
        /// </summary>
        public event EventHandler<RoutedEventArgs>? ActionButtonClicked;

        /// <summary>
        /// Raises the <see cref="ActionButtonClicked"/> event.
        /// </summary>
        internal void OnActionButtonClicked()
        {
            ActionButtonClicked?.Invoke(this, new RoutedEventArgs());
        }

        #endregion

        #region Auto-Grow Implementation

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            // Handle Text property changes for character count and auto-grow
            if (change.Property == TextProperty)
            {
                // Update character count
                CharacterCount = Text?.Length ?? 0;

                if (IsAutoGrow)
                {
                    UpdateAutoGrowHeight();
                }
            }

            // Handle IsAutoGrow property changes
            if (change.Property == IsAutoGrowProperty && IsAutoGrow)
            {
                UpdateAutoGrowHeight();
            }
        }

        private double _originalMinHeight;

        private void UpdateAutoGrowHeight()
        {
            // Store original MinHeight on first call
            if (_originalMinHeight == 0)
            {
                _originalMinHeight = MinHeight > 0 ? MinHeight : 80;
            }

            // Measure the text and adjust height
            var text = Text;
            if (string.IsNullOrEmpty(text))
            {
                // Reset to original minimum height
                MinHeight = _originalMinHeight;
                return;
            }

            // Create FormattedText to measure
            var formattedText = new FormattedText(
                text!,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(FontFamily, FontStyle, FontWeight),
                FontSize,
                Foreground);

            // Set max width for wrapping
            formattedText.MaxTextWidth = Bounds.Width > 0 ? Bounds.Width - Padding.Left - Padding.Right : 200;

            // Calculate desired height with padding
            var desiredHeight = formattedText.Height + Padding.Top + Padding.Bottom + 16;
            
            MinHeight = Math.Max(_originalMinHeight, desiredHeight);
        }

        #endregion
    }
}
