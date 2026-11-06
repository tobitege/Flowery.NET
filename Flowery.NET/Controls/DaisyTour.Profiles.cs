using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Flowery.Controls;

public sealed partial class DaisyTour
{
    private DaisyTourEditor? _editor;
    private DaisyTourStyleProfile? _previewProfile;
    private DaisyTourStyleProfile? _activeProfile;
    private int _editGeneration;

    public static readonly StyledProperty<string> IdProperty =
        AvaloniaProperty.Register<DaisyTour, string>(nameof(Id), string.Empty);
    public string Id { get => GetValue(IdProperty); set => SetValue(IdProperty, value); }
    public static readonly DirectProperty<DaisyTour, DaisyTourStyleProfile?> StyleProfileProperty =
        AvaloniaProperty.RegisterDirect<DaisyTour, DaisyTourStyleProfile?>(nameof(StyleProfile),
            tour => tour.StyleProfile, (tour, profile) => tour.StyleProfile = profile);

    /// <summary>The active style profile. A new profile is validated before it is stored, and against <see cref="Id"/>
    /// when that is set already; a rejected profile throws and leaves the active one in place. Its step and annotation
    /// IDs are validated against <see cref="Steps"/> when the tour starts, so the steps may be added later. The tour
    /// keeps its own copy: later changes to the assigned object do not reach the tour, and the returned profile is
    /// that copy, which callers treat as read-only and replace by assigning a new profile.</summary>
    public DaisyTourStyleProfile? StyleProfile
    {
        get => _activeProfile;
        set
        {
            if (value is not null)
            {
                // Serializing validates the format and the 1 MiB limit, which the editor needs to open the profile.
                _ = DaisyTourStyleProfileSerializer.Serialize(value);
                if (!string.IsNullOrEmpty(Id)) DaisyTourStyleProfileSerializer.ValidateTourId(value, Id);
            }
            if (SetAndRaise(StyleProfileProperty, ref _activeProfile, value?.Copy())) RefreshPresentation();
        }
    }

    private DaisyTourStyleProfile? EffectiveProfile => _previewProfile ?? _activeProfile;
    internal Canvas EditingSurface => _surface;
    internal IReadOnlyList<DaisyTourAnnotationPresenter> AnnotationPresenters => _presenters;
    internal void RefreshEditorLayout() { ResizeSurface(); QueueGeometry(); }

    private Rect PlaceEditorCard(Size card)
    {
        var travel = CardTravelArea(card);
        return new Rect(travel.X + travel.Width / 2, travel.Bottom, card.Width, card.Height);
    }

    private Rect CardTravelArea(Size card)
    {
        const double margin = 12;
        var left = _visibleArea.Left + margin;
        var right = _visibleArea.Right - margin;
        return new Rect(left, _visibleArea.Top + margin, Math.Max(0, right - left - card.Width),
            Math.Max(0, _visibleArea.Height - 2 * margin - card.Height));
    }

    private Rect AvoidEditorInspector(Rect card)
    {
        if (_editor is not { } editor) return card;
        var inspector = editor.InspectorBounds;
        var overlap = card.Intersect(inspector);
        if (overlap.Width <= 0 || overlap.Height <= 0) return card;
        var travel = CardTravelArea(new Size(card.Width, card.Height));
        var x = inspector.Left + inspector.Width / 2 < _visibleArea.Left + _visibleArea.Width / 2
            ? inspector.Right + 12
            : inspector.Left - 12 - card.Width;
        // Avoidance changes only the displayed bounds, never the saved or temporary position.
        return new Rect(Math.Clamp(x, travel.Left, travel.Right), card.Y, card.Width, card.Height);
    }

    internal DaisyTourProfileLayers ProfileLayers(DaisyTourStep step, DaisyTourAnnotation annotation)
    {
        var profile = EffectiveProfile;
        var stepProfile = DaisyTourProfileResolver.FindStep(profile, step.Id);
        return new(DaisyTourProfileResolver.FindAnnotation(stepProfile, annotation.Id)?.Style,
            stepProfile?.AnnotationDefaults, profile?.AnnotationDefaults);
    }

    private T TourSetting<T>(AvaloniaProperty property, T applicationValue, T inheritedValue)
    {
        if (DaisyTourProfileResolver.TryGetValue(EffectiveProfile?.Style, property, out var value, out var inherit))
            return inherit ? inheritedValue : (T)value!;
        return applicationValue;
    }

    private T StepSetting<T>(DaisyTourStep step, AvaloniaProperty property, T applicationValue, T inheritedValue)
    {
        var profile = DaisyTourProfileResolver.FindStep(EffectiveProfile, step.Id);
        if (DaisyTourProfileResolver.TryGetValue(profile?.Style, property, out var value, out var inherit))
            return inherit ? inheritedValue : (T)value!;
        return applicationValue;
    }

    private T AnnotationSetting<T>(DaisyTourAnnotation annotation, AvaloniaProperty property, T applicationValue, T inheritedValue)
    {
        if (CurrentStep is not { } step) return applicationValue;
        if (DaisyTourProfileResolver.TryGetValue(ProfileLayers(step, annotation).Annotation, property, out var value, out var inherit))
            return inherit ? inheritedValue : (T)value!;
        return applicationValue;
    }

