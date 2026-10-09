using UnityEngine;
using Random = UnityEngine.Random;

namespace LastOrders.Livestock
{
    /// <summary>
    /// A hen, moved by hand from her pieces: short quick steps with the legs scissoring and the
    /// head held still then jerked forward at every one, as hens walk; the head never still for
    /// long even standing, ticking round to look with one eye then the other. Stopped, she pecks at
    /// the ground in quick runs, scratches - a kick back with each foot and a puff of dust - looks
    /// about, or flaps up with a few feathers loose. A hen that has just laid, or is startled by a
    /// hand in the nest box, flaps and stretches up. At night she settles down with her feet
    /// tucked in, feathers puffed up, head drawn in.
    ///
    /// Walking is read off how far her herd (<see cref="AnimalHerd"/>) moved her each frame, so it
    /// always matches her speed. The parts are built by the Last Orders / Farming builder; this only
    /// moves them.
    /// </summary>
    [DefaultExecutionOrder(1300)]
    [DisallowMultipleComponent]
    public sealed class ChickenBody : MonoBehaviour, IAnimalBody
    {
        enum Act { None, Peck, Scratch, Look, Flap }

        [Header("Parts")]
        [Tooltip("The whole hen, pivoted on the ground between her feet: it bobs, leans and sits.")]
        [SerializeField] Transform model;

        [Tooltip("Head and neck, pivoted at the base of the neck: pecks, looks about, bobs.")]
        [SerializeField] Transform head;

        [SerializeField] Transform wingLeft, wingRight;

        [Tooltip("Pivoted where it joins the back.")]
        [SerializeField] Transform tail;

        [Tooltip("Pivoted at the hips.")]
        [SerializeField] Transform legLeft, legRight;

        [Header("Effects")]
        [Tooltip("A few feathers loose when she flaps. Shared with the rest of the pen, in world space.")]
        [SerializeField] ParticleSystem feathers;

        [Tooltip("A puff where she scratches. Shared with the rest of the pen, in world space.")]
        [SerializeField] ParticleSystem dust;

        [Header("Feel")]
        [Tooltip("Metres a step: short, a hen's stride is a busy one.")]
        [SerializeField, Min(0.02f)] float stepMetres = 0.1f;

        [SerializeField] float legSwingDegrees = 36f;
        [SerializeField] float bobHeight = 0.02f;
        [SerializeField] float waddleDegrees = 5f;

        [Tooltip("How far the head is left behind and then caught up at each step.")]
        [SerializeField] float headBob = 0.035f;

        [Tooltip("How far down she settles to sleep: her legs' length.")]
        [SerializeField] float sitDrop = 0.11f;

        Vector3 modelRest, modelScale, headRest;
        Quaternion modelTurn, headTurn, wingLeftTurn, wingRightTurn, tailTurn, legLeftTurn, legRightTurn;
        Vector3 legLeftScale, legRightScale;
        bool captured;

        Vector3 lastPosition;
        bool primed;
        float speed, moving, phase;

        Act act;
        float actClock, actLength;
        int actCount;
        bool kickedFirst, kickedSecond;

        bool asleep;
        float sleep;

        // Brooding in the nest box: sat down but awake, shuffling now and then to settle.
        bool working;
        float brood, nextShuffle, shuffleClock = -1f;

        float lookYaw, lookTilt, lookYawTarget, lookTiltTarget, nextLook, lookFar;
        float tailWag, tailWagVelocity;
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

            if (head != null)
            {
                headRest = head.localPosition;
                headTurn = head.localRotation;
            }

            if (wingLeft != null) wingLeftTurn = wingLeft.localRotation;
            if (wingRight != null) wingRightTurn = wingRight.localRotation;
            if (tail != null) tailTurn = tail.localRotation;
            if (legLeft != null)
            {
                legLeftTurn = legLeft.localRotation;
                legLeftScale = legLeft.localScale;
            }

            if (legRight != null)
            {
                legRightTurn = legRight.localRotation;
                legRightScale = legRight.localScale;
            }
        }

