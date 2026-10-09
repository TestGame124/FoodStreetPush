using UnityEngine;
using Random = UnityEngine.Random;

namespace LastOrders.Livestock
{
    /// <summary>
    /// The cow, moved by hand from her pieces: a slow four-legged walk on diagonal pairs with a
    /// nod of the head and a sway, the bell on her collar swinging with it. Stopped, she puts her
    /// head down to graze, chewing; looks round at something; shakes her head with her ears
    /// flapping; or lifts her head and moos - as she does when she has just been milked. Always,
    /// whatever else: the tail swishing and every so often flicking hard, an ear twitching, a blink,
    /// the slow breathing of something that size, the cud going round when she stands. At night
    /// she lies down under her roof, legs folded, eyes shut.
    ///
    /// Walking is read off how far her herd (<see cref="AnimalHerd"/>) moved her each frame. The
    /// parts are built by the Last Orders / Farming builder; this only moves them.
    /// </summary>
    [DefaultExecutionOrder(1300)]
    [DisallowMultipleComponent]
    public sealed class CowBody : MonoBehaviour, IAnimalBody
    {
        enum Act { None, Graze, Look, Shake, Moo }

        [Header("Parts")]
        [Tooltip("The whole cow, pivoted on the ground under her middle: it sways, breathes and lies down.")]
        [SerializeField] Transform model;

        [Tooltip("Head and neck, pivoted where the neck meets the shoulders.")]
        [SerializeField] Transform head;

        [Tooltip("The lower jaw, pivoted at its hinge: chews and moos.")]
        [SerializeField] Transform jaw;

        [SerializeField] Transform earLeft, earRight;

        [Tooltip("Both eyelids, pivoted at their tops: scaled down to nothing open, full size shut.")]
        [SerializeField] Transform lids;

        [Tooltip("The bell on her collar, pivoted at the strap.")]
        [SerializeField] Transform bell;

        [Tooltip("The tail from the root to halfway, and the rest with the tuft, a child of it.")]
        [SerializeField] Transform tail, tailTip;

        [Tooltip("The udder, pivoted where it hangs from her belly: it fills as the next churn comes on.")]
        [SerializeField] Transform udder;

        [Tooltip("Pivoted at the shoulders and hips.")]
        [SerializeField] Transform legFrontLeft, legFrontRight, legBackLeft, legBackRight;

        [Header("Feel")]
        [SerializeField, Min(0.05f)] float stepMetres = 0.42f;
        [SerializeField] float legSwingDegrees = 22f;
        [SerializeField] float bobHeight = 0.018f;
        [SerializeField] float swayDegrees = 2.5f;

        [Tooltip("How far down she goes lying: about her legs' length.")]
        [SerializeField] float lieDrop = 0.36f;

        Vector3 modelRest, modelScale, lidsScale;
        Quaternion modelTurn, headTurn, jawTurn, earLeftTurn, earRightTurn, bellTurn, tailTurn, tailTipTurn;
        Quaternion legFrontLeftTurn, legFrontRightTurn, legBackLeftTurn, legBackRightTurn;
        bool captured;

        Vector3 lastPosition;
        bool primed;
        float speed, moving, phase;

        Act act;
        float actClock, actLength, lookSide;

        bool asleep;
        float sleep;

        // At the hay rack: head up into it, chewing hard, a tug at the hay every so often.
        bool working;
        float work, nextTug, tugClock = -1f;
        float fill, fillTarget;
        Vector3 udderScale;
        Quaternion udderTurn;

        float earLeftFlick, earLeftVelocity, earRightFlick, earRightVelocity, nextEarLeft, nextEarRight;
        float blink, nextBlink;
        float swish, swishVelocity, nextSwish;
        float bellSwing, bellVelocity, lastHeadPitch;
        float breathOffset;

        public bool Busy => act != Act.None || (!asleep && sleep > 0.05f);

        void Awake() => Capture();

        void Capture()
        {
            if (captured) return;
            captured = true;

            if (model != null)
            {
                modelRest = model.localPosition;
                modelTurn = model.localRotation;
                modelScale = model.localScale;
            }

            headTurn = Rest(head);
            jawTurn = Rest(jaw);
            earLeftTurn = Rest(earLeft);
            earRightTurn = Rest(earRight);
            bellTurn = Rest(bell);
            tailTurn = Rest(tail);
            tailTipTurn = Rest(tailTip);
            legFrontLeftTurn = Rest(legFrontLeft);
            legFrontRightTurn = Rest(legFrontRight);
            legBackLeftTurn = Rest(legBackLeft);
            legBackRightTurn = Rest(legBackRight);
            if (lids != null) lidsScale = lids.localScale;
            if (udder != null)
            {
                udderScale = udder.localScale;
                udderTurn = udder.localRotation;
            }
        }

