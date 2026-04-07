using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;

namespace Flowery.Controls
{
    /// <summary>
    /// A DaisyButton that copies text to the clipboard and briefly shows a success state.
    /// </summary>
    public class DaisyCopyButton : DaisyButton
    {
        private bool _isBusy;
        private object? _idleContent;

        /// <summary>
        /// Defines the <see cref="CopyText"/> property.
        /// </summary>
        public static readonly StyledProperty<string?> CopyTextProperty =
            AvaloniaProperty.Register<DaisyCopyButton, string?>(nameof(CopyText), null);

        /// <summary>
        /// Gets or sets the text copied to clipboard when clicked.
        /// </summary>
        public string? CopyText
        {
            get => GetValue(CopyTextProperty);
            set => SetValue(CopyTextProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="SuccessDuration"/> property.
        /// </summary>
        public static readonly StyledProperty<TimeSpan> SuccessDurationProperty =
            AvaloniaProperty.Register<DaisyCopyButton, TimeSpan>(nameof(SuccessDuration), TimeSpan.FromSeconds(2));

        /// <summary>
        /// Gets or sets how long the success state is shown.
        /// </summary>
        public TimeSpan SuccessDuration
        {
            get => GetValue(SuccessDurationProperty);
            set => SetValue(SuccessDurationProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="SuccessContent"/> property.
        /// </summary>
        public static readonly StyledProperty<object?> SuccessContentProperty =
            AvaloniaProperty.Register<DaisyCopyButton, object?>(nameof(SuccessContent), "Copied");

        /// <summary>
        /// Gets or sets the content displayed while in success state.
        /// </summary>
        public object? SuccessContent
        {
            get => GetValue(SuccessContentProperty);
            set => SetValue(SuccessContentProperty, value);
        }

        public DaisyCopyButton()
        {
            if (Content == null)
            {
                Content = "Copy";
            }
        }

        protected override async void OnClick()
        {
            if (_isBusy) return;

            base.OnClick();

            _isBusy = true;

            _idleContent ??= Content;
            var idleEnabled = IsEnabled;

            try
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(CopyText ?? string.Empty);
                }

                Content = SuccessContent;
                IsEnabled = false;

                await Task.Delay(SuccessDuration);
            }
            finally
            {
                Content = _idleContent;
                IsEnabled = idleEnabled;
                _isBusy = false;
            }
        }
    }
}