        void OnEnable()
        {
            Capture();
            primed = false;
            speed = moving = 0f;
            phase = Random.value * Mathf.PI * 2f;
            act = Act.None;
            asleep = false;
            sleep = 0f;
            working = false;
            brood = 0f;
            shuffleClock = -1f;
            tailWag = tailWagVelocity = 0f;
            breathOffset = Random.value * 10f;
            nextLook = Random.Range(0.2f, 1f);
            Pose(0f);
        }

        // --- What the herd asks of her -------------------------------------------------------------

        public float Idle()
        {
            if (asleep) return 0f;
            float roll = Random.value;
            if (roll < 0.45f) return Begin(Act.Peck);
            if (roll < 0.7f) return Begin(Act.Scratch);
            if (roll < 0.93f) return Begin(Act.Look);
            return Begin(Act.Flap);
        }

        public void Call()
        {
            if (asleep) return;
            Begin(Act.Flap);
        }

        public void Sleep(bool down)
        {
            asleep = down;
            if (down) act = Act.None;
        }

        public void Work(bool on)
        {
            working = on;
            if (!on) return;
            nextShuffle = Random.Range(1f, 2.5f);
        }

        /// <summary>A hen does not show how far on her egg is.</summary>
        public void Filling(float amount) { }

        float Begin(Act what)
        {
            act = what;
            actClock = 0f;
            kickedFirst = kickedSecond = false;

            switch (what)
            {
                case Act.Peck:
                    actCount = Random.Range(2, 5);
                    actLength = 0.25f + actCount * 0.32f;
                    break;
                case Act.Scratch:
                    actLength = 1.3f;
                    break;
                case Act.Look:
                    actLength = Random.Range(1.1f, 2f);
                    lookFar = Random.value < 0.5f ? -48f : 48f;
                    break;
                case Act.Flap:
                    actLength = 0.85f;
                    Emit(feathers, model != null ? model.position + Vector3.up * 0.28f : transform.position, Random.Range(3, 6));
                    tailWagVelocity += 160f;
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
                // First frame, or set down somewhere new: not a stride.
                primed = true;
                lastPosition = position;
            }

            Vector3 moved = position - lastPosition;
            moved.y = 0f;
            lastPosition = position;
            float distance = moved.magnitude;

            speed = Mathf.Lerp(speed, distance / dt, 1f - Mathf.Exp(-10f * dt));
            moving = Mathf.MoveTowards(moving, speed > 0.06f ? 1f : 0f, dt * 5f);

            float before = Mathf.Sin(phase);
            phase = Mathf.Repeat(phase + distance / stepMetres * Mathf.PI, Mathf.PI * 2000f);
            if (moving > 0.5f && Mathf.Sign(before) != Mathf.Sign(Mathf.Sin(phase))) tailWagVelocity += 40f * (before > 0f ? 1f : -1f);

            sleep = Mathf.MoveTowards(sleep, asleep ? 1f : 0f, dt / (asleep ? 0.9f : 0.6f));
            brood = Mathf.MoveTowards(brood, working ? 1f : 0f, dt / 0.45f);

            // Sat on the nest, she shuffles round now and then to settle herself on it.
            if (brood > 0.9f)
            {
                nextShuffle -= dt;
                if (nextShuffle <= 0f)
                {
                    nextShuffle = Random.Range(2.2f, 4.5f);
                    shuffleClock = 0f;
                    tailWagVelocity += 120f;
                }
            }

            if (shuffleClock >= 0f)
            {
                shuffleClock += dt;
                if (shuffleClock > 0.7f) shuffleClock = -1f;
            }

            if (act != Act.None)
            {
                actClock += dt;
                if (actClock >= actLength) act = Act.None;
            }

            // The head is never still for long: a tick round to look with the other eye.
            nextLook -= dt;
            if (nextLook <= 0f)
            {
                nextLook = Random.Range(0.35f, 1.3f);
                lookYawTarget = Random.Range(-30f, 30f);
                lookTiltTarget = Random.Range(-14f, 14f);
            }

            // Snappy, as a bird's head moves: there almost at once, then held.
            float snap = 1f - Mathf.Exp(-22f * dt);
            lookYaw = Mathf.Lerp(lookYaw, lookYawTarget, snap);
            lookTilt = Mathf.Lerp(lookTilt, lookTiltTarget, snap);

            Spring(ref tailWag, ref tailWagVelocity, 0f, 90f, 7f, dt);
            Pose(dt);
        }

