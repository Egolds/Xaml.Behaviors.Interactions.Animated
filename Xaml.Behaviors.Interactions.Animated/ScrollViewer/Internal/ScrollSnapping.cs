using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using System;
using System.Collections.Generic;
using System.Linq;
using Xaml.Behaviors.Interactions.Animated.Utilites;

namespace Xaml.Behaviors.Interactions.Animated.Internal;

/// <summary>
/// Snap points support shared by the animated scrolling behaviors: the animation has to land on the
/// same offset the built-in scrolling would land on.
/// </summary>
internal static class ScrollSnapping
{
    /// <summary>
    /// Aligns <paramref name="offset"/> to the nearest snap point along <paramref name="orientation"/>.
    /// Returns the offset unchanged when the content declares no snap points.
    /// </summary>
    public static Vector SnapOffset(ScrollContentPresenter presenter, Vector offset, double direction,
        bool snapToNext, Orientation orientation)
    {
        var snapPointsType = orientation == Orientation.Vertical
            ? presenter.VerticalSnapPointsType
            : presenter.HorizontalSnapPointsType;

        if (snapPointsType == SnapPointsType.None)
            return offset;

        if (GetScrollSnapPointsInfo(presenter) is not { } snapPointsInfo)
            return offset;

        if (snapToNext && direction == 0)
            return offset;

        var alignment = orientation == Orientation.Vertical
            ? presenter.VerticalSnapPointsAlignment
            : presenter.HorizontalSnapPointsAlignment;

        var areSnapPointsRegular = orientation == Orientation.Vertical
            ? snapPointsInfo.AreVerticalSnapPointsRegular
            : snapPointsInfo.AreHorizontalSnapPointsRegular;

        IReadOnlyList<double> irregularSnapPoints = new List<double>();
        double regularSnapPoint = 0, regularSnapPointOffset = 0;

        if (areSnapPointsRegular)
            regularSnapPoint = snapPointsInfo.GetRegularSnapPoints(orientation, alignment, out regularSnapPointOffset);
        else
            irregularSnapPoints = snapPointsInfo.GetIrregularSnapPoints(orientation, alignment);

        if (!areSnapPointsRegular && irregularSnapPoints.Count == 0)
            return offset;

        var diff = GetAlignmentDiff(presenter).Get(orientation);
        var estimatedOffset = offset.Get(orientation) + diff;

        double previousSnapPoint, nextSnapPoint;

        if (areSnapPointsRegular)
        {
            previousSnapPoint = (int)(estimatedOffset / regularSnapPoint) * regularSnapPoint + regularSnapPointOffset;
            nextSnapPoint = previousSnapPoint + regularSnapPoint;
        }
        else
        {
            (previousSnapPoint, nextSnapPoint) = FindNearestSnapPoint(irregularSnapPoints, estimatedOffset);
        }

        var midPoint = (previousSnapPoint + nextSnapPoint) / 2;

        var nearestSnapPoint = snapToNext
            ? direction > 0 ? previousSnapPoint : nextSnapPoint
            : estimatedOffset < midPoint ? previousSnapPoint : nextSnapPoint;

        return offset.With(orientation, nearestSnapPoint - diff);
    }

    private static IScrollSnapPointsInfo? GetScrollSnapPointsInfo(ScrollContentPresenter presenter)
    {
        var scrollable = presenter.Content;

        if (presenter.Content is ItemsControl itemsControl)
            scrollable = itemsControl.Presenter?.Panel;

        if (presenter.Content is ItemsPresenter itemsPresenter)
            scrollable = itemsPresenter.Panel;

        return scrollable as IScrollSnapPointsInfo;
    }

    private static (double previous, double next) FindNearestSnapPoint(IReadOnlyList<double> snapPoints, double value)
    {
        var point = snapPoints.BinarySearch(value, Comparer<double>.Default);

        if (point >= 0)
        {
            var exact = snapPoints[Math.Max(0, point)];
            return (exact, exact);
        }

        point = ~point;

        var previousSnapPoint = snapPoints[Math.Max(0, point - 1)];
        var nextSnapPoint = point >= snapPoints.Count ? snapPoints.Last() : snapPoints[Math.Max(0, point)];

        return (previousSnapPoint, nextSnapPoint);
    }

    private static Vector GetAlignmentDiff(ScrollContentPresenter presenter)
    {
        var vector = default(Vector);

        switch (presenter.VerticalSnapPointsAlignment)
        {
            case SnapPointsAlignment.Center:
                vector += new Vector(0, presenter.Viewport.Height / 2);
                break;
            case SnapPointsAlignment.Far:
                vector += new Vector(0, presenter.Viewport.Height);
                break;
        }

        switch (presenter.HorizontalSnapPointsAlignment)
        {
            case SnapPointsAlignment.Center:
                vector += new Vector(presenter.Viewport.Width / 2, 0);
                break;
            case SnapPointsAlignment.Far:
                vector += new Vector(presenter.Viewport.Width, 0);
                break;
        }

        return vector;
    }
}
