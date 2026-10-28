using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Flowery.Enums;
using Flowery.Helpers;

namespace Flowery.Controls;

public sealed partial class DaisyTour
{
    // The move grip: arrows in four directions.
    private const string MoveCardIconPath = "M13,6V11H18V7.75L22.25,12L18,16.25V13H13V18H16.25L12,22.25L7.75,18H11V13H6V16.25L1.75,12L6,7.75V11H11V6H7.75L12,1.75L16.25,6H13Z";

    private readonly DaisyButton _cardMove = new()
    {
        Content = new PathIcon { Data = Geometry.Parse(MoveCardIconPath), Width = 16, Height = 16 },
        Variant = DaisyButtonVariant.Ghost, Shape = DaisyButtonShape.Square, Size = DaisySize.Small,
        IsVisible = false, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(8, 0, 0, 0)
    };
    private readonly DaisyButton _cardSave = new()
    {
        IconSymbol = DaisyIconSymbol.Save, Variant = DaisyButtonVariant.Ghost,
        Shape = DaisyButtonShape.Square, Size = DaisySize.Small, IsEnabled = false,
        IsVisible = false, VerticalAlignment = VerticalAlignment.Top
    };
    private readonly DaisyButton _cardToggle = new()
    {
        Content = new PathIcon { Width = 16, Height = 16 }, Variant = DaisyButtonVariant.Ghost,
        Shape = DaisyButtonShape.Square, Size = DaisySize.Small,
        IsVisible = false, VerticalAlignment = VerticalAlignment.Top
    };
    private bool _cardCollapsed;
    private (IPointer Pointer, Point PointerOrigin, Point CardOrigin, Point? PreviousPosition)? _cardDrag;
    private Point? _temporaryCardPosition;
    private Rect _configuredCardBounds;
    private Rect _intendedCardBounds;
    internal bool IsCardDragging => _cardDrag is not null;
    private bool CardPositionChanged => _temporaryCardPosition is not null &&
        (Math.Abs(_intendedCardBounds.X - _configuredCardBounds.X) >= 0.5 || Math.Abs(_intendedCardBounds.Y - _configuredCardBounds.Y) >= 0.5);

    // Chevron up folds the card, chevron down unfolds it. Both are parsed once per tour, so a refresh that sets the
    // same icon changes nothing; static Avalonia objects would be shared across UI threads.
    private readonly Geometry? _foldCardIcon = FloweryPathHelpers.GetIconGeometry("DaisyIconChevronUp");
    private readonly Geometry? _unfoldCardIcon = FloweryPathHelpers.GetIconGeometry("DaisyIconChevronDown");

    private Geometry? CardToggleIcon(bool collapsed) => collapsed ? _unfoldCardIcon : _foldCardIcon;

    private Grid CreateCardHeader()
    {
        FlowerySizeManager.SetIgnoreGlobalSize(_cardMove, true);
        FlowerySizeManager.SetIgnoreGlobalSize(_cardSave, true);
        FlowerySizeManager.SetIgnoreGlobalSize(_cardToggle, true);
        AutomationProperties.SetAutomationId(_cardMove, "DaisyTourMoveCard");
        AutomationProperties.SetAutomationId(_cardSave, "DaisyTourSaveCard");
        AutomationProperties.SetAutomationId(_cardToggle, "DaisyTourToggleCard");
        if (_cardToggle.Content is PathIcon toggleIcon) toggleIcon.Data = CardToggleIcon(false);
        _cardToggle.Click += OnCardToggle;
        _cardSave.Click += OnCardSave;
        _cardMove.AddHandler(PointerPressedEvent, OnCardMovePressed, RoutingStrategies.Bubble, handledEventsToo: true);
        _cardMove.AddHandler(PointerMovedEvent, OnCardMoveMoved, RoutingStrategies.Bubble, handledEventsToo: true);
        _cardMove.AddHandler(PointerReleasedEvent, OnCardMoveFinished, RoutingStrategies.Bubble, handledEventsToo: true);
        // Avalonia has no canceled pointer event; a capture lost without a release cancels the move.
        _cardMove.AddHandler(PointerCaptureLostEvent, OnCardMoveCanceled, RoutingStrategies.Direct, handledEventsToo: true);
        _cardMove.KeyDown += OnCardMoveKeyDown;
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(_title);
        Grid.SetColumn(_cardMove, 1);
        header.Children.Add(_cardMove);
        Grid.SetColumn(_cardSave, 2);
        header.Children.Add(_cardSave);
        Grid.SetColumn(_cardToggle, 3);
        header.Children.Add(_cardToggle);
        return header;
    }

    private void OnCardToggle(object? sender, RoutedEventArgs args)
    {
        if (_editor is not { } editor || _session is not { Busy: false } session) return;
        editor.CancelGesture();
        CancelCardDrag();
        _cardCollapsed = !_cardCollapsed;
        RefreshPresentation();
        UpdateGeometry(session);
        _cardToggle.Focus();
    }

    private void SetCardCompact(bool compact)
    {
        var visible = !compact;
        _description.IsVisible = _customContent.IsVisible = _count.IsVisible = _buttons.IsVisible = visible;
        _progressScroll.IsVisible = visible;
        _title.TextWrapping = compact ? TextWrapping.NoWrap : TextWrapping.Wrap;
        _title.TextTrimming = compact ? TextTrimming.CharacterEllipsis : TextTrimming.None;
        _cardLayout.RowSpacing = compact ? 0 : 12;
        if (compact)
        {
            _card.Width = Math.Min(360, _card.Width);
            _card.Padding = new Thickness(8);
        }
    }

