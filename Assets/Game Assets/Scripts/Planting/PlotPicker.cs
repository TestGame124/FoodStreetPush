using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Finds the plot of a kind under a point on the screen: the <see cref="ISeedPlot"/> a seed is
/// dragged over, the <see cref="IHarvestPlot"/> under a sickle. Every collider along the ray is
/// tried, nearest first, so a worker or a crop standing in front of a pot does not hide it.
/// </summary>
public static class PlotPicker
{
    static RaycastHit[] hits = new RaycastHit[16];
    static readonly IComparer<RaycastHit> NearestFirst = Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

    /// <summary>The <typeparamref name="TPlot"/> under <paramref name="screen"/> seen through <paramref name="camera"/>, or null.</summary>
    public static TPlot Under<TPlot>(Camera camera, Vector2 screen, float maxDistance = 500f) where TPlot : class
    {
        if (camera == null) return null;

        Ray ray = camera.ScreenPointToRay(screen);
        int count = Physics.RaycastNonAlloc(ray, hits, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        if (count == hits.Length)
        {
            hits = new RaycastHit[hits.Length * 2];
            count = Physics.RaycastNonAlloc(ray, hits, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
        }

        Array.Sort(hits, 0, count, NearestFirst);

        for (int i = 0; i < count; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null) continue;

            // Upwards from the collider: a pot keeps its script on the root and may keep colliders lower down.
            TPlot plot = collider.GetComponentInParent<TPlot>();
            if (plot != null) return plot;
        }

        return null;
    }
}