        static Quaternion Rest(Transform t) => t != null ? t.localRotation : Quaternion.identity;

        void OnEnable()
        {
            Capture();
            primed = false;
            speed = moving = 0f;
            phase = Random.value * Mathf.PI * 2f;
            act = Act.None;
            asleep = false;
            working = false;
            work = 0f;
            tugClock = -1f;
            fill = fillTarget = 0f;
            sleep = 0f;
            swish = swishVelocity = bellSwing = bellVelocity = 0f;
            earLeftFlick = earRightFlick = earLeftVelocity = earRightVelocity = 0f;
            breathOffset = Random.value * 10f;
            nextBlink = Random.Range(1f, 4f);
            nextSwish = Random.Range(2f, 5f);
            nextEarLeft = Random.Range(1f, 4f);
            nextEarRight = Random.Range(1f, 4f);
            Pose();
        }

        // --- What the herd asks of her -------------------------------------------------------------

        public float Idle()
        {
            if (asleep) return 0f;
            float roll = Random.value;
            if (roll < 0.6f) return Begin(Act.Graze);
            if (roll < 0.8f) return Begin(Act.Look);
            if (roll < 0.9f) return Begin(Act.Shake);
            return Begin(Act.Moo);
        }

        public void Call()
        {
            if (asleep) return;
            Begin(Act.Moo);
        }

        public void Work(bool on)
        {
            working = on;
            if (on)
            {
                act = Act.None;
                nextTug = Random.Range(0.6f, 1.6f);
            }
        }

        public void Filling(float amount) => fillTarget = Mathf.Clamp01(amount);

        public void Sleep(bool down)
        {
            asleep = down;
            if (down) act = Act.None;
        }

        float Begin(Act what)
        {
            act = what;
            actClock = 0f;

            switch (what)
            {
                case Act.Graze:
                    actLength = Random.Range(4f, 8f);
                    break;
                case Act.Look:
                    actLength = Random.Range(2f, 3.2f);
                    lookSide = Random.value < 0.5f ? -1f : 1f;
                    break;
                case Act.Shake:
                    actLength = 0.9f;
                    earLeftVelocity += 420f;
                    earRightVelocity -= 420f;
                    bellVelocity += 140f;
                    break;
                case Act.Moo:
                    actLength = 1.5f;
                    break;
            }

            return actLength;
        }

        // --- Every frame ------------------------------------------------------------------------------

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 position = transform.position;
            if (!primed || (position - lastPosition).sqrMagnitude > 4f)
            {
                primed = true;
                lastPosition = position;
            }

            Vector3 moved = position - lastPosition;
            moved.y = 0f;
            lastPosition = position;
            float distance = moved.magnitude;

            speed = Mathf.Lerp(speed, distance / dt, 1f - Mathf.Exp(-8f * dt));
            moving = Mathf.MoveTowards(moving, speed > 0.05f ? 1f : 0f, dt * 2.5f);

            float before = Mathf.Sin(phase);
            phase = Mathf.Repeat(phase + distance / stepMetres * Mathf.PI, Mathf.PI * 2000f);
            if (moving > 0.5f && Mathf.Sign(before) != Mathf.Sign(Mathf.Sin(phase))) bellVelocity += 60f * Mathf.Sign(before);

            sleep = Mathf.MoveTowards(sleep, asleep ? 1f : 0f, dt / (asleep ? 1.8f : 1.4f));
            work = Mathf.MoveTowards(work, working ? 1f : 0f, dt / 0.8f);

            // The udder fills slowly with the next churn and empties at once when she is milked.
            fill = fillTarget < fill - 0.2f ? fillTarget : Mathf.MoveTowards(fill, fillTarget, dt * 0.5f);

            if (work > 0.9f)
            {
                nextTug -= dt;
                if (nextTug <= 0f)
                {
                    nextTug = Random.Range(1.4f, 2.8f);
                    tugClock = 0f;
                    earLeftVelocity += 120f;
                    bellVelocity += 40f;
                }
            }

            if (tugClock >= 0f)
            {
                tugClock += dt;
                if (tugClock > 0.6f) tugClock = -1f;
            }

            if (act != Act.None)
            {
                actClock += dt;
                if (actClock >= actLength) act = Act.None;
            }

            // The little things that never stop while she is awake.
            bool awake = sleep < 0.5f;
            nextBlink -= dt;
            if (nextBlink <= 0f)
            {
                nextBlink = Random.Range(2.5f, 6f);
                blink = 0.16f;
            }