        void Pose(float dt)
        {
            float s = Smooth(sleep);
            float awake = 1f - s;
            float b = Smooth(brood);

            // How far down she sits: to sleep, or on the nest.
            float sit = Mathf.Max(s, b);
            float t = Time.time;
            float u = actLength > 0f ? Mathf.Clamp01(actClock / actLength) : 0f;

            // Walking.
            float swing = Mathf.Sin(phase) * legSwingDegrees * moving;
            float bob = Mathf.Abs(Mathf.Cos(phase)) * bobHeight * moving;
            float roll = Mathf.Sin(phase) * waddleDegrees * moving;
            float stride = (0.5f - Mathf.Repeat(phase / Mathf.PI, 1f)) * headBob * moving;

            float pitch = 0f, hop = 0f;
            float headPitch = 0f, headYaw = lookYaw * awake, headTilt = lookTilt * awake;
            float wingLift = 0f, kickLeft = 0f, kickRight = 0f;
            Vector3 headShift = Vector3.forward * stride;

            switch (act)
            {
                case Act.Peck:
                {
                    // Head down and a quick run of pecks, then back up.
                    float down = Mathf.Min(Smooth(u / 0.15f), Smooth((1f - u) / 0.15f));
                    float k = Mathf.Repeat(actClock - 0.12f, 0.32f) / 0.32f;
                    float jab = actClock > 0.12f && actClock < actLength - 0.12f ? Mathf.Sin(k * Mathf.PI) : 0f;
                    headPitch = 30f * down + 40f * jab * jab;
                    headShift += new Vector3(0f, -0.02f * down, 0.03f * down + 0.02f * jab);
                    pitch = 12f * down;
                    headYaw *= 0.3f;
                    break;
                }

                case Act.Scratch:
                {
                    // Lean in, kick back left, then right, a puff of dust with each.
                    pitch = 10f * Mathf.Min(Smooth(u / 0.15f), Smooth((1f - u) / 0.15f));
                    kickLeft = Kick(u, 0.12f, 0.42f) * -55f;
                    kickRight = Kick(u, 0.5f, 0.8f) * -55f;
                    headPitch = 14f * pitch / 10f;

                    Vector3 foot = model != null ? model.position - transform.forward * 0.1f : transform.position;
                    if (!kickedFirst && u > 0.27f)
                    {
                        kickedFirst = true;
                        Emit(dust, foot - transform.right * 0.05f, 2);
                    }

                    if (!kickedSecond && u > 0.65f)
                    {
                        kickedSecond = true;
                        Emit(dust, foot + transform.right * 0.05f, 2);
                    }

                    break;
                }

                case Act.Look:
                {
                    // A long look one way, a jerk round to the other.
                    float side = u < 0.5f ? 1f : -1f;
                    headYaw = Mathf.Lerp(headYaw, lookFar * side, Mathf.Min(Smooth(u / 0.08f), Smooth((1f - u) / 0.1f)));
                    headTilt = 18f * side;
                    headShift += Vector3.up * 0.015f;
                    break;
                }

                case Act.Flap:
                {
                    // Up on her toes, wings out and beating, a little hop, then settled again.
                    float env = Mathf.Sin(u * Mathf.PI);
                    wingLift = env * (40f + 35f * Mathf.Abs(Mathf.Sin(actClock * Mathf.PI * 7f)));
                    hop = Mathf.Max(0f, Mathf.Sin(Mathf.Clamp01(u / 0.6f) * Mathf.PI)) * 0.1f;
                    pitch = -14f * env;
                    headPitch = -20f * env;
                    headShift += Vector3.up * 0.03f * env;
                    break;
                }
            }

            // Brooding: wings let a little loose, a shuffle side to side now and then.
            float shuffle = shuffleClock >= 0f ? Mathf.Sin(shuffleClock * 26f) * 7f * Mathf.Sin(shuffleClock / 0.7f * Mathf.PI) : 0f;
            wingLift = Mathf.Max(wingLift, 9f * b);

            // Breathing, and puffed up asleep or on the nest.
            float breath = Mathf.Sin((t + breathOffset) * Mathf.PI * 2f / Mathf.Lerp(1.1f, 2.2f, sit)) * 0.018f;

            if (model != null)
            {
                model.localPosition = modelRest + Vector3.up * (bob + hop - sitDrop * sit);
                model.localRotation = modelTurn * Quaternion.Euler(pitch * awake, shuffle * 0.6f, roll * awake + shuffle);
                model.localScale = Vector3.Scale(modelScale,
                    new Vector3(1f + 0.06f * sit + breath * 0.5f, 1f - 0.05f * sit + breath, 1f + 0.05f * sit + breath * 0.5f));
            }

            if (head != null)
            {
                // Asleep: drawn in, low and turned into her shoulder.
                Vector3 drawnIn = new Vector3(0f, -0.04f, -0.05f) * s;
                head.localPosition = headRest + headShift * awake + drawnIn;
                head.localRotation = headTurn * Quaternion.Euler(headPitch * awake + 24f * s, headYaw + 40f * s, headTilt);
            }

            if (wingLeft != null) wingLeft.localRotation = wingLeftTurn * Quaternion.Euler(0f, 0f, -wingLift);
            if (wingRight != null) wingRight.localRotation = wingRightTurn * Quaternion.Euler(0f, 0f, wingLift);
            if (tail != null) tail.localRotation = tailTurn * Quaternion.Euler(-6f * sit, tailWag, 0f);

            // Feet tucked up under her, asleep or on the nest.
            float stand = 1f - sit;
            if (legLeft != null)
            {
                legLeft.localRotation = legLeftTurn * Quaternion.Euler((swing + kickLeft) * stand + 70f * sit, 0f, 0f);
                legLeft.localScale = Vector3.Scale(legLeftScale, new Vector3(1f, 1f - 0.55f * sit, 1f));
            }

            if (legRight != null)
            {
                legRight.localRotation = legRightTurn * Quaternion.Euler((-swing + kickRight) * stand + 70f * sit, 0f, 0f);
                legRight.localScale = Vector3.Scale(legRightScale, new Vector3(1f, 1f - 0.55f * sit, 1f));
            }
        }

