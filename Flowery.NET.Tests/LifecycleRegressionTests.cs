using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Effects;
using FlowerySlideEffect = Flowery.Enums.FlowerySlideEffect;
using Flowery.Helpers;
using Flowery.Localization;
using Xunit;

namespace Flowery.NET.Tests;

public class LifecycleRegressionTests
{
    [AvaloniaFact]
    public void GlobalLocalizationAndSizingDoNotKeepRemovedTargetsAlive()
    {
        _ = FlowerySizeManager.CurrentSize;
        var references = CreateDiscardedTargets();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var retained = references.Select(reference => reference.Target).Where(target => target != null)
            .Select(target => target is Control control
                ? $"{control.GetType().Name}: Loaded={control.IsLoaded}, Focused={control.IsFocused}, Attached={control.IsAttachedToVisualTree()}"
                : target!.GetType().FullName)
            .ToArray();
        Assert.Empty(retained);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] CreateDiscardedTargets()
    {
        var text = new TextBlock();
        var extension = new LocalizeExtension("Size_Small");
        text.Text = (string)extension.ProvideValue(new TargetServices(text));
        var button = new DaisyButton();
        var sidebar = new FloweryComponentSidebar
        {
            Categories = new System.Collections.ObjectModel.ObservableCollection<SidebarCategory>()
        };
        var category = new SidebarCategory { Name = "Category" };
        var item = new SidebarItem { Name = "Item" };
        category.Items.Add(item);
        sidebar.Categories.Add(category);
        var window = new Window { Content = new StackPanel { Children = { button, sidebar } } };
        window.Show();
        Flush(window);
        window.Content = null;
        Flush(window);
        window.Close();
        Dispatcher.UIThread.RunJobs();
        return [new(text), new(button), new(sidebar), new(category), new(item)];
    }

    [AvaloniaFact]
    public async Task LocalizationFromWorkerUpdatesTargetOnUiThread()
    {
        var original = FloweryLocalization.CurrentCulture;
        var text = new TextBlock();
        try
        {
            FloweryLocalization.SetCulture("en");
            text.Text = (string)new LocalizeExtension("Size_Small").ProvideValue(new TargetServices(text));
            var allUpdatesOnUiThread = true;
            text.PropertyChanged += (_, args) =>
            {
                if (args.Property == TextBlock.TextProperty)
                    allUpdatesOnUiThread &= Dispatcher.UIThread.CheckAccess();
            };
            await Task.Run(() => FloweryLocalization.SetCulture("de"));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(FloweryLocalization.GetString("Size_Small"), text.Text);
            Assert.True(allUpdatesOnUiThread);
        }
        finally { FloweryLocalization.SetCulture(original); }
    }

    [AvaloniaFact]
    public void ResponsiveFontReregistersWhenTextReturnsToVisualTree()
    {
        var previous = FlowerySizeManager.CurrentSize;
        var text = new TextBlock { Text = "Responsive" };
        FlowerySizeManager.SetResponsiveFont(text, ResponsiveFontTier.Primary);
        var window = new Window { Content = text };
        try
        {
            FlowerySizeManager.ApplySize(DaisySize.Small);
            window.Show();
            Flush(window);
            var small = text.FontSize;
            window.Content = null;
            Flush(window);
            FlowerySizeManager.ApplySize(DaisySize.ExtraLarge);
            window.Content = text;
            Flush(window);
            Assert.True(text.FontSize > small);
            FlowerySizeManager.ApplySize(DaisySize.Small);
            Assert.Equal(small, text.FontSize);
        }
        finally { window.Close(); FlowerySizeManager.ApplySize(previous); }
    }

