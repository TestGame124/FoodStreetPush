using LastOrders.Audio;
using UnityEngine;

namespace FoodStreet.Visitors
{
    /// <summary>
    /// How a table's chairs are sat in: where a visitor stands to sit down, where the body ends
    /// up, and each chair sliding out from under the table and back in again.
    ///
    /// Worked out from the table as it is dressed rather than authored per prefab. The top is
    /// measured off the table's own renderers, so a re-dressed table - taller, wider - puts
    /// everybody the right distance from it without anyone moving a sit point. "Inward" is the
    /// face of the top each chair is outside of, NOT the direction to the table's pivot: that
    /// pivot is a corner of the build cell, and facing it turned every diner toward the camera.
    ///
    /// Distances are along that inward line and measured from the table's edge, because the
    /// edge is what a body must not go through. A seated visitor's hips sit
    /// <see cref="hipsToEdge"/> back from it - room for the chest and the elbows - and the chair
    /// is pulled out however far that takes, which for a chair tucked under the top is a little.
    /// To sit down it comes out further, far enough for a person to stand between the seat and
    /// the table.
    ///
    /// Added to a table the first time somebody reserves a chair at it, so tables already in a
    /// level need nothing; the one on the prefab is there to tune.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TableSeating : MonoBehaviour
    {
        [Header("The body in the chair")]
        [Tooltip("Metres between a seated visitor's hips and the table's edge: room for the chest and the elbows.")]
        [SerializeField, Min(0.1f)] float hipsToEdge = 0.3f;

        [Tooltip("Metres from the chair's pivot back toward its backrest, where the body sits.")]
        [SerializeField] float seatBack = 0.08f;

        [Tooltip("Height of the cushion's top above the chair's pivot. Measured from the chair model by " +
                 "'Last Orders / Restaurant / Set Up Seated Visitors'.")]
        [SerializeField, Min(0.1f)] float seatHeight = 0.5f;

        [Header("Getting in and out")]
        [Tooltip("Metres in front of the seat a visitor stands before sitting: about where the feet end up " +
                 "once seated, so the feet stay put while the hips go back.")]
        [SerializeField, Min(0.1f)] float standIn = 0.42f;

        [Tooltip("Metres between a standing visitor's middle and the table's edge.")]
        [SerializeField, Min(0.1f)] float standToEdge = 0.25f;

        [Tooltip("Metres to the side of the standing spot a visitor walks to first, so the last step is in " +
                 "beside the table rather than through it.")]
        [SerializeField, Min(0.2f)] float sideStep = 0.75f;

        [Tooltip("Seconds for a chair to slide out, or back in.")]
        [SerializeField, Min(0.05f)] float slideSeconds = 0.35f;

        [Tooltip("Degrees a chair twists as it scrapes across the floor, settling as it stops.")]
        [SerializeField, Range(0f, 8f)] float scrapeTwist = 2.5f;

        sealed class Seat
        {
            public Transform chair;
            public Vector3 restLocal;
            public Quaternion restRotation;

            /// <summary>Away from the table, in the chair's parent's space, a metre long in the world.</summary>
            public Vector3 outwardLocal;

            /// <summary>Into the table, in the table's own space.</summary>
            public Vector3 inward;

            /// <summary>Metres from the chair's resting pivot out to the table's edge. Negative when the
            /// pivot is tucked right under the top.</summary>
            public float edge;

            public float offset, from, to, clock, delay;
            public bool sliding;
            public float twistSign = 1f;
        }

        TableBehaviour table;
        Seat[] seats;
        float tableTopLocal;
        bool built;

        /// <summary>The seating for a table, added on first use.</summary>
        public static TableSeating Of(TableBehaviour table)
        {
            if (table == null) return null;

            TableSeating seating = table.GetComponent<TableSeating>();
            return seating != null ? seating : table.gameObject.AddComponent<TableSeating>();
        }

        public float SlideSeconds => slideSeconds;

        public bool Has(int chair)
        {
            Build();
            return seats != null && chair >= 0 && chair < seats.Length && seats[chair] != null && seats[chair].chair != null;
        }

        /// <summary>World height of the table's top.</summary>
        public float TableTop
        {
            get
            {
                Build();
                return transform.TransformPoint(new Vector3(0f, tableTopLocal, 0f)).y;
            }
        }

        /// <summary>Into the table from this chair, flat and a metre long: the way a diner faces.</summary>
        public Vector3 Inward(int chair) => Has(chair) ? transform.TransformDirection(seats[chair].inward) : Vector3.forward;

        /// <summary>Where the body stands with the chair slid <paramref name="offset"/> metres out.</summary>
        public Vector3 SeatPoint(int chair, float offset) =>
            Has(chair) ? RestPivot(seats[chair]) - Inward(chair) * (offset + seatBack) : transform.position;

        /// <summary>Where the body is with the chair wherever it is right now.</summary>
        public Vector3 SeatNow(int chair) => Has(chair) ? SeatPoint(chair, seats[chair].offset) : transform.position;

        /// <summary>Where the body ends up, sat in and tucked to the table.</summary>
        public Vector3 SeatedPoint(int chair) => Has(chair) ? SeatPoint(chair, SeatedOffset(seats[chair])) : transform.position;

        /// <summary>Where the body lands on sitting down, before scooting in.</summary>
        public Vector3 PulledPoint(int chair) => Has(chair) ? SeatPoint(chair, PulledOffset(seats[chair])) : transform.position;

        /// <summary>Between the pulled-out seat and the table: where somebody stands to sit down, and
        /// stands up to.</summary>
        public Vector3 StandPoint(int chair) => PulledPoint(chair) + Inward(chair) * standIn;

        /// <summary>
        /// Beside the standing spot, on whichever side <paramref name="toward"/> is: the last point
        /// walked to before stepping in, and the first after stepping out. Keeps the final step
        /// alongside the table instead of across it.
        /// </summary>
        public Vector3 SidePoint(int chair, Vector3 toward)
        {
            Vector3 stand = StandPoint(chair);
            Vector3 side = Vector3.Cross(Vector3.up, Inward(chair));

            Vector3 to = toward - stand;
            float sign = Vector3.Dot(to, side) >= 0f ? 1f : -1f;

            return stand + side * (sign * sideStep);
        }

        /// <summary>The middle of the table's edge in front of this chair, at the top's height.</summary>
        public Vector3 EdgePoint(int chair)
        {
            if (!Has(chair)) return transform.position;

            Vector3 point = RestPivot(seats[chair]) + Inward(chair) * seats[chair].edge;
            point.y = TableTop;
            return point;
        }

        /// <summary>World height of the cushion's top.</summary>
        public float SeatTop(int chair) => Has(chair) ? RestPivot(seats[chair]).y + seatHeight : transform.position.y + seatHeight;

        public bool IsSliding(int chair) => Has(chair) && seats[chair].sliding;

        /// <summary>Slides the chair out far enough to stand in front of it.</summary>
        public void PullOut(int chair)
        {
            if (!Has(chair)) return;
            SlideTo(seats[chair], PulledOffset(seats[chair]), 0f);
            SoundController.Play(SoundId.ChairOut, transform.position);
        }

        /// <summary>Slides the chair - and whoever is on it - in to the table.</summary>
        public void ScootIn(int chair)
        {
            if (!Has(chair)) return;
            SlideTo(seats[chair], SeatedOffset(seats[chair]), 0f);
            SoundController.Play(SoundId.ChairIn, transform.position);
        }

        /// <summary>Pushes the empty chair back under the table, after <paramref name="delay"/>
        /// seconds - long enough for whoever left it to step clear.</summary>
        public void TuckIn(int chair, float delay = 0f)
        {
            if (Has(chair)) SlideTo(seats[chair], 0f, delay);
        }

        /// <summary>For tooling and tests: the chair's slide right now, in metres out from rest.</summary>
        public float OffsetOf(int chair) => Has(chair) ? seats[chair].offset : 0f;

        float SeatedOffset(Seat seat) => Mathf.Max(0f, hipsToEdge - seat.edge - seatBack);

        /// <summary>Out far enough that someone can stand between the seat and the table - and always
        /// a little past the seated slide, so sitting down is followed by a visible scoot in.</summary>
        float PulledOffset(Seat seat) =>
            Mathf.Max(SeatedOffset(seat) + 0.1f, standIn + standToEdge - seat.edge - seatBack);

        Vector3 RestPivot(Seat seat) =>
            seat.chair.parent != null ? seat.chair.parent.TransformPoint(seat.restLocal) : seat.restLocal;

        void SlideTo(Seat seat, float offset, float delay)
        {
            seat.from = seat.offset;
            seat.to = offset;
            seat.clock = 0f;
            seat.delay = delay;
            seat.sliding = true;
            seat.twistSign = Random.value < 0.5f ? -1f : 1f;
        }

        void Update()
        {
            if (seats == null) return;

            float step = Time.deltaTime / slideSeconds;

            for (int i = 0; i < seats.Length; i++)
            {
                Seat seat = seats[i];
                if (seat == null || !seat.sliding || seat.chair == null) continue;

                if (seat.delay > 0f)
                {
                    seat.delay -= Time.deltaTime;
                    continue;
                }

                seat.clock = Mathf.Min(1f, seat.clock + step);

                // Eased both ways - a chair is lifted a touch and set down rather than shoved - with
                // a small twist that dies away, the way a chair scrapes round on a floor.
                float k = seat.clock * seat.clock * seat.clock * (seat.clock * (seat.clock * 6f - 15f) + 10f);
                seat.offset = Mathf.LerpUnclamped(seat.from, seat.to, k);

                float twist = Mathf.Abs(seat.to - seat.from) > 0.02f
                    ? Mathf.Sin(seat.clock * Mathf.PI * 2f) * (1f - seat.clock) * scrapeTwist * seat.twistSign
                    : 0f;

                Apply(seat, twist);

                if (seat.clock >= 1f) seat.sliding = false;
            }
        }

        static void Apply(Seat seat, float twist)
        {
            seat.chair.localPosition = seat.restLocal + seat.outwardLocal * seat.offset;
            seat.chair.localRotation = Quaternion.AngleAxis(twist, Vector3.up) * seat.restRotation;
        }

        /// <summary>Every chair back where it was authored. A table switched off mid-slide - picked up
        /// in build mode, say - would otherwise come back with a chair stuck out.</summary>
        void OnDisable()
        {
            if (seats == null) return;

            for (int i = 0; i < seats.Length; i++)
            {
                Seat seat = seats[i];
                if (seat == null || seat.chair == null) continue;

                seat.offset = 0f;
                seat.sliding = false;
                Apply(seat, 0f);
            }
        }

        /// <summary>
        /// Measures the table once, on first use: while every chair is still where it was authored,
        /// and in the table's own space so a table that is moved later stays measured.
        /// </summary>
        void Build()
        {
            if (built) return;
            built = true;

            table = GetComponent<TableBehaviour>();
            TableBehaviour.Chair[] chairs = table != null ? table.Chairs : null;
            if (chairs == null) return;

            bool measured = MeasureTop(chairs, out Bounds top);
            tableTopLocal = measured ? top.max.y : 0.75f;

            seats = new Seat[chairs.Length];

            for (int i = 0; i < chairs.Length; i++)
            {
                Transform chair = chairs[i] != null ? chairs[i].sitPos : null;
                if (chair == null) continue;

                Vector3 pivot = transform.InverseTransformPoint(chair.position);

                Vector3 inward;
                float edge;

                if (measured) FaceOf(top, pivot, out inward, out edge);
                else Fallback(chairs[i], pivot, out inward, out edge);

                Vector3 outwardWorld = transform.TransformDirection(-inward);

                seats[i] = new Seat
                {
                    chair = chair,
                    restLocal = chair.localPosition,
                    restRotation = chair.localRotation,
                    outwardLocal = chair.parent != null ? chair.parent.InverseTransformVector(outwardWorld) : outwardWorld,
                    inward = inward,
                    edge = edge,
                };
            }
        }

        /// <summary>
        /// The table's own bounds, in its space: every renderer on it that is not a chair, and not a
        /// plate somebody is eating off.
        /// </summary>
        bool MeasureTop(TableBehaviour.Chair[] chairs, out Bounds top)
        {
            top = default;
            bool any = false;

            foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled || IsPartOfAChair(renderer.transform, chairs)) continue;

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                Bounds local = filter.sharedMesh.bounds;
                Matrix4x4 toTable = transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;

                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 point = local.center + Vector3.Scale(local.extents,
                        new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));

