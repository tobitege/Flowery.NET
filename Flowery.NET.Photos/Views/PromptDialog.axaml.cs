using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Input;
using Flowery.NET.Photos.Localization;

namespace Flowery.NET.Photos.Views;

public partial class PromptDialog : Window
{
    public string? Value => Input.Text;
    public PromptDialog() : this(string.Empty, string.Empty) { }
    public PromptDialog(string title, string message, string? value = null, string? alternative = null,
        string? accept = null, bool destructive = false)
    {
        InitializeComponent();
        Title = title; Message.Text = message;
        AutomationProperties.SetAutomationId(this, "Photos.Prompt");
        AutomationProperties.SetName(Input, title);
        Input.Text = value; Input.IsVisible = value != null;
        CancelButton.Content = PhotosLocalization.GetString("Photos_Cancel");
        AutomationProperties.SetName(CancelButton, PhotosLocalization.GetString("Photos_Cancel"));
        AcceptButton.Content = accept ?? PhotosLocalization.GetString("Photos_Ok");
        AutomationProperties.SetName(AcceptButton, AcceptButton.Content.ToString()!);
        AcceptButton.Variant = destructive ? DaisyButtonVariant.Error : DaisyButtonVariant.Primary;
        AlternativeButton.Content = alternative;
        AutomationProperties.SetName(AlternativeButton, alternative ?? string.Empty);
        AlternativeButton.IsVisible = alternative != null;
        CancelButton.Click += (_, _) => Close(0);
        AcceptButton.Click += (_, _) => Close(1);
        AlternativeButton.Click += (_, _) => Close(2);
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { e.Handled = true; Close(0); }
            else if (e.Key == Key.Enter && AcceptButton.IsEnabled) { e.Handled = true; Close(1); }
        };
        Input.TextChanged += (_, _) => AcceptButton.IsEnabled = !Input.IsVisible || !string.IsNullOrWhiteSpace(Input.Text);
        AcceptButton.IsEnabled = !Input.IsVisible || !string.IsNullOrWhiteSpace(Input.Text);
        Opened += (_, _) =>
        {
            if (Owner is { } owner)
            {
                MaxWidth = Math.Max(240, owner.Bounds.Width - 48);
                if (owner.TryFindResource("PhotosAccentBrush", out var accent)) Resources["PhotosAccentBrush"] = accent;
            }
            if (Input.IsVisible) { Input.Focus(); Input.SelectAll(); }
        };
    }
}