            blink = Mathf.Max(0f, blink - dt);

            nextSwish -= dt;
            if (nextSwish <= 0f)
            {
                nextSwish = Random.Range(2.5f, 7f);
                if (awake) swishVelocity += Random.value < 0.5f ? -260f : 260f;
            }

            nextEarLeft -= dt;
            if (nextEarLeft <= 0f)
            {
                nextEarLeft = Random.Range(1.5f, 5f);
                if (awake) earLeftVelocity += 320f;
            }

            nextEarRight -= dt;
            if (nextEarRight <= 0f)
            {
                nextEarRight = Random.Range(1.5f, 5f);
                if (awake) earRightVelocity -= 320f;
            }

            Spring(ref swish, ref swishVelocity, 0f, 30f, 4f, dt, 55f);
            Spring(ref earLeftFlick, ref earLeftVelocity, 0f, 160f, 9f, dt, 40f);
            Spring(ref earRightFlick, ref earRightVelocity, 0f, 160f, 9f, dt, 40f);
            Spring(ref bellSwing, ref bellVelocity, 0f, 60f, 3.5f, dt, 35f);

            Pose();

            // The bell swings when the head moves.
            float headPitchNow = head != null ? head.localEulerAngles.x : 0f;
            float delta = Mathf.DeltaAngle(lastHeadPitch, headPitchNow);
            lastHeadPitch = headPitchNow;
            bellVelocity -= delta * 6f;
        }

