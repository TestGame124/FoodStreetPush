using System.Collections.Generic;
using UnityEngine;

namespace FoodStreet.Visitors
{
    /// <summary>One chair at one table, by the identity the level authored.</summary>
    public readonly struct SeatRef
    {
        public readonly TableBehaviour Table;
        public readonly int Index;

        /// <summary>How this table's chairs are sat in and slid about. Null only for a default seat.</summary>
        public readonly TableSeating Seating;

        public SeatRef(TableBehaviour table, int index)
        {
            Table = table;
            Index = index;
            Seating = TableSeating.Of(table);
        }

        public bool IsValid => Table != null && Table.ChairAt(Index) != null;

        public TableBehaviour.Chair Chair => Table != null ? Table.ChairAt(Index) : null;

        bool Measured => Seating != null && Seating.Has(Index);

        /// <summary>Where the visitor ends up sitting, tucked in to the table. Falls back to the chair,
        /// then the table itself, so a chair with no authored sit point seats somebody visibly wrong
        /// rather than at the world origin.</summary>
        public Vector3 SitPoint
        {
            get
            {
                if (Measured) return Seating.SeatedPoint(Index);

                TableBehaviour.Chair chair = Chair;
                return chair != null && chair.sitPos != null
                    ? chair.sitPos.position
                    : (Table != null ? Table.transform.position : Vector3.zero);
            }
        }

        /// <summary>
        /// Faces the table rather than the chair's own forward: chairs are art, and their pivots
        /// point wherever the artist left them. Nor the table's pivot - that is a corner of its
        /// build cell, so facing it turns diners half away from their own plate.
        /// </summary>
        public Vector3 SitForward
        {
            get
            {
                if (Measured) return Seating.Inward(Index);

                TableBehaviour.Chair chair = Chair;
                if (chair == null || chair.sitPos == null) return Vector3.forward;

                Vector3 toward = chair.foodPlatePos != null ? chair.foodPlatePos.position : Table.transform.position;
                Vector3 inward = toward - chair.sitPos.position;
                inward.y = 0f;

                return inward.sqrMagnitude > 1e-4f ? inward.normalized : chair.sitPos.forward;
            }
        }

        /// <summary>Between the pulled-out chair and the table, where somebody stands to sit down.</summary>
        public Vector3 StandPoint => Measured ? Seating.StandPoint(Index) : SitPoint;

        /// <summary>Beside the standing spot, on the side nearer <paramref name="toward"/>.</summary>
        public Vector3 SidePoint(Vector3 toward) => Measured ? Seating.SidePoint(Index, toward) : SitPoint;

        /// <summary>Where the body lands on sitting down, before the chair scoots in.</summary>
        public Vector3 PulledPoint => Measured ? Seating.PulledPoint(Index) : SitPoint;

        /// <summary>Where the body is with the chair wherever it has slid to.</summary>
        public Vector3 SeatNow => Measured ? Seating.SeatNow(Index) : SitPoint;

        /// <summary>Where a plate goes. The level's own waiting staff already deliver here.</summary>
        public Transform PlatePoint => Chair != null ? Chair.foodPlatePos : null;
    }

    /// <summary>
    /// Every chair in the level, and the one place they are handed out.
    ///
    /// Capacity is counted in CHAIRS, not tables. The old system counted tables so that a party
    /// took a whole one, but a car here brings one to a few people - so a four-top with one
    /// person at it is three seats the restaurant is refusing to sell, and refusing them makes
    /// the car park fill up for a reason the player cannot see. A party is seated together when
    /// a table has room for it, and spread over free chairs when none does.
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

            // A clean place first: one the last diner left crumbs at only once there is no other.
            for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < tables.Count; i++)
            {
                TableBehaviour table = tables[i];
                TableBehaviour.Chair[] chairs = table != null ? table.Chairs : null;
                if (chairs == null) continue;

                for (int c = 0; c < chairs.Length; c++)
                {
                    if (chairs[c] == null || chairs[c].isBusy) continue;
                    if (pass == 0 && LastOrders.Crew.TableMess.IsDirtyAt(table, c)) continue;

                    chairs[c].isBusy = true;
                    chairs[c].isSitted = false;

                    seat = new SeatRef(table, c);
                    return true;
                }
            }

            seat = default;
            return false;
        }

        /// <summary>
        /// Claims chairs for a party that came in one car: at one table when any table has room for
        /// all of them, spread over the nearest free chairs otherwise. Returns how many were
        /// claimed, which can be fewer than asked for - never more.
        /// </summary>
        public int TryReserveParty(int size, List<SeatRef> into)
        {
            Rescan();

            int claimed = 0;
            size = Mathf.Max(1, size);

            // A table with room for everyone first: a party sits together when it can - at a clean one
            // if there is one.
            for (int pass = 0; pass < 2 && claimed == 0; pass++)
            for (int i = 0; i < tables.Count && claimed == 0; i++)
            {
                TableBehaviour table = tables[i];
                TableBehaviour.Chair[] chairs = table != null ? table.Chairs : null;
                if (chairs == null || FreeAt(chairs) < size) continue;
                if (pass == 0 && Messy(table)) continue;

                claimed += ReserveAt(table, chairs, size, into);
            }

            // Otherwise wherever there is a chair, one table at a time.
            for (int i = 0; i < tables.Count && claimed < size; i++)
            {
                TableBehaviour table = tables[i];
                TableBehaviour.Chair[] chairs = table != null ? table.Chairs : null;
                if (chairs == null) continue;

                claimed += ReserveAt(table, chairs, size - claimed, into);
            }

            return claimed;
        }

        static bool Messy(TableBehaviour table)
        {
            for (int i = 0; i < LastOrders.Crew.TableMess.All.Count; i++)
                if (LastOrders.Crew.TableMess.All[i] != null && LastOrders.Crew.TableMess.All[i].Table == table) return true;

            return false;
        }

        static int FreeAt(TableBehaviour.Chair[] chairs)
        {
            int free = 0;
            for (int c = 0; c < chairs.Length; c++)
                if (chairs[c] != null && !chairs[c].isBusy) free++;

            return free;
        }

        static int ReserveAt(TableBehaviour table, TableBehaviour.Chair[] chairs, int wanted, List<SeatRef> into)
        {
            int claimed = 0;

            for (int c = 0; c < chairs.Length && claimed < wanted; c++)
            {
                if (chairs[c] == null || chairs[c].isBusy) continue;

                chairs[c].isBusy = true;
                chairs[c].isSitted = false;

                into.Add(new SeatRef(table, c));
                claimed++;
            }

            return claimed;
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
