using System.Collections.Generic;
using UnityEngine;

namespace FoodStreet.Visitors
{
    /// <summary>One chair at one table, by the identity the level authored.</summary>
    public readonly struct SeatRef
    {
        public readonly TableBehaviour Table;
        public readonly int Index;

        public SeatRef(TableBehaviour table, int index)
        {
            Table = table;
            Index = index;
        }

        public bool IsValid => Table != null && Table.ChairAt(Index) != null;

        public TableBehaviour.Chair Chair => Table != null ? Table.ChairAt(Index) : null;

        /// <summary>Where the visitor sits. Falls back to the table itself, so a chair with no
        /// authored sit point seats somebody visibly wrong rather than at the world origin.</summary>
        public Vector3 SitPoint
        {
            get
            {
                TableBehaviour.Chair chair = Chair;
                return chair != null && chair.sitPos != null
                    ? chair.sitPos.position
                    : (Table != null ? Table.transform.position : Vector3.zero);
            }
        }

        public Vector3 SitForward
        {
            get
            {
                TableBehaviour.Chair chair = Chair;
                if (chair == null || chair.sitPos == null || Table == null) return Vector3.forward;

                // Faces the table rather than the chair's own forward. Chairs are art and their
                // pivots point wherever the artist left them; the table is always what somebody
                // eating is looking at.
                Vector3 inward = Table.transform.position - chair.sitPos.position;
                inward.y = 0f;

                return inward.sqrMagnitude > 1e-4f ? inward.normalized : chair.sitPos.forward;
            }
        }

        /// <summary>Where a plate goes. The level's own waiting staff already deliver here.</summary>
        public Transform PlatePoint => Chair != null ? Chair.foodPlatePos : null;
    }

    /// <summary>
    /// Every chair in the level, and the one place they are handed out.
    ///
    /// Capacity is counted in CHAIRS, not tables. The old system counted tables so that a party
    /// took a whole one, but a visitor here arrives alone in their own car - so a four-top with
    /// one person at it is three seats the restaurant is refusing to sell, and refusing them
    /// makes the car park fill up for a reason the player cannot see.
    ///
    /// Occupancy is stored on the level's own <see cref="TableBehaviour.Chair"/> rather than in
    /// a list beside it. There is then one answer to "is this chair taken" rather than two that
    /// can disagree, and a table destroyed mid-service frees its chairs through its own
    /// OnDisable with nothing here to remember.
    ///
    /// The table list is rescanned rather than registered, because tables are placed at runtime
    /// by the build system and <see cref="Zone"/> keeps its own list private. A scan of a
    /// handful of tables once a second costs nothing measurable and cannot go stale.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class SeatRegistry : MonoBehaviour
    {
        [Tooltip("Seconds between rescans for tables built or destroyed during play.")]
        [SerializeField] float rescanSeconds = 1f;

        readonly List<TableBehaviour> tables = new List<TableBehaviour>();

        float nextScan;

        public int ChairCount
        {
            get
            {
                Rescan();

                int count = 0;
                for (int i = 0; i < tables.Count; i++)
                {
                    TableBehaviour.Chair[] chairs = tables[i] != null ? tables[i].Chairs : null;
                    if (chairs != null) count += chairs.Length;
                }

                return count;
            }
        }

        public int FreeChairCount
        {
            get
            {
                Rescan();

                int free = 0;
                for (int i = 0; i < tables.Count; i++)
                {
                    TableBehaviour.Chair[] chairs = tables[i] != null ? tables[i].Chairs : null;
                    if (chairs == null) continue;

                    for (int c = 0; c < chairs.Length; c++)
                    {
                        if (chairs[c] != null && !chairs[c].isBusy) free++;
                    }
                }

                return free;
            }
        }

        public bool HasFreeChair => FreeChairCount > 0;

        /// <summary>Free chairs as a fraction of all of them. Feeds the traffic profile's busy
        /// slowdown. One when there are no chairs at all, because a level with no seating is
        /// not a busy level - the arrival gate turns everyone away on its own.</summary>
        public float FreeChairRatio
        {
            get
            {
                int total = ChairCount;
                return total <= 0 ? 1f : (float)FreeChairCount / total;
            }
        }

        void Awake() => Rescan(force: true);

        /// <summary>
        /// Claims a chair.
        ///
        /// Marks the level's own flags, so the waiting staff see the chair as taken too. The
        /// visitor is not sitting yet - they still have to walk in - and `isSitted` stays false
        /// until they arrive, which is what stops a waiter setting out with a plate for a chair
        /// that is still empty.
        /// </summary>
        public bool TryReserve(out SeatRef seat)
        {
            Rescan();

            for (int i = 0; i < tables.Count; i++)
            {
                TableBehaviour table = tables[i];
                TableBehaviour.Chair[] chairs = table != null ? table.Chairs : null;
                if (chairs == null) continue;

                for (int c = 0; c < chairs.Length; c++)
                {
                    if (chairs[c] == null || chairs[c].isBusy) continue;

                    chairs[c].isBusy = true;
                    chairs[c].isSitted = false;

                    seat = new SeatRef(table, c);
                    return true;
                }
            }

            seat = default;
            return false;
        }

        public void Release(SeatRef seat)
        {
            TableBehaviour.Chair chair = seat.Chair;
            if (chair == null) return;

            chair.ResetStates();
        }

        /// <summary>Frees every chair. For a hard reset, where a visitor removed abnormally
        /// would otherwise hold a chair for the rest of the session.</summary>
        public void ReleaseAll()
        {
            Rescan(force: true);

            for (int i = 0; i < tables.Count; i++)
            {
                TableBehaviour.Chair[] chairs = tables[i] != null ? tables[i].Chairs : null;
                if (chairs == null) continue;

                for (int c = 0; c < chairs.Length; c++) chairs[c]?.ResetStates();
            }
        }

        void Rescan(bool force = false)
        {
            if (!force && Time.unscaledTime < nextScan) return;

            nextScan = Time.unscaledTime + Mathf.Max(0.1f, rescanSeconds);

            tables.Clear();
            tables.AddRange(FindObjectsByType<TableBehaviour>(FindObjectsSortMode.None));
        }
    }
}
