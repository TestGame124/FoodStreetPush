using System.Collections.Generic;
using LastOrders.Kitchen;
using UnityEngine;

namespace LastOrders.Crew
{
    /// <summary>
    /// The fittings: the serving pass polished from the dining room's side, a quiet station wiped
    /// down, the decorations dusted - the lamp, the jukebox, the plants on the shelf.
    /// </summary>
    public sealed class FixtureChores : IChoreSource
    {
        public void Offer(Vector3 from, List<Chore> into)
        {
            IReadOnlyList<ServingPass> passes = DinerPlaces.Passes;
            for (int p = 0; p < passes.Count; p++)
            {
                ServingPass pass = passes[p];
                if (pass == null || pass.ServicePoints == null || pass.ServicePoints.Count == 0) continue;

                float weight = 1.5f / pass.ServicePoints.Count;
                for (int i = 0; i < pass.ServicePoints.Count; i++)
                {
                    Transform point = pass.ServicePoints[i];
                    if (point == null) continue;

                    into.Add(new Chore
                    {
                        Label = "Polishing the pass",
                        Stand = point.position,
                        Work = CounterTop(pass, point.position),
                        Reach = 0.2f,
                        Motion = ChoreMotion.Wipe,
                        Seconds = 4f,
                        Weight = weight,
                        Key = point,
                    });
                }
            }

            IReadOnlyList<CookeryStation> stations = DinerPlaces.Stations;
            for (int i = 0; i < stations.Count; i++)
            {
                CookeryStation station = stations[i];
                if (station == null || Busy(station)) continue;

                Pose spot = StationSpot(station, 0.5f, out Vector3 top);
                CookeryStation quiet = station;
                into.Add(new Chore
                {
                    Label = $"Wiping down the {StationName(station)}",
                    Stand = spot.position,
                    Work = top,
                    Reach = 0.25f,
                    Motion = ChoreMotion.Wipe,
                    Seconds = 4f,
                    Weight = 1f,
                    Key = station,
                    StillWanted = () => quiet != null && !Busy(quiet),
                });
            }

            IReadOnlyList<PlacedObject_Done> decor = DinerPlaces.Decor;
            Rect room = DinerPlaces.Inner;
            var middle = new Vector3(room.center.x, 0f, room.center.y);
            for (int i = 0; i < decor.Count; i++)
            {
                PlacedObject_Done thing = decor[i];
                if (thing == null) continue;

                Bounds b = DinerPlaces.BoundsOf(thing);
                Vector3 toward = middle - b.center;
                toward.y = 0f;
                toward = toward.sqrMagnitude > 0.01f ? toward.normalized : Vector3.back;

                float reachOut = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(toward.x), 0f, Mathf.Abs(toward.z))));
                Vector3 stand = b.center + toward * (reachOut + 0.45f);
                stand.y = thing.transform.position.y;
                Vector3 work = b.center + toward * (reachOut * 0.6f);
                work.y = Mathf.Clamp(b.min.y + b.size.y * 0.7f, stand.y + 0.6f, stand.y + 1.5f);

                into.Add(new Chore
                {
                    Label = $"Dusting the {DecorName(thing)}",
                    Stand = stand,
                    Work = work,
                    Reach = Mathf.Clamp(b.extents.x + b.extents.z, 0.12f, 0.3f) * 0.5f,
                    Motion = ChoreMotion.Dust,
                    Seconds = 3.5f,
                    Weight = 1.5f,
                    Key = thing,
                });
            }
        }

        /// <summary>
        /// Where somebody stands to work at a station: in front of its working edge,
        /// <paramref name="standOff"/> metres out, facing it; and the middle of its top.
        /// </summary>
        public static Pose StationSpot(CookeryStation station, float standOff, out Vector3 top)
        {
            Bounds b = DinerPlaces.BoundsOf(station);
            Vector3 front = station.centerPoint != null ? station.centerPoint.position : b.center - station.transform.forward * b.extents.z;

            // Stations pivot on a corner, so "the front" is measured from the middle of what they draw.
            Vector3 outward = front - b.center;
            outward.y = 0f;
            outward = outward.sqrMagnitude > 1e-4f ? outward.normalized : -station.transform.forward;

            float edge = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(outward.x), 0f, Mathf.Abs(outward.z))));
            Vector3 stand = b.center + outward * (edge + standOff);
            stand.y = station.transform.position.y;

            top = b.center + outward * (edge * 0.55f);
            top.y = Mathf.Min(b.max.y, stand.y + 1.05f);

            return new Pose(stand, Quaternion.LookRotation(-outward, Vector3.up));
        }

        public static string StationName(CookeryStation station)
        {
            var placed = station.GetComponent<PlacedObject_Done>();
            if (placed != null && placed.PlacedObjectTypeSO != null) return placed.PlacedObjectTypeSO.name;
            return "station";
        }

        static string DecorName(PlacedObject_Done thing) =>
            thing.PlacedObjectTypeSO != null ? thing.PlacedObjectTypeSO.name.ToLowerInvariant() : "decorations";

        static bool Busy(CookeryStation station) => station.recipiesInProgress != null && station.recipiesInProgress.Count > 0;

        /// <summary>The pass's top nearest a point, a little in from its front.</summary>
        static Vector3 CounterTop(ServingPass pass, Vector3 near)
        {
            Transform best = null;
            float nearest = float.MaxValue;
            for (int i = 0; pass.Slots != null && i < pass.Slots.Count; i++)
            {
                Transform slot = pass.Slots[i];
                if (slot == null) continue;

                float d = (slot.position - near).sqrMagnitude;
                if (d < nearest)
                {
                    nearest = d;
                    best = slot;
                }
            }

            if (best != null) return best.position + Vector3.up * 0.01f;

            Bounds b = DinerPlaces.BoundsOf(pass);
            return new Vector3(near.x, b.max.y, b.center.z);
        }
    }
}