                    point = toTable.MultiplyPoint3x4(point);

                    if (!any)
                    {
                        top = new Bounds(point, Vector3.zero);
                        any = true;
                    }
                    else top.Encapsulate(point);
                }
            }

            return any;
        }

        static bool IsPartOfAChair(Transform piece, TableBehaviour.Chair[] chairs)
        {
            for (int i = 0; i < chairs.Length; i++)
            {
                TableBehaviour.Chair chair = chairs[i];
                if (chair == null) continue;

                if (chair.sitPos != null && piece.IsChildOf(chair.sitPos)) return true;
                if (chair.foodPlatePos != null && piece.IsChildOf(chair.foodPlatePos)) return true;
            }

            return false;
        }

        /// <summary>Which face of the top the chair is outside of, and how far out.</summary>
        static void FaceOf(Bounds top, Vector3 pivot, out Vector3 inward, out float edge)
        {
            Vector3 centre = top.center;
            float outX = Mathf.Abs(pivot.x - centre.x) - top.extents.x;
            float outZ = Mathf.Abs(pivot.z - centre.z) - top.extents.z;

            if (outZ >= outX)
            {
                inward = new Vector3(0f, 0f, pivot.z > centre.z ? -1f : 1f);
                edge = outZ;
            }
            else
            {
                inward = new Vector3(pivot.x > centre.x ? -1f : 1f, 0f, 0f);
                edge = outX;
            }
        }

        /// <summary>No table model to measure: face the plate, and guess the edge just short of it.</summary>
        void Fallback(TableBehaviour.Chair chair, Vector3 pivot, out Vector3 inward, out float edge)
        {
            Vector3 target = chair.foodPlatePos != null
                ? transform.InverseTransformPoint(chair.foodPlatePos.position)
                : Vector3.zero;

            Vector3 flat = target - pivot;
            flat.y = 0f;

            inward = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward;
            edge = Mathf.Max(0f, flat.magnitude - 0.15f);
        }
    }
}
