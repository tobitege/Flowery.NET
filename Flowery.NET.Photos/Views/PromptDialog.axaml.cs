using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Input;
using Flowery.NET.Photos.Localization;

namespace Flowery.NET.Photos.Views;

public partial class PromptDialog : Window
{
    public string? Value => Input.Text;
    public PromptDialog() : this(string.Empty, string.Empty) { }
    public PromptDialog(string title, string message, string? value = null, string? alternative = null)
    {
        InitializeComponent();
        Title = title; Message.Text = message;
        AutomationProperties.SetAutomationId(this, "Photos.Prompt");
        AutomationProperties.SetName(Input, title);
        Input.Text = value; Input.IsVisible = value != null;
        CancelButton.Content = PhotosLocalization.GetString("Photos_Cancel");
        AutomationProperties.SetName(CancelButton, PhotosLocalization.GetString("Photos_Cancel"));
        AcceptButton.Content = PhotosLocalization.GetString("Photos_Ok");
        AutomationProperties.SetName(AcceptButton, PhotosLocalization.GetString("Photos_Ok"));
        AlternativeButton.Content = alternative;
        AutomationProperties.SetName(AlternativeButton, alternative ?? string.Empty);
        AlternativeButton.IsVisible = alternative != null;
        CancelButton.Click += (_, _) => Close(0);
        AcceptButton.Click += (_, _) => Close(1);
        AlternativeButton.Click += (_, _) => Close(2);
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(0); };
        Opened += (_, _) => { if (Input.IsVisible) { Input.Focus(); Input.SelectAll(); } };
    }
}
