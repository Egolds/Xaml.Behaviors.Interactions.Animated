using Avalonia;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace Xaml.Behaviors.Interactions.Animated.Internal;

/// <summary>
/// Resolves which scroll viewer owns a wheel event when they are nested into each other.
/// </summary>
internal static class NestedScrollChaining
{
    /// <summary>Offsets are fractional, so edges have to be compared loosely.</summary>
    public const double Tolerance = 0.5;

    /// <summary>
    /// Walks from the event source up to <paramref name="own"/>. Returns false when a nested scroll
    /// viewer on the way scrolls itself on this event, which means the caller must not react to it.
    /// </summary>
    public static bool TryWalkToOwnPresenter(object? source, ScrollContentPresenter own,
        PointerWheelEventArgs e, out object? stoppedAt)
    {
        var src = source;

        while (src != null && src != own)
        {
            if (src is ScrollContentPresenter nested)
            {
                if (CanNestedPresenterHandle(nested, e))
                {
                    stoppedAt = null;
                    return false;
                }

                src = nested.GetVisualParent(); // no room left in it: keep going up the chain
            }
            else if (src is Visual visual)
            {
                src = visual.GetVisualParent();
            }
            else
            {
                break; // not a visual: there is nothing left to walk up to
            }
        }

        stoppedAt = src;
        return true;
    }

    /// <summary>
    /// Whether any scroll viewer above <paramref name="from"/> can scroll along <paramref name="orientation"/>.
    /// Edges are ignored on purpose: an outer list stays the owner of the event even when it is
    /// already scrolled to its end.
    /// </summary>
    public static bool HasScrollableAncestor(Visual from, Orientation orientation)
    {
        foreach (var ancestor in from.GetVisualAncestors())
        {
            if (ancestor is ScrollContentPresenter presenter &&
                presenter.Extent.Get(orientation) - presenter.Viewport.Get(orientation) > Tolerance)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a nested scroll viewer consumes this wheel event itself. It does not when the gesture
    /// asks for an axis it cannot scroll at all, or when it is already at the edge in that direction —
    /// in both cases the event has to reach the outer list, otherwise that list would fall back to the
    /// built-in non-animated scrolling.
    /// </summary>
    private static bool CanNestedPresenterHandle(ScrollContentPresenter presenter, PointerWheelEventArgs e)
    {
        // Shift+wheel and horizontal gestures are addressed to horizontal scrolling.
        var horizontal = e.Delta.X != 0 || e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        var orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical;
        var delta = horizontal && e.Delta.X != 0 ? e.Delta.X : e.Delta.Y;

        var maxOffset = presenter.Extent.Get(orientation) - presenter.Viewport.Get(orientation);
        if (maxOffset <= Tolerance)
            return false;

        var offset = presenter.Offset.Get(orientation);

        // A positive delta always moves the content towards the zero offset, on both axes.
        return delta > 0
            ? offset > Tolerance
            : offset < maxOffset - Tolerance;
    }
}