    private void OnCardMovePressed(object? sender, PointerPressedEventArgs args)
    {
        if (_editor is not { } editor || _session is not { Busy: false } ||
            !args.GetCurrentPoint(_surface).Properties.IsLeftButtonPressed) return;
        editor.CancelGesture();
        _cardDrag = (args.Pointer, args.GetPosition(_surface), new Point(CardBounds.X, CardBounds.Y), _temporaryCardPosition);
        args.Pointer.Capture(_cardMove);
        if (args.Pointer.Captured != _cardMove)
        {
            ReleaseCardDrag();
            return;
        }
        _cardMove.Focus(NavigationMethod.Pointer);
        args.Handled = true;
    }

    private void OnCardMoveMoved(object? sender, PointerEventArgs args)
    {
        if (_cardDrag is not { } drag || drag.Pointer.Id != args.Pointer.Id) return;
        UpdateCardDrag(args.GetPosition(_surface));
        args.Handled = true;
    }

    private void OnCardMoveFinished(object? sender, PointerReleasedEventArgs args)
    {
        if (_cardDrag is not { } drag || drag.Pointer.Id != args.Pointer.Id) return;
        // A button can release capture before its PointerReleased handler runs.
        UpdateCardDrag(args.GetPosition(_surface));
        ReleaseCardDrag();
        args.Handled = true;
    }

    private void OnCardMoveCanceled(object? sender, PointerCaptureLostEventArgs args)
    {
        if (_cardDrag is not { } drag || drag.Pointer.Id != args.Pointer.Id) return;
        CancelCardDrag();
        args.Handled = true;
    }

    private void UpdateCardDrag(Point point)
    {
        if (_cardDrag is not { } drag) return;
        MoveCardTo(new Point(drag.CardOrigin.X + point.X - drag.PointerOrigin.X,
            drag.CardOrigin.Y + point.Y - drag.PointerOrigin.Y));
    }

    private void MoveCardTo(Point point)
    {
        if (_editor is null || _session is not { Busy: false } session) return;
        if (Math.Abs(point.X - CardBounds.X) < 0.01 && Math.Abs(point.Y - CardBounds.Y) < 0.01) return;
        var travel = CardTravelArea(new Size(_intendedCardBounds.Width, _intendedCardBounds.Height));
        var x = Math.Clamp(point.X, travel.Left, travel.Right);
        var y = Math.Clamp(point.Y, travel.Top, travel.Bottom);
        // Moving the card only clears the view. Saving is a separate authored change.
        if (Math.Abs(x - CardBounds.X) < 0.01 && Math.Abs(y - CardBounds.Y) < 0.01) return;
        _temporaryCardPosition = new Point(travel.Width > 0 ? (x - travel.Left) / travel.Width : 0.5,
            travel.Height > 0 ? (y - travel.Top) / travel.Height : 1);
        UpdateGeometry(session);
    }

    private void OnCardMoveKeyDown(object? sender, KeyEventArgs args)
    {
        if (_editor is not { } editor || _session is not { Busy: false } ||
            args.Key is not (Key.Left or Key.Right or Key.Up or Key.Down)) return;
        editor.CancelGesture();
        var shift = args.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var distance = shift ? 10d : 1d;
        MoveCardTo(new Point(CardBounds.X + (args.Key == Key.Left ? -distance : args.Key == Key.Right ? distance : 0),
            CardBounds.Y + (args.Key == Key.Up ? -distance : args.Key == Key.Down ? distance : 0)));
        args.Handled = true;
    }

    private void ApplyCardPositionPreview()
    {
        _configuredCardBounds = CardBounds;
        if (_editor is not null && _temporaryCardPosition is { } position)
        {
            var travel = CardTravelArea(new Size(CardBounds.Width, CardBounds.Height));
            CardBounds = new Rect(travel.X + position.X * travel.Width, travel.Y + position.Y * travel.Height,
                CardBounds.Width, CardBounds.Height);
        }
        _intendedCardBounds = CardBounds;
        if (_editor is not null && _cardCollapsed)
        {
            SetCardCompact(true);
            _card.Measure(new Size(_card.Width, _card.MaxHeight));
            CardBounds = new Rect(CardBounds.X, CardBounds.Y, _card.DesiredSize.Width, _card.DesiredSize.Height);
        }
        CardBounds = AvoidEditorInspector(CardBounds);
        _cardSave.IsEnabled = _session is { Editing: true, Busy: false } && CardPositionChanged;
    }

    private void OnCardSave(object? sender, RoutedEventArgs args)
    {
        if (_editor is not { } editor || _session is not { Busy: false } ||
            _temporaryCardPosition is not { } position || !CardPositionChanged) return;
        ClearCardPositionPreview();
        editor.SetCardPosition(position.X, position.Y);
    }

    internal void CancelCardDrag()
    {
        if (_cardDrag is not { } drag) return;
        _temporaryCardPosition = drag.PreviousPosition;
        ReleaseCardDrag();
        if (_session is { Busy: false } session) UpdateGeometry(session);
    }

    internal void ClearCardPositionPreview()
    {
        ReleaseCardDrag();
        _temporaryCardPosition = null;
        _cardSave.IsEnabled = false;
    }

    private void ReleaseCardDrag()
    {
        if (_cardDrag is not { } drag) return;
        _cardDrag = null;
        if (drag.Pointer.Captured == _cardMove) drag.Pointer.Capture(null);
    }
}