    private double EffectiveHighlightPadding => TourSetting(HighlightPaddingProperty, HighlightPadding, 6d);
    // The padding the highlight frames use; a geometry update reads it once.
    private double HighlightPaddingValue => EffectiveHighlightPadding is var padding && double.IsFinite(padding) ? Math.Max(0, padding) : 0;
    private double EffectiveHighlightCornerRadius => TourSetting(HighlightCornerRadiusProperty, HighlightCornerRadius, 8d);
    // A profile that inherits the size uses automatic sizing: the global size where the size manager would supply
    // it to the tour, otherwise the default size.
    private DaisySize EffectiveSize => TourSetting(SizeProperty, Size,
        FlowerySizeManager.EnableGlobalAutoSize && FlowerySizeManager.UseGlobalSizeByDefault && !FlowerySizeManager.ShouldIgnoreGlobalSize(this)
            ? FlowerySizeManager.CurrentSize : DaisySize.Medium);
    private DaisyTourCardPosition EffectiveCardPosition => TourSetting(CardPositionProperty, CardPosition, DaisyTourCardPosition.Target);

    internal object? EditorSetting(DaisyTourStep step, DaisyTourAnnotation annotation, string name, bool stepScope) => name switch
    {
        "size" => EffectiveSize,
        "highlightPadding" => EffectiveHighlightPadding,
        "highlightCornerRadius" => EffectiveHighlightCornerRadius,
        "cardBorderBrush" => CardBorder(ThemeBrush("DaisyBaseContentBrush")),
        "cardPosition" => stepScope && StepSetting(step, DaisyTourStep.CardPositionProperty, step.CardPosition, DaisyTourCardPosition.Default) is var position && position != DaisyTourCardPosition.Default ? position : EffectiveCardPosition,
        "placement" => StepSetting(step, DaisyTourStep.PlacementProperty, step.Placement, DaisyTourPlacement.Auto),
        "cardX" => StepSetting(step, DaisyTourStep.CardXProperty, step.CardX, double.NaN),
        "cardY" => StepSetting(step, DaisyTourStep.CardYProperty, step.CardY, double.NaN),
        "sourceAnchor" => AnnotationSetting(annotation, DaisyTourAnnotation.SourceAnchorProperty, annotation.SourceAnchor, DaisyTourAnchor.Auto),
        "targetAnchor" => AnnotationSetting(annotation, DaisyTourAnnotation.TargetAnchorProperty, annotation.TargetAnchor, DaisyTourAnchor.Auto),
        "routeLimitSide" => AnnotationSetting(annotation, DaisyTourAnnotation.RouteLimitSideProperty, annotation.RouteLimitSide, DaisyTourRouteSide.None),
        "routeLimitCoordinate" => AnnotationSetting(annotation, DaisyTourAnnotation.RouteLimitCoordinateProperty, annotation.RouteLimitCoordinate, double.NaN),
        "routeLimitMargin" => AnnotationSetting(annotation, DaisyTourAnnotation.RouteLimitMarginProperty, annotation.RouteLimitMargin, 12d),
        _ => null
    };

    internal async Task BeginEditingAsync(DaisyTourEditor editor, DaisyTourStyleProfile draft, CancellationToken token)
    {
        VerifyAccess();
        if (_editor is not null) throw new InvalidOperationException("A tour editor is already open.");
        if (Steps.Count == 0) throw new InvalidOperationException("The tour needs at least one step for editing.");
        Cancel();
        var editGeneration = ++_editGeneration;
        _editor = editor;
        _previewProfile = draft;
        KeyboardNavigation.SetTabNavigation(_card, KeyboardNavigationMode.Local);
        KeyboardNavigation.SetTabNavigation(_surface, KeyboardNavigationMode.Cycle);
        _surface.Children.Insert(_surface.Children.IndexOf(_card), editor.HandleLayer);
        _surface.Children.Add(editor);
        try { await StartAsync(token); }
        catch
        {
            if (editGeneration == _editGeneration) EndEditing(editor);
            throw;
        }
        if (editGeneration == _editGeneration && _session is not null) editor.Focus();
    }

    internal void UpdatePreview(DaisyTourStyleProfile profile, bool geometryOnly = false)
    {
        _previewProfile = profile;
        // Handle movement changes geometry, not application layout or card content.
        // Keep it on the pointer's UI turn instead of waiting for low-priority layout work.
        if (geometryOnly && _session is { Busy: false } session) TryUpdateGeometry(session);
        else
        {
            RefreshPresentation();
            // Inspector fields read the presenters' resolved values immediately after publication.
            // Refresh those values before Undo/import rebuilds the fields; layout still settles later.
            if (_session is { Busy: false } activeSession) TryUpdateGeometry(activeSession);
        }
    }

    internal async Task EditStepAsync(int index)
    {
        if (_session is { Busy: false } session && index >= 0 && index < session.Steps.Length)
            await TransitionAsync(session, index);
    }

    internal void EndEditing(DaisyTourEditor editor)
    {
        if (!ReferenceEquals(_editor, editor)) return;
        _editGeneration++;
        // End the editing session before releasing its surface; normal completion events stay suppressed.
        if (_session is { } session) Close(session, DaisyTourCloseReason.Cancelled);
        _surface.Children.Remove(editor.HandleLayer);
        _surface.Children.Remove(editor);
        _editor = null;
        _previewProfile = null;
        KeyboardNavigation.SetTabNavigation(_card, KeyboardNavigationMode.Cycle);
        // Back to the surface's default; WinUI's default is Local, Avalonia's is Continue.
        _surface.ClearValue(KeyboardNavigation.TabNavigationProperty);
    }
}

internal readonly record struct DaisyTourProfileLayers(
    DaisyTourStyleOverride? Annotation, DaisyTourStyleOverride? Step, DaisyTourStyleOverride? Tour);
