using System;
using UnityEngine;

namespace LastOrders.Effects
{
    /// <summary>
    /// Something put down on the build grid, landing: it drops in from a little above, stretched,
    /// squashes as it touches down, wobbles back to its own shape, and says the moment it lands so
    /// the dust can go up there. It is scaled about the middle of its cells on the ground rather than
    /// its pivot - a kitchen station pivots on a corner - so it squashes in place.
    ///
    /// Added to the object the first time and left on it, switched off, after. The pose it was put
    /// down in is what it always comes back to, at once if the animation is cut short: the object
    /// picked up again, carried off, put back in its pool.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlacementPop : MonoBehaviour
    {
        /// <summary>How one landing goes.</summary>
        [Serializable]
        public struct Shape
        {
            [Tooltip("Metres above where it lands that it falls from.")]
            public float drop;

            [Tooltip("Seconds it takes to fall, or to grow in where it stands when it does not drop.")]
            public float fall;

            [Tooltip("The size it starts at, 1 for its own: something new pops in as it falls.")]
            [Range(0f, 1f)] public float appear;

            [Tooltip("How far it flattens as it lands: 0.2 is down to 80% of its height.")]
            [Range(0f, 0.5f)] public float squash;

            [Tooltip("Seconds the wobble after landing takes to die away.")]
            public float wobble;

            [Tooltip("Seconds before it starts: one of a run of floor tiles waiting its turn.")]
            public float delay;

            public Shape(float drop, float fall, float appear, float squash, float wobble, float delay = 0f)
            {
                this.drop = drop;
                this.fall = fall;
                this.appear = appear;
                this.squash = squash;
                this.wobble = wobble;
                this.delay = delay;
            }
        }

        /// <summary>How much wider it goes for each bit shorter: a squash bulges, a stretch thins.</summary>
        const float Bulge = 0.6f;

        /// <summary>How long it is stretched by the time it lands, for each metre it fell from.</summary>
        const float StretchPerMetre = 0.11f;

        /// <summary>The most one frame moves it on. Putting something down can hitch a frame - the
        /// object is made or taken from its pool - and that frame should not swallow the fall.</summary>
        const float LongestStep = 1f / 20f;

        Shape shape;
        Vector3 rest;
        Vector3 baseScale;
        Vector3 centre;
        Quaternion turn;

        /// <summary>From the middle of its cells to its pivot, in its own turned axes, at its own size.</summary>
        Vector3 offset;

        Vector3 lastWritten;
        float elapsed;
        bool playing;
        bool landed;
        Action<PlacementPop> onLanded;

        public bool IsPlaying => playing;

        /// <summary>The middle of its cells on the ground: where it lands.</summary>
        public Vector3 Centre => centre;

        /// <summary>Lands <paramref name="target"/> where it now stands, about <paramref name="groundCentre"/>.</summary>
        public static PlacementPop Play(Transform target, Vector3 groundCentre, Shape shape, Action<PlacementPop> whenLanded = null)
        {
            if (target == null) return null;

            PlacementPop pop = target.GetComponent<PlacementPop>();
            if (pop == null) pop = target.gameObject.AddComponent<PlacementPop>();

            pop.Begin(groundCentre, shape, whenLanded);
            return pop;
        }

        /// <summary>Puts <paramref name="target"/> straight back in its pose, if it is landing: before
        /// something else moves it.</summary>
        public static void SettleOn(Transform target)
        {
            if (target == null) return;

            PlacementPop pop = target.GetComponent<PlacementPop>();
            if (pop != null) pop.Settle();
        }

        void Begin(Vector3 groundCentre, Shape next, Action<PlacementPop> whenLanded)
        {
            // Put down again while still landing: the size it had before is still the one to come back
            // to, and so is the place, unless whatever put it down has just moved it somewhere new.
            if (!playing) baseScale = transform.localScale;
            rest = playing && transform.position == lastWritten ? rest : transform.position;

            turn = transform.rotation;
            centre = new Vector3(groundCentre.x, rest.y, groundCentre.z);
            offset = Quaternion.Inverse(turn) * (centre - rest);

            shape = next;
            shape.fall = Mathf.Max(0.01f, shape.fall);
            shape.wobble = Mathf.Max(0.01f, shape.wobble);

            elapsed = 0f;
            landed = false;
            onLanded = whenLanded;
            playing = true;
            enabled = true;

            // Posed at once, so it is never drawn a frame in its final place before it drops.
            Pose(0f);
        }

        void LateUpdate()
        {
            if (!playing)
            {
                enabled = false;
                return;
            }

            // Real time, so it lands with the game paused too.
            elapsed += Mathf.Min(Time.unscaledDeltaTime, LongestStep);
            Pose(elapsed);
        }

        void OnDisable() => Settle();

        /// <summary>Back in its pose, at its own size, and idle. Left where it is if something else has
        /// moved it since this last did.</summary>
        public void Settle()
        {
            if (!playing) return;

            playing = false;
            transform.localScale = baseScale;
            if (transform.position == lastWritten) transform.position = rest;

            onLanded = null;
            enabled = false;
        }

        void Pose(float t)
        {
            t -= shape.delay;

            // Waiting its turn: not there yet.
            if (t < 0f)
            {
                Apply(shape.drop, Vector3.one * Mathf.Max(0.001f, shape.appear * 0.01f));
                return;
            }

            if (t < shape.fall)
            {
                float k = t / shape.fall;

                // Gathering speed as it falls, and stretching with it.
                float lift = shape.drop * (1f - k * k);
                float stretch = StretchPerMetre * shape.drop * k;
                float grow = Mathf.LerpUnclamped(shape.appear, 1f, BackOut(Mathf.Clamp01(k * 1.4f)));

                Apply(lift, new Vector3(1f - stretch * Bulge, 1f + stretch, 1f - stretch * Bulge) * grow);
                return;
            }

            if (!landed)
            {
                landed = true;
                Action<PlacementPop> call = onLanded;
                onLanded = null;
                call?.Invoke(this);
            }

            float u = t - shape.fall;
            if (u >= shape.wobble)
            {
                Settle();
                return;
            }

            // Flattened on the first frame down, then a spring back that dies away over the wobble.
            float decay = 4.6f / shape.wobble;
            float speed = 3f * Mathf.PI / shape.wobble;
            float height = 1f - shape.squash * Mathf.Exp(-decay * u) * Mathf.Cos(speed * u);
            float width = 1f + (1f - height) * Bulge;

            Apply(0f, new Vector3(width, height, width));
        }

        /// <summary>Lifts it and scales it by <paramref name="by"/> in its own axes, about the middle of its cells.</summary>
        void Apply(float lift, Vector3 by)
        {
            transform.localScale = Vector3.Scale(baseScale, by);
            transform.position = centre - turn * Vector3.Scale(offset, by) + Vector3.up * lift;
            lastWritten = transform.position;
        }

        static float BackOut(float t)
        {
            const float s = 1.70158f;
            t -= 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }
    }
}
