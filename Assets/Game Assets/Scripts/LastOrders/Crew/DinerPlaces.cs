using System.Collections.Generic;
using LastOrders.Kitchen;
using LastOrders.Restaurant;
using UnityEngine;

namespace LastOrders.Crew
{
    /// <summary>
    /// What is in the diner, for the chore sources: the floor, the tables, the serving pass, the
    /// stations and the decorations. Looked up again every couple of seconds, so a table bought or a
    /// lamp moved is part of the round soon after, and shared so the sources do not each search the
    /// scene.
    /// </summary>
    public static class DinerPlaces
    {
        const float RefreshSeconds = 2f;

        static float refreshedAt = float.NegativeInfinity;
        static RestaurantFloor floor;
        static readonly List<TableBehaviour> tables = new List<TableBehaviour>();
        static readonly List<ServingPass> passes = new List<ServingPass>();
        static readonly List<CookeryStation> stations = new List<CookeryStation>();
        static readonly List<PlacedObject_Done> decor = new List<PlacedObject_Done>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            refreshedAt = float.NegativeInfinity;
            floor = null;
            tables.Clear();
            passes.Clear();
            stations.Clear();
            decor.Clear();
        }

        public static RestaurantFloor Floor
        {
            get
            {
                Refresh();
                return floor;
            }
        }

        public static IReadOnlyList<TableBehaviour> Tables
        {
            get
            {
                Refresh();
                return tables;
            }
        }

        public static IReadOnlyList<ServingPass> Passes
        {
            get
            {
                Refresh();
                return passes;
            }
        }

        public static IReadOnlyList<CookeryStation> Stations
        {
            get
            {
                Refresh();
                return stations;
            }
        }

        public static IReadOnlyList<PlacedObject_Done> Decor
        {
            get
            {
                Refresh();
                return decor;
            }
        }

        /// <summary>The room between the walls, as the floor was last laid; empty when there is no floor.</summary>
        public static Rect Inner
        {
            get
            {
                RestaurantFloor f = Floor;
                if (f == null) return default;
                if (f.IsFitted) return f.Inner;

                // Not laid this session yet: its mesh's bounds are as good.
                Bounds b = f.GetComponent<Renderer>().bounds;
                return Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
            }
        }

        /// <summary>True if <paramref name="world"/> is inside the diner's walls (a little in from them).</summary>
        public static bool Inside(Vector3 world, float inset = 0f)
        {
            Rect r = Inner;
            if (r.width <= 0f) return false;
            return world.x > r.xMin + inset && world.x < r.xMax - inset && world.z > r.yMin + inset && world.z < r.yMax - inset;
        }

        static void Refresh()
        {
            float now = Time.realtimeSinceStartup;
            if (now - refreshedAt < RefreshSeconds) return;
            refreshedAt = now;

            if (floor == null) floor = Object.FindFirstObjectByType<RestaurantFloor>();

            tables.Clear();
            foreach (TableBehaviour table in Object.FindObjectsByType<TableBehaviour>(FindObjectsSortMode.InstanceID))
                if (table.isActiveAndEnabled && (floor == null || Inside(table.transform.position))) tables.Add(table);

            passes.Clear();
            passes.AddRange(Object.FindObjectsByType<ServingPass>(FindObjectsSortMode.InstanceID));

            stations.Clear();
            foreach (CookeryStation station in Object.FindObjectsByType<CookeryStation>(FindObjectsSortMode.InstanceID))
                if (station.isActiveAndEnabled) stations.Add(station);

            decor.Clear();
            foreach (PlacedObject_Done placed in Object.FindObjectsByType<PlacedObject_Done>(FindObjectsSortMode.InstanceID))
            {
                PlacedObjectTypeSO type = placed.PlacedObjectTypeSO;
                if (type == null || type.categoryType != ObjectCategory.Decoration || placed.isRemoved) continue;
                if (floor != null && !Inside(placed.transform.position, -0.5f)) continue;
                decor.Add(placed);
            }
        }

        /// <summary>Everything a placed thing draws, as one box - its blob shadow left out: the stations' sprite
        /// shadows reach well past their fronts.</summary>
        public static Bounds BoundsOf(Component thing)
        {
            bool any = false;
            var bounds = new Bounds(thing.transform.position, Vector3.zero);
            foreach (Renderer r in thing.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r is ParticleSystemRenderer || r is SpriteRenderer) continue;
                if (!any) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                any = true;
            }

            return any ? bounds : new Bounds(thing.transform.position + Vector3.up * 0.5f, Vector3.one);
        }
    }
}
