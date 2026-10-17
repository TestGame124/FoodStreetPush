using System.Collections.Generic;
using FoodStreet.Visitors;
using UnityEngine;

namespace LastOrders.Crew
{
    /// <summary>
    /// The tables: a mess left by the last diner, wiped up before anything else; and now and then a
    /// table nobody is sitting at, wiped down anyway. Done standing at a side of the table with no chair
    /// on it, leaning over the top with the cloth going round.
    /// </summary>
    public sealed class TableChores : IChoreSource
    {
        const float MessSeconds = 3.2f;
        const float WipeDownSeconds = 4.5f;
        const float WipeDownWeight = 2f;

        /// <summary>How far out from the table's edge they stand to lean over it.</summary>
        const float StandOff = 0.3f;

        /// <summary>Half a table's width, when it has no chair opposite to measure it by.</summary>
        const float DefaultHalfWidth = 0.53f;

        public void Offer(Vector3 from, List<Chore> into)
        {
            IReadOnlyList<TableMess> messes = TableMess.All;
            for (int i = 0; i < messes.Count; i++)
            {
                TableMess mess = messes[i];
                if (mess == null || !mess.IsDirty || mess.Table == null) continue;

                TableSeating seating = TableSeating.Of(mess.Table);
                if (seating == null || !seating.Has(mess.Chair) || Occupied(mess.Table, mess.Chair)) continue;

                Vector3 at = mess.transform.position;
                if (!FreeSide(mess.Table, seating, mess.Chair, at, out Vector3 stand, out _)) continue;

                TableMess target = mess;
                TableBehaviour table = mess.Table;
                int chair = mess.Chair;
                into.Add(new Chore
                {
                    Label = "Clearing a table",
                    Stand = stand,
                    Work = at,
                    Reach = 0.1f,
                    Motion = ChoreMotion.Wipe,
                    Seconds = MessSeconds,
                    Weight = 10f,
                    Urgent = true,
                    Key = target,
                    StillWanted = () => target != null && target.IsDirty && !Occupied(table, chair),
                    Done = () =>
                    {
                        if (target != null) target.Clean();
                    },
                });
            }

            IReadOnlyList<TableBehaviour> tables = DinerPlaces.Tables;
            for (int t = 0; t < tables.Count; t++)
            {
                TableBehaviour table = tables[t];
                if (table == null || AnyoneAt(table)) continue;

                TableSeating seating = TableSeating.Of(table);
                if (seating == null || !seating.Has(0)) continue;
                if (!FreeSide(table, seating, 0, from, out Vector3 stand, out Vector3 centre)) continue;

                Vector3 toward = stand - centre;
                toward.y = 0f;
                into.Add(new Chore
                {
                    Label = "Wiping down a table",
                    Stand = stand,
                    Work = centre + toward.normalized * 0.12f,
                    Reach = 0.16f,
                    Motion = ChoreMotion.Wipe,
                    Seconds = WipeDownSeconds,
                    Weight = WipeDownWeight,
                    Key = table,
                    StillWanted = () => table != null && !AnyoneAt(table),
                });
            }
        }

        /// <summary>
        /// A spot on the floor beside the table, off a side with no chair - the side nearest
        /// <paramref name="near"/> - and the middle of the top.
        /// </summary>
        static bool FreeSide(TableBehaviour table, TableSeating seating, int chair, Vector3 near, out Vector3 stand, out Vector3 centre)
        {
            Vector3 inward = seating.Inward(chair);
            inward.y = 0f;
            inward.Normalize();

            Vector3 edge = seating.EdgePoint(chair);
            float half = DefaultHalfWidth;

            // A chair across the table gives its depth.
            TableBehaviour.Chair[] chairs = table.Chairs;
            for (int i = 0; chairs != null && i < chairs.Length; i++)
            {
                if (i == chair || !seating.Has(i)) continue;
                if (Vector3.Dot(seating.Inward(i), inward) > -0.7f) continue;

                Vector3 across = seating.EdgePoint(i) - edge;
                across.y = 0f;
                half = Mathf.Clamp(across.magnitude * 0.5f, 0.3f, 1f);
                break;
            }

            centre = edge + inward * half;
            Vector3 side = Vector3.Cross(Vector3.up, inward);
            float sign = Vector3.Dot(near - centre, side) >= 0f ? 1f : -1f;

            Vector3 floor = table.transform.position;
            stand = centre + side * (sign * (half + StandOff));
            stand.y = floor.y;

            // The side picked first may be up against something; the other one, then.
            if (!ChoreBoard.IsOpen(stand, 0.25f))
            {
                Vector3 other = centre - side * (sign * (half + StandOff));
                other.y = floor.y;
                if (ChoreBoard.IsOpen(other, 0.25f)) stand = other;
            }

            return true;
        }

        static bool Occupied(TableBehaviour table, int chair)
        {
            TableBehaviour.Chair c = table != null ? table.ChairAt(chair) : null;
            return c == null || c.isBusy;
        }

        static bool AnyoneAt(TableBehaviour table)
        {
            TableBehaviour.Chair[] chairs = table.Chairs;
            if (chairs == null) return true;

            for (int i = 0; i < chairs.Length; i++)
                if (chairs[i] != null && chairs[i].isBusy) return true;

            return false;
        }
    }
}
