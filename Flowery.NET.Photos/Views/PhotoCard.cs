using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos.Views;

/// <summary>A Daisy card with the same activation behavior for mouse, keyboard and UI Automation.</summary>
public sealed class PhotoCard : DaisyCard
{
    public static readonly RoutedEvent<RoutedEventArgs> ClickEvent =
        RoutedEvent.Register<PhotoCard, RoutedEventArgs>(nameof(Click), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs>? Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    public PhotoCard(string id, string name)
    {
        Classes.Add("photo-card");
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        AutomationProperties.SetAutomationId(this, id);
        AutomationProperties.SetName(this, name);
        Tapped += (_, e) => { Invoke(); e.Handled = true; };
        KeyDown += (_, e) =>
        {
            if (e.Key is not (Key.Enter or Key.Space)) return;
            Invoke();
            e.Handled = true;
        };
    }

    private void Invoke()
    {
        if (IsEffectivelyEnabled) RaiseEvent(new RoutedEventArgs(ClickEvent));
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new CardPeer(this);

    private sealed class CardPeer(PhotoCard owner) : ControlAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
        protected override string GetClassNameCore() => nameof(PhotoCard);
        public void Invoke() { EnsureEnabled(); owner.Invoke(); }
    }
}

/// <summary>Owns one decoded thumbnail only while its image is attached to the visual tree.</summary>
public sealed class PhotoThumbnail : Image
{
    private readonly PhotoItem _item;
    private CancellationTokenSource? _load;
    private Bitmap? _bitmap;

    public PhotoThumbnail(PhotoItem item)
    {
        _item = item;
        Stretch = Stretch.UniformToFill;
        AttachedToVisualTree += Load;
        DetachedFromVisualTree += (_, _) =>
        {
            _load?.Cancel();
            _load = null;
            Source = null;
            _bitmap?.Dispose();
            _bitmap = null;
        };
    }

    private async void Load(object? sender, VisualTreeAttachmentEventArgs e)
    {
        var load = new CancellationTokenSource();
        _load = load;
        try
        {
            var bitmap = await ThumbnailLoader.LoadAsync(_item, 240, load.Token);
            if (_load != load) { bitmap?.Dispose(); return; }
            _bitmap = bitmap;
            Source = bitmap;
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (_load == load) _load = null;
            load.Dispose();
        }
    }
}
