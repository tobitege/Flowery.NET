using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos.Views;

public partial class PhotoTile : UserControl
{
    private CancellationTokenSource? _load;
    private Bitmap? _image;
    private PhotoItem? _item;
    public PhotoItem Item => _item ?? throw new InvalidOperationException();
    public event Action<PhotoItem, KeyModifiers, bool>? Activated;
    public event Action? SelectionChanged;

    public PhotoTile() { InitializeComponent(); }
    public PhotoTile(PhotoItem item, double width, double height, bool showName = true) : this()
    {
        _item = item;
        Width = width; Height = height;
        Focusable = true;
        NameLabel.Text = item.FileName;
        NameLabel.IsVisible = showName;
        NameLabel.Parent!.SetValue(IsVisibleProperty, showName);
        ToolTip.SetTip(this, item.FileName);
        Avalonia.Automation.AutomationProperties.SetName(this, item.FileName);
        var identity = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(item.Path)))[..16];
        AutomationProperties.SetAutomationId(this, "Photos.Tile." + identity);
        AutomationProperties.SetAutomationId(Selection, "Photos.Tile.Select." + identity);
        AutomationProperties.SetName(Selection, Localization.PhotosLocalization.GetString("Photos_Select") + " " + item.FileName);
        Selection.IsCheckedChanged += (_, _) =>
        {
            var selected = Selection.IsChecked == true;
            if (item.IsSelected == selected) return;
            item.IsSelected = selected;
            SelectionChanged?.Invoke();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key is not (Key.Enter or Key.Space)) return;
            Activated?.Invoke(item, e.KeyModifiers, e.Key == Key.Space);
            e.Handled = true;
        };
        AttachedToVisualTree += async (_, _) =>
        {
            _item.PropertyChanged += OnItemChanged;
            UpdateState();
            var load = new CancellationTokenSource();
            _load = load;
            try
            {
                var image = await ThumbnailLoader.LoadAsync(item, height, load.Token);
                if (_load != load) { image?.Dispose(); return; }
                _image = image; Photo.Source = image;
            }
            catch (OperationCanceledException) { }
            finally { load.Dispose(); if (_load == load) _load = null; }
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _item.PropertyChanged -= OnItemChanged;
            _load?.Cancel(); _load = null;
            Photo.Source = null; _image?.Dispose(); _image = null;
        };
        PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            Activated?.Invoke(item, e.KeyModifiers, e.GetPosition(this).Y < 45 && e.GetPosition(this).X > Bounds.Width - 45);
            e.Handled = true;
        };
    }
    private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => UpdateState();
    private void UpdateState()
    {
        Selection.IsChecked = Item.IsSelected;
        Heart.IsVisible = Item.IsFavorite;
        Frame.BorderBrush = Item.IsSelected
            ? this.FindResource("PhotosAccentBrush") as IBrush ?? Brushes.DodgerBlue : Brushes.Transparent;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PhotoTilePeer(this);

    private sealed class PhotoTilePeer(PhotoTile owner) : ControlAutomationPeer(owner), IInvokeProvider, IToggleProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
        protected override string GetClassNameCore() => nameof(PhotoTile);
        protected override string? GetNameCore() => owner._item?.FileName;
        public ToggleState ToggleState => owner._item?.IsSelected == true ? ToggleState.On : ToggleState.Off;
        public void Invoke()
        {
            EnsureEnabled();
            if (owner._item is { } item) owner.Activated?.Invoke(item, KeyModifiers.None, false);
        }
        public void Toggle()
        {
            EnsureEnabled();
            if (owner._item is not { } item) return;
            item.IsSelected = !item.IsSelected;
            owner.SelectionChanged?.Invoke();
        }
    }
}