        /// <summary>A kick back between <paramref name="from"/> and <paramref name="to"/> of the act: quick out, slower back.</summary>
        static float Kick(float u, float from, float to)
        {
            if (u <= from || u >= to) return 0f;
            float k = (u - from) / (to - from);
            return k < 0.35f ? Smooth(k / 0.35f) : 1f - Smooth((k - 0.35f) / 0.65f);
        }

        static void Emit(ParticleSystem system, Vector3 at, int count)
        {
            if (system == null) return;
            if (!system.isPlaying) system.Play();
            var emit = new ParticleSystem.EmitParams { position = at, applyShapeToPosition = true };
            system.Emit(emit, count);
        }

        static void Spring(ref float value, ref float velocity, float target, float k, float c, float dt)
        {
            // Semi-implicit Euler, in steps short enough to stay stable at a low frame rate.
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / 0.01f));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                velocity += (k * (target - value) - c * velocity) * h;
                value += velocity * h;
            }

            value = Mathf.Clamp(value, -35f, 35f);
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Wires the parts in, for the builder that makes them.</summary>
        public void Configure(Transform whole, Transform headAndNeck, Transform leftWing, Transform rightWing, Transform tailFeathers,
                              Transform leftLeg, Transform rightLeg, ParticleSystem loose, ParticleSystem puff)
        {
            model = whole;
            head = headAndNeck;
            wingLeft = leftWing;
            wingRight = rightWing;
            tail = tailFeathers;
            legLeft = leftLeg;
            legRight = rightLeg;
            feathers = loose;
            dust = puff;
            captured = false;
        }
    }
}
