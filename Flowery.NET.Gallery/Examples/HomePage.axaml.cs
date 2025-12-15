using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Flowery.Controls;

namespace Flowery.NET.Gallery.Examples;

public partial class HomePage : UserControl
{
    public event EventHandler? BrowseComponentsRequested;

    public HomePage()
    {
        InitializeComponent();

        // Subscribe to global size changes
        FlowerySizeManager.SizeChanged += OnGlobalSizeChanged;

        // Apply initial size
        ApplySizeToLayout(FlowerySizeManager.CurrentSize);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        FlowerySizeManager.SizeChanged -= OnGlobalSizeChanged;
    }

    private void OnGlobalSizeChanged(object? sender, DaisySize size)
    {
        ApplySizeToLayout(size);
    }

    private void ApplySizeToLayout(DaisySize size)
    {
        // Calculate scale factor based on size
        var (paddingScale, emojiScale, cardWidth, cardImageHeight) = size switch
        {
            DaisySize.ExtraSmall => (0.6, 0.6, 280.0, 80.0),
            DaisySize.Small => (0.8, 0.8, 320.0, 100.0),
            DaisySize.Medium => (1.0, 1.0, 380.0, 120.0),
            DaisySize.Large => (1.2, 1.25, 440.0, 140.0),
            DaisySize.ExtraLarge => (1.4, 1.5, 500.0, 160.0),
            _ => (1.0, 1.0, 380.0, 120.0)
        };

        // Hero section padding and emoji
        if (HeroSection != null)
            HeroSection.Padding = new Thickness(40 * paddingScale, 60 * paddingScale);

        if (HeroEmoji != null)
            HeroEmoji.FontSize = 64 * emojiScale;

        // About section padding
        if (AboutSection != null)
            AboutSection.Padding = new Thickness(40 * paddingScale, 50 * paddingScale);

        if (AboutContent != null)
            AboutContent.MaxWidth = 800 * paddingScale;

        // Thanks section padding and emoji
        if (ThanksSection != null)
            ThanksSection.Padding = new Thickness(40 * paddingScale, 50 * paddingScale);

        if (ThanksContent != null)
            ThanksContent.MaxWidth = 900 * paddingScale;

        if (ThanksEmoji != null)
            ThanksEmoji.FontSize = 48 * emojiScale;

        // Cards
        if (FloweryCard != null)
            FloweryCard.Width = cardWidth;

        if (FloweryCardImage != null)
            FloweryCardImage.Height = cardImageHeight;

        if (FloweryCardEmoji != null)
            FloweryCardEmoji.FontSize = 48 * emojiScale;

        if (AvaloniaCard != null)
            AvaloniaCard.Width = cardWidth;

        if (AvaloniaCardImage != null)
            AvaloniaCardImage.Height = cardImageHeight;

        if (AvaloniaCardEmoji != null)
            AvaloniaCardEmoji.FontSize = 56 * emojiScale;

        // Footer
        if (FooterSection != null)
            FooterSection.Padding = new Thickness(30 * paddingScale);
    }

    public void BrowseBtn_Click(object? sender, RoutedEventArgs e)
    {
        BrowseComponentsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OpenUrl(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Process.Start("xdg-open", url);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", url);
    }

    public void GitHubBtn_Click(object? sender, RoutedEventArgs e)
    {
        OpenUrl("https://github.com/tobitege/Flowery.NET");
    }

    public void DaisyUIBtn_Click(object? sender, RoutedEventArgs e)
    {
        OpenUrl("https://daisyui.com");
    }

    public void AvaloniaBtn_Click(object? sender, RoutedEventArgs e)
    {
        OpenUrl("https://avaloniaui.net");
    }
}
