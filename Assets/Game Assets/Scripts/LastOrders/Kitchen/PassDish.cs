using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastOrders.Kitchen
{
    /// <summary>
    /// One plated dish: set on the serving pass by the kitchen, lifted off it by a waiter, carried to
    /// a table and eaten from there. The same object the whole way, so the dish a customer gets is the
    /// one that came out of the kitchen.
    ///
    /// Pooled by the <see cref="ServingPassBoard"/>; it never builds anything after warm-up. It only
    /// runs while it is moving - a hop, a pop - and switches itself off again as soon as it stops, so a
    /// pass full of plates costs nothing a frame.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PassDish : MonoBehaviour
    {
        enum Motion { None, Hop, Pop, Shrink }

        const float SquashTime = 0.16f, PopTime = 0.24f, ShrinkTime = 0.18f;

        MeshFilter filter;
        MeshRenderer view;

        Motion motion;
        bool arrived;
        float clock, duration, arc;
        Vector3 from, to, size = Vector3.one;
        Quaternion turnFrom, turnTo;
        Action landed;

        /// <summary>The dish this plate is showing.</summary>
        public ItemType Type { get; private set; }

        /// <summary>The board that pools it; null for a plate that outlived its pass.</summary>
        internal ServingPassBoard Board { get; set; }

        /// <summary>Its place on the pass, or -1 while off it: carried, on a table, pooled.</summary>
        internal int Spot { get; set; } = -1;

        /// <summary>True while it is flying somewhere; a waiter waits for the plate to land before lifting it.</summary>
        public bool IsMoving => motion == Motion.Hop;

        internal static PassDish Create(Transform parent)
        {
            var go = new GameObject("PassDish");
            go.transform.SetParent(parent, false);
            go.SetActive(false);

            var dish = go.AddComponent<PassDish>();
            dish.filter = go.AddComponent<MeshFilter>();
            dish.view = go.AddComponent<MeshRenderer>();

            // Small and flat: a shadow would cost a caster pass for a smudge nobody sees at this zoom.
            dish.view.shadowCastingMode = ShadowCastingMode.Off;
            dish.view.lightProbeUsage = LightProbeUsage.Off;
            dish.view.reflectionProbeUsage = ReflectionProbeUsage.Off;
            dish.enabled = false;
            return dish;
        }

        /// <summary>Shows <paramref name="type"/> from <paramref name="set"/>, still and at its own size.</summary>
        internal void Dress(ItemType type, PassDishSet set)
        {
            motion = Motion.None;
            landed = null;
            enabled = false;
            size = Vector3.one;
            transform.localScale = size;
            Type = type;
            if (set == null) return;

            filter.sharedMesh = set.MeshFor(type);
            view.sharedMaterial = set.Material;
        }

        /// <summary>Back to the pool - after a customer has eaten, or when it is no longer wanted.</summary>
        public void Recycle()
        {
            if (Board != null) Board.Recycle(this);
            else Destroy(gameObject);
        }

        // --- Motion -----------------------------------------------------------------------------------

        /// <summary>
        /// Up and over to <paramref name="target"/>, turning to <paramref name="rotation"/> on the way,
        /// landing with a squash. <paramref name="whenLanded"/> runs as it touches down.
        /// </summary>
        public void HopTo(Vector3 target, Quaternion rotation, float seconds, float height, Action whenLanded = null)
        {
            from = transform.position;
            to = target;
            turnFrom = transform.rotation;
            turnTo = rotation;
            duration = Mathf.Max(0.01f, seconds);
            arc = height;
            landed = whenLanded;
            arrived = false;
            size = Vector3.one;
            transform.localScale = size;
            Begin(Motion.Hop);
        }

        /// <summary>Appears where it stands, swelling up with a little overshoot.</summary>
        public void PopIn()
        {
            size = Vector3.one;
            transform.localScale = Vector3.zero;
            landed = null;
            Begin(Motion.Pop);
        }

        /// <summary>Shrinks away, then goes back to the pool.</summary>
        public void PopOut()
        {
            // As it stands: set on a table, it is scaled against the table's own parts.
            if (motion == Motion.None) size = transform.localScale;
            landed = null;
            Begin(Motion.Shrink);
        }

        /// <summary>Stops wherever it is: picked up mid-pop, say.</summary>
        public void Settle()
        {
            motion = Motion.None;
            transform.localScale = size;
            enabled = false;
        }

        void Begin(Motion next)
        {
            motion = next;
            clock = 0f;
            enabled = true;
        }

        void Update()
        {
            clock += Time.deltaTime;

            switch (motion)
            {
                case Motion.Hop:
                    StepHop();
                    break;

                case Motion.Pop:
                    float p = Mathf.Clamp01(clock / PopTime);
                    transform.localScale = size * OutBack(p);
                    if (p >= 1f) Settle();
                    break;

                case Motion.Shrink:
                    float s = Mathf.Clamp01(clock / ShrinkTime);
                    transform.localScale = size * (1f - s * s);
                    if (s >= 1f)
                    {
                        Settle();
                        Recycle();
                    }

                    break;

                default:
                    enabled = false;
                    break;
            }
        }

        void StepHop()
        {
            float t = Mathf.Clamp01(clock / duration);
            if (t < 1f)
            {
                float e = t * t * (3f - 2f * t);
                transform.SetPositionAndRotation(Vector3.Lerp(from, to, e) + Vector3.up * (arc * 4f * t * (1f - t)),
                                                 Quaternion.Slerp(turnFrom, turnTo, e));
                return;
            }

            if (!arrived)
            {
                arrived = true;
                transform.SetPositionAndRotation(to, turnTo);

                if (landed != null)
                {
                    Action callback = landed;
                    landed = null;
                    callback();

                    // The callback may have sent it on somewhere else, or put it away.
                    if (motion != Motion.Hop || !arrived) return;
                }
            }

            // A squash as it lands, wider and lower, springing back.
            float u = Mathf.Clamp01((clock - duration) / SquashTime);
            float squash = Mathf.Sin(u * Mathf.PI) * (1f - u);
            transform.localScale = Vector3.Scale(size, new Vector3(1f + 0.14f * squash, 1f - 0.22f * squash, 1f + 0.14f * squash));
            if (u >= 1f) Settle();
        }

        static float OutBack(float t)
        {
            const float c = 1.9f;
            float x = t - 1f;
            return 1f + (c + 1f) * x * x * x + c * x * x;
        }
    }
}
