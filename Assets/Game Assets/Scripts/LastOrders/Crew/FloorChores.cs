using System.Collections.Generic;
using UnityEngine;

namespace LastOrders.Crew
{
    /// <summary>
    /// The floor: a patch of the dining room or the kitchen aisle swept, broom side to side, toward
    /// the middle of the room. Any open spot will do; the closer ones are likelier, so a sweep moves
    /// round the room rather than jumping across it.
    /// </summary>
    public sealed class FloorChores : IChoreSource
    {
        /// <summary>Metres between one patch and the next.</summary>
        const float Spacing = 1.6f;

        /// <summary>How much sweeping is wanted altogether, against a table's 2.</summary>
        const float TotalWeight = 6f;

        static readonly List<Vector3> spots = new List<Vector3>();
        static Rect laidFor;
        static float listedAt = float.NegativeInfinity;

        public void Offer(Vector3 from, List<Chore> into)
        {
            List<Vector3> patches = Patches();
            if (patches.Count == 0) return;

            Rect room = DinerPlaces.Inner;
            var middle = new Vector3(room.center.x, 0f, room.center.y);
            float weight = TotalWeight / patches.Count;

            for (int i = 0; i < patches.Count; i++)
            {
                Vector3 stand = patches[i];
                Vector3 toward = middle - stand;
                toward.y = 0f;
                toward = toward.sqrMagnitude > 0.25f ? toward.normalized : Vector3.forward;

                into.Add(new Chore
                {
                    Label = "Sweeping the floor",
                    Stand = stand,
                    Work = stand + toward * 0.55f,
                    Reach = 0.35f,
                    Motion = ChoreMotion.Sweep,
                    Seconds = 5.5f,
                    Weight = weight,
                    Key = new Vector2Int(Mathf.RoundToInt(stand.x * 10f), Mathf.RoundToInt(stand.z * 10f)),
                });
            }
        }

        /// <summary>Open spots on a grid over the dining room and the kitchen aisle, listed again when the room changes.</summary>
        static List<Vector3> Patches()
        {
            Rect room = DinerPlaces.Inner;
            float now = Time.realtimeSinceStartup;
            if (room == laidFor && now - listedAt < 6f) return spots;

            laidFor = room;
            listedAt = now;
            spots.Clear();
            if (room.width <= 1f || room.height <= 1f) return spots;

            float y = DinerPlaces.Floor != null ? DinerPlaces.Floor.transform.position.y : 0f;
            for (float x = room.xMin + 0.8f; x <= room.xMax - 0.8f; x += Spacing)
            {
                for (float z = room.yMin + 0.8f; z <= room.yMax - 0.8f; z += Spacing)
                {
                    var p = new Vector3(x, y, z);
                    if (ChoreBoard.IsOpen(p, 0.35f) && ChoreBoard.IsOpen(p + Vector3.forward * 0.5f, 0.2f)) spots.Add(p);
                }
            }

            return spots;
        }
    }
}