    [AvaloniaFact]
    public async Task RevealCancellationRestoresOriginalTransformAndOpacity()
    {
        var original = new RotateTransform(15);
        var border = new Border { Width = 80, Height = 80, RenderTransform = original, Opacity = .6 };
        RevealBehavior.SetManualTriggerOnly(border, true);
        RevealBehavior.SetDuration(border, TimeSpan.FromMilliseconds(80));
        RevealBehavior.SetIsEnabled(border, true);
        var window = new Window { Content = border };
        window.Show();
        Flush(window);
        try
        {
            RevealBehavior.TriggerReveal(border);
            window.Content = null;
            Assert.Same(original, border.RenderTransform);
            Assert.Equal(.6, border.Opacity);
            await Frames();
            Assert.Same(original, border.RenderTransform);
            Assert.Equal(.6, border.Opacity);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ScrollRevealRemainsVisibleAfterOneShotAndReattachment()
    {
        var border = new Border { Width = 80, Height = 80 };
        RevealBehavior.SetManualTriggerOnly(border, true);
        RevealBehavior.SetDuration(border, TimeSpan.Zero);
        RevealBehavior.SetIsEnabled(border, true);
        ScrollRevealBehavior.SetIsEnabled(border, true);
        var viewer = new ScrollViewer { Content = border };
        var window = new Window { Content = viewer, Width = 300, Height = 300 };
        window.Show();
        Flush(window);
        try
        {
            await Frames();
            Assert.Equal(1, border.Opacity);
            viewer.Content = null;
            viewer.Content = border;
            Flush(window);
            Assert.Equal(1, border.Opacity);
            ScrollRevealBehavior.SetIsEnabled(border, false);
            ScrollRevealBehavior.SetIsEnabled(border, true);
            await Frames();
            Assert.Equal(1, border.Opacity);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task StoppedSlideEffectCannotApplyQueuedOrLaterFrames()
    {
        var border = new Border { Width = 80, Height = 80 };
        var window = new Window { Content = border };
        window.Show();
        Flush(window);
        try
        {
            FlowerySlideEffects.SetAutoStart(border, false);
            FlowerySlideEffects.SetEffect(border, FlowerySlideEffect.ZoomIn);
            FlowerySlideEffects.SetDuration(border, .08);
            FlowerySlideEffects.StartEffect(border);
            FlowerySlideEffects.StopEffect(border);
            Dispatcher.UIThread.RunJobs();
            Assert.True(border.RenderTransform == null || border.RenderTransform.Value.IsIdentity);
            FlowerySlideEffects.StartEffect(border);
            Dispatcher.UIThread.RunJobs();
            FlowerySlideEffects.StopEffect(border);
            await Frames();
            Assert.True(border.RenderTransform == null || border.RenderTransform.Value.IsIdentity);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task NumberFlowDetachedPartsStopAndShowTheirFinalValue()
    {
        var number = new DaisyNumberFlow { Value = 10, Duration = TimeSpan.FromMilliseconds(80) };
        var window = new Window { Content = number };
        window.Show();
        Flush(window);
        try
        {
            number.Value = 12;
            var part = number.Parts.Last();
            window.Content = null;
            Assert.False(part.IsCurrentlyAnimating);
            Assert.Equal(0, part.CurrentOffset);
            Assert.Equal(1, part.Opacity);
            await Frames();
            Assert.False(part.IsCurrentlyAnimating);
            Assert.Equal(0, part.CurrentOffset);
            number.Value = 13;
            Assert.All(number.Parts, item => Assert.False(item.IsCurrentlyAnimating));
            window.Content = number;
            Flush(window);
            Assert.Equal("13", string.Concat(number.Parts.Select(item => item.Character)));
        }
        finally { window.Close(); }
    }

    private static Task Frames() => AnimationHelper.AnimateAsync(
        static _ => { }, TimeSpan.FromMilliseconds(120), new LinearEasing());

    private static void Flush(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class TargetServices(TextBlock target) : IServiceProvider, IProvideValueTarget
    {
        public object TargetObject => target;
        public object TargetProperty => TextBlock.TextProperty;
        public object? GetService(Type serviceType) => serviceType == typeof(IProvideValueTarget) ? this : null;
    }
}