        void Pose()
        {
            float s = Smooth(sleep);
            float awake = 1f - s;
            float t = Time.time;
            float u = actLength > 0f ? Mathf.Clamp01(actClock / actLength) : 0f;

            // Walking: diagonal pairs.
            float swing = Mathf.Sin(phase) * legSwingDegrees * moving;
            float bob = Mathf.Abs(Mathf.Cos(phase)) * bobHeight * moving;
            float sway = Mathf.Sin(phase) * swayDegrees * moving;
            float nod = Mathf.Sin(phase * 2f) * 3f * moving;

            float headPitch = nod, headYaw = 0f, headRoll = 0f;
            float chew = moving < 0.3f ? 0.4f : 0f;
            float jawOpen = 0f, squash = 0f;

            switch (act)
            {
                case Act.Graze:
                {
                    // Head down to the grass and kept there, chewing, nosing a little to and fro.
                    float down = Mathf.Min(Smooth(u * actLength / 0.9f), Smooth((1f - u) * actLength / 0.8f));
                    headPitch += 52f * down;
                    headYaw = Mathf.Sin(t * 0.7f) * 9f * down;
                    chew = Mathf.Lerp(chew, 1f, down);
                    break;
                }

                case Act.Look:
                {
                    float turn = Mathf.Min(Smooth(u * actLength / 0.7f), Smooth((1f - u) * actLength / 0.7f));
                    headYaw = 34f * lookSide * turn;
                    headPitch -= 8f * turn;
                    headRoll = 6f * lookSide * turn;
                    chew *= 0.5f;
                    break;
                }

                case Act.Shake:
                {
                    float env = Mathf.Sin(u * Mathf.PI);
                    headRoll = Mathf.Sin(actClock * Mathf.PI * 6f) * 16f * env;
                    headYaw = Mathf.Sin(actClock * Mathf.PI * 6f + 0.6f) * 8f * env;
                    chew = 0f;
                    break;
                }

                case Act.Moo:
                {
                    // Head up and out, mouth open, the whole of her into it.
                    float up = Mathf.Min(Smooth(u / 0.2f), Smooth((1f - u) / 0.22f));
                    headPitch -= 20f * up;
                    jawOpen = 20f * Mathf.Min(Smooth((u - 0.12f) / 0.15f), Smooth((1f - u) / 0.28f));
                    squash = 0.025f * up;
                    chew = 0f;
                    break;
                }
            }

            // At the rack: head lifted into it, chewing hard, and a sharp tug down at the hay now and then.
            // A moo at the rack lifts her head out of it.
            float w = Smooth(work) * (act == Act.Moo ? 0.25f : 1f);
            if (w > 0f)
            {
                float tug = tugClock >= 0f ? Mathf.Sin(Mathf.Clamp01(tugClock / 0.6f) * Mathf.PI) : 0f;
                headPitch = Mathf.Lerp(headPitch, -10f + 16f * tug * tug, w);
                headYaw = Mathf.Lerp(headYaw, Mathf.Sin(t * 0.8f) * 5f, w);
                chew = Mathf.Lerp(chew, 1f, w);
            }

            float breath = Mathf.Sin((t + breathOffset) * Mathf.PI * 2f / Mathf.Lerp(3f, 4.5f, s)) * 0.012f;

            if (model != null)
            {
                model.localPosition = modelRest + Vector3.up * (bob - lieDrop * s);
                model.localRotation = modelTurn * Quaternion.Euler(0f, 0f, sway * awake + 4f * s);
                model.localScale = Vector3.Scale(modelScale,
                    new Vector3(1f + breath + squash * 0.5f, 1f + breath * 0.4f - squash, 1f + squash * 0.5f));
            }

            if (head != null)
                head.localRotation = headTurn * Quaternion.Euler(headPitch * awake + 12f * s, headYaw * awake + 22f * s, headRoll * awake);

            if (jaw != null)
            {
                float chewing = Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 2f * 1.4f)) * 7f * chew * awake;
                jaw.localRotation = jawTurn * Quaternion.Euler(chewing + jawOpen, Mathf.Sin(t * Mathf.PI * 2f * 0.7f) * 4f * chew * awake, 0f);
            }

            if (earLeft != null) earLeft.localRotation = earLeftTurn * Quaternion.Euler(0f, 0f, earLeftFlick * awake - 12f * s);
            if (earRight != null) earRight.localRotation = earRightTurn * Quaternion.Euler(0f, 0f, earRightFlick * awake + 12f * s);

            if (lids != null)
            {
                float shut = Mathf.Max(s, blink > 0f ? 1f : 0f);
                lids.localScale = Vector3.Scale(lidsScale, new Vector3(1f, Mathf.Lerp(0.02f, 1f, shut), 1f));
            }

            if (bell != null) bell.localRotation = bellTurn * Quaternion.Euler(bellSwing, 0f, 0f);

            if (udder != null)
            {
                // Fuller as the next churn comes on, swinging a little as she walks.
                float size = Mathf.Lerp(0.8f, 1.14f, Smooth(fill));
                udder.localScale = Vector3.Scale(udderScale, new Vector3(size, Mathf.Lerp(0.75f, 1.18f, Smooth(fill)), size));
                udder.localRotation = udderTurn * Quaternion.Euler(Mathf.Sin(phase) * 6f * moving, 0f, Mathf.Sin(phase * 0.5f) * 5f * moving);
            }

            // A lazy swish all the time, the flicks on top; the tip lags the root, so it whips.
            float lazy = Mathf.Sin(t * 0.9f) * 9f * awake;
            if (tail != null) tail.localRotation = tailTurn * Quaternion.Euler(sway * 1.5f, lazy + swish, 0f);
            if (tailTip != null)
                tailTip.localRotation = tailTipTurn * Quaternion.Euler(0f, Mathf.Sin(t * 0.9f - 1.1f) * 12f * awake + swish * 0.7f, 0f);

            // Lying: the front legs fold back under her chest, the back ones forward and out to the side.
            Leg(legFrontLeft, legFrontLeftTurn, swing * awake + 82f * s, -10f * s);
            Leg(legBackRight, legBackRightTurn, swing * awake - 80f * s, 18f * s);
            Leg(legFrontRight, legFrontRightTurn, -swing * awake + 82f * s, 10f * s);
            Leg(legBackLeft, legBackLeftTurn, -swing * awake - 80f * s, -18f * s);
        }

        static void Leg(Transform leg, Quaternion rest, float pitch, float roll)
        {
            if (leg != null) leg.localRotation = rest * Quaternion.Euler(pitch, 0f, roll);
        }

        static void Spring(ref float value, ref float velocity, float target, float k, float c, float dt, float limit)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / 0.01f));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                velocity += (k * (target - value) - c * velocity) * h;
                value += velocity * h;
            }

            value = Mathf.Clamp(value, -limit, limit);
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Wires the parts in, for the builder that makes them.</summary>
        public void Configure(Transform whole, Transform headAndNeck, Transform lowerJaw, Transform leftEar, Transform rightEar,
                              Transform eyelids, Transform collarBell, Transform tailRoot, Transform tailEnd, Transform frontLeft,
                              Transform frontRight, Transform backLeft, Transform backRight, Transform bag = null)
        {
            udder = bag;
            model = whole;
            head = headAndNeck;
            jaw = lowerJaw;
            earLeft = leftEar;
            earRight = rightEar;
            lids = eyelids;
            bell = collarBell;
            tail = tailRoot;
            tailTip = tailEnd;
            legFrontLeft = frontLeft;
            legFrontRight = frontRight;
            legBackLeft = backLeft;
            legBackRight = backRight;
            captured = false;
        }
    }
}
