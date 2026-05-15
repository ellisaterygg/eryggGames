using Godot;
using System.Collections.Generic;
using System.Linq;

namespace EryggGames.Shared;

public static class OverlapUtils
{
    /// <summary>
    /// Finds the item in the collection that has the most overlapping area with the source rect.
    /// </summary>
    public static T? GetMostOverlapping<T>(Rect2 sourceRect, IEnumerable<T?> candidates, System.Func<T, Rect2> getRectFunc, float minOverlapRatio = 0.2f) where T : class
    {
        T? bestCandidate = null;
        float maxArea = 0f;
        float sourceArea = sourceRect.Area;

        foreach (var candidate in candidates)
        {
            if (candidate == null) continue;
            Rect2 targetRect = getRectFunc(candidate);
            if (!sourceRect.Intersects(targetRect)) continue;

            Rect2 intersection = sourceRect.Intersection(targetRect);
            float overlapArea = intersection.Area;

            if (overlapArea > maxArea)
            {
                maxArea = overlapArea;
                bestCandidate = candidate;
            }
        }

        // Optional: Ensure at least some significant portion is overlapping
        if (bestCandidate != null && (maxArea / sourceArea) < minOverlapRatio)
        {
            return null;
        }

        return bestCandidate;
    }
}
