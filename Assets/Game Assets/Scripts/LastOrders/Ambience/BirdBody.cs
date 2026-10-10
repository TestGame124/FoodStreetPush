using UnityEngine;
using Random = UnityEngine.Random;

namespace LastOrders.Ambience
{
    /// <summary>
    /// A bird's pose, moved by hand from its pieces: the wings folded flat against the sides at rest
    /// and opening out to fly, beaten in a wave from shoulder to tip, held still to glide, thrown
    /// forward to flare and land; the head ticking about, pecking, tucked back to preen; the legs
    /// tucked up in flight.
    ///
    /// Where it goes and how it flies is <see cref="AmbientBird"/>'s; it sets <see cref="Spread"/>,
    /// <see cref="FlapHz"/> and the rest each frame and this follows. Settled on the ground or a perch
    /// it keeps itself busy - pecks, looks about, preens, ruffles, a crow caws - and steps are read off
    /// how far the bird was moved, so a pigeon's head bobs in time with its walk.
    ///
    /// The parts are built by the Last Orders / Ambience builder; this only moves them. Each wing is
    /// modelled spread flat out to the side (+X for the right), in two pieces hinged at the wrist.
    /// </summary>
    [DefaultExecutionOrder(1310)]
    [DisallowMultipleComponent]
    public sealed class BirdBody : MonoBehaviour
    {
        public enum Gait { Walk, Hop }

        enum Act { None, Peck, Look, Preen, Ruffle, Caw, Turn }

        [Header("Parts")]
        [Tooltip("The whole bird, pivoted on the ground between its feet.")]
        [SerializeField] Transform model;

        [Tooltip("Pivoted at the base of the neck.")]
        [SerializeField] Transform head;

        [Tooltip("Shoulder to wrist, pivoted at the shoulder.")]
        [SerializeField] Transform wingLeft, wingRight;

        [Tooltip("Wrist to tip, children of the inner wings, pivoted at the wrist.")]
        [SerializeField] Transform tipLeft, tipRight;

        [Tooltip("Pivoted where it joins the rump. Optional.")]
        [SerializeField] Transform tail;

        [Tooltip("Both legs, pivoted at the hips. Optional.")]
        [SerializeField] Transform legs;

        [Header("Kind")]
        [SerializeField] Gait gait = Gait.Walk;

        [Tooltip("Metres a step when walking.")]
        [SerializeField, Min(0.02f)] float stepMetres = 0.09f;

        [Tooltip("How far the head is thrust forward and drawn back with each step: a pigeon's bob.")]
        [SerializeField] float headBob = 0.03f;

        [Tooltip("How often it caws, a share of its idle acts. Crows.")]
        [SerializeField, Range(0f, 1f)] float cawShare;

        [Tooltip("How often it pecks, a share of its idle acts on the ground.")]
        [SerializeField, Range(0f, 1f)] float peckShare = 0.45f;

        [Header("Wings")]
        [Tooltip("Degrees each wing beats up and down from the level at full depth.")]
        [SerializeField] float flapDegrees = 52f;

        [Tooltip("Degrees the wings are held up in a V while gliding.")]
        [SerializeField] float dihedral = 9f;

        [Tooltip("Folded: degrees the wing is swung back to lie along the body.")]
        [SerializeField] float foldBack = 82f;

        [Tooltip("Folded: degrees the wing is turned about its length - past upright, so its leading edge " +
                 "leans in over the back and the two wings meet over it like a roof.")]
        [SerializeField] float foldTwist = -122f;

        [Tooltip("Folded: degrees the hand is turned at the wrist - x toward the leading edge, y in over the tail.")]
        [SerializeField] Vector2 foldTip = new Vector2(8f, 6f);

        [Tooltip("Folded: the hand's length, a share of its spread length - the primaries slid under each other.")]
        [SerializeField, Range(0.3f, 1f)] float foldHand = 0.62f;

        [Tooltip("Folded: how narrow the wing is front to back, the feathers closed up on each other.")]
        [SerializeField, Range(0.3f, 1f)] float foldNarrow = 0.7f;

        [Tooltip("Folded: degrees the folded wing's tip is raised over the tail.")]
        [SerializeField] float foldRaise = 4f;

        // --- What AmbientBird sets every frame ------------------------------------------------------

        /// <summary>0 folded, 1 open.</summary>
        public float Spread { get; set; }

        /// <summary>Wing beats a second.</summary>
        public float FlapHz { get; set; } = 6f;

        /// <summary>0 to 1: how deep the beats go. 0 with wings open is a glide.</summary>
        public float FlapDepth { get; set; }

        /// <summary>0 to 1: wings thrown forward and up, braking to land.</summary>
        public float Flare { get; set; }

        /// <summary>Degrees, nose up positive.</summary>
        public float Pitch { get; set; }

        /// <summary>Degrees, banking into a turn: positive rolls the right wing down.</summary>
        public float Roll { get; set; }

        /// <summary>0 tucked up, 1 hanging ready to land.</summary>
        public float LegsDown { get; set; } = 1f;

        /// <summary>True on the ground or a perch with nothing else to do: it fills the time itself.</summary>
        public bool Settled { get; set; }

        /// <summary>A short hop's height, metres: set while it hops.</summary>
        public float Lift { get; set; }

        /// <summary>True while an idle act is under way; the bird waits for it before stepping off.</summary>
        public bool Busy => act != Act.None;

        Vector3 modelRest, headRest;
        Quaternion modelTurn, headTurn, wingLeftTurn, wingRightTurn, tipLeftTurn, tipRightTurn, tailTurn, legsTurn;
        Vector3 modelScale, legsScale, tailScale, wingLeftScale, wingRightScale, tipLeftScale, tipRightScale;
        bool captured;

        float phase, depth, spread, flare, pitch, roll, legsDown;
        Vector3 lastPosition;
        bool primed;
        float walkPhase, moving;

        Act act;
        float actClock, actLength, nextAct;
        int actCount;
        float lookYaw, lookPitch, lookYawTarget, lookPitchTarget;
        float tailFlick;

        void Awake() => Capture();

        void OnEnable()
        {
            primed = false;
            act = Act.None;
            nextAct = Random.Range(0.3f, 1.5f);
            phase = Random.value * 10f;
        }

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

            if (wingLeft != null)
            {
                wingLeftTurn = wingLeft.localRotation;
                wingLeftScale = wingLeft.localScale;
            }

            if (wingRight != null)
            {
                wingRightTurn = wingRight.localRotation;
                wingRightScale = wingRight.localScale;
            }
            if (tipLeft != null)
            {
                tipLeftTurn = tipLeft.localRotation;
                tipLeftScale = tipLeft.localScale;
            }

            if (tipRight != null)
            {
                tipRightTurn = tipRight.localRotation;
                tipRightScale = tipRight.localScale;
            }
            if (tail != null)
            {
                tailTurn = tail.localRotation;
                tailScale = tail.localScale;
            }

            if (legs != null)
            {
                legsTurn = legs.localRotation;
                legsScale = legs.localScale;
            }
        }

        /// <summary>Snaps the pose straight to what it has been told, for a bird just placed.</summary>
        public void Snap()
        {
            spread = Spread;
            flare = Flare;
            pitch = Pitch;
            roll = Roll;
            legsDown = LegsDown;
            depth = FlapDepth;
            primed = false;
        }

        /// <summary>Poses it at once as it has been told, <paramref name="flapPhase"/> radians into a
        /// beat: for a picture taken in edit mode, where nothing updates.</summary>
        public void PoseNow(float flapPhase)
        {
            Capture();
            Snap();
            phase = flapPhase;
            act = Act.None;
            Pose();
        }

        /// <summary>Breaks off whatever it was doing on the ground: it is about to take off.</summary>
        public void Startle()
        {
            act = Act.None;
            lookYawTarget = 0f;
            lookPitchTarget = -10f;
            tailFlick = 1f;
        }

        void LateUpdate()
        {
            Capture();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Follow(dt);
            Walk(dt);
            if (Settled) Idle(dt);
            else act = Act.None;

            Pose();
        }

        /// <summary>Eases toward what AmbientBird asked for, so wings open and close over a moment.</summary>
        void Follow(float dt)
        {
            spread = Mathf.MoveTowards(spread, Spread, dt * 4.5f);
            flare = Mathf.MoveTowards(flare, Flare, dt * 4f);
            depth = Mathf.MoveTowards(depth, FlapDepth, dt * 3f);
            legsDown = Mathf.MoveTowards(legsDown, LegsDown, dt * 3f);
            pitch = Mathf.Lerp(pitch, Pitch, 1f - Mathf.Exp(-dt * 6f));
            roll = Mathf.Lerp(roll, Roll, 1f - Mathf.Exp(-dt * 5f));

            if (spread > 0.02f) phase += FlapHz * dt * Mathf.PI * 2f;
            tailFlick = Mathf.MoveTowards(tailFlick, 0f, dt * 4f);
        }

        /// <summary>Steps read off how far the bird moved on the ground this frame.</summary>
        void Walk(float dt)
        {
            Vector3 position = transform.position;
            if (!primed)
            {
                primed = true;
                lastPosition = position;
                return;
            }

            Vector3 step = position - lastPosition;
            lastPosition = position;
            step.y = 0f;

            float speed = spread < 0.5f ? step.magnitude / dt : 0f;
            moving = Mathf.MoveTowards(moving, Mathf.Clamp01(speed / 0.25f), dt * 6f);
            walkPhase += step.magnitude / stepMetres * Mathf.PI;
        }

        // --- Idle: what a settled bird does with itself ------------------------------------------------

        void Idle(float dt)
        {
            if (act == Act.None)
            {
                nextAct -= dt;
                if (nextAct <= 0f && moving < 0.1f) Begin();
                else if (act == Act.None && Random.value < dt * 0.6f) PickLook(25f);
            }
            else
            {
                actClock += dt;
                if (act == Act.Look && actClock > actLength * 0.5f && actCount > 0)
                {
                    actCount--;
                    PickLook(70f);
                    actClock = 0f;
                }

                if (actClock >= actLength)
                {
                    if (act == Act.Peck && --actCount > 0)
                    {
                        actClock = 0f;
                        actLength = Random.Range(0.22f, 0.32f);
                    }
                    else if (act == Act.Caw && --actCount > 0)
                    {
                        actClock = 0f;
                        actLength = Random.Range(0.45f, 0.6f);
                    }
                    else
                    {
                        act = Act.None;
                        lookYawTarget = 0f;
                        lookPitchTarget = 0f;
                        nextAct = Random.Range(0.4f, 2.2f);
                        if (Random.value < 0.35f) tailFlick = 1f;
                    }
                }
            }

            lookYaw = Mathf.Lerp(lookYaw, lookYawTarget, 1f - Mathf.Exp(-dt * 14f));
            lookPitch = Mathf.Lerp(lookPitch, lookPitchTarget, 1f - Mathf.Exp(-dt * 12f));
        }

        void Begin()
        {
            actClock = 0f;
            float pick = Random.value;
            bool onGround = transform.position.y < 0.2f;

            if (pick < cawShare)
            {
                act = Act.Caw;
                actCount = Random.Range(2, 5);
                actLength = Random.Range(0.45f, 0.6f);
            }
            else if (onGround && pick < cawShare + peckShare)
            {
                act = Act.Peck;
                actCount = Random.Range(2, 6);
                actLength = Random.Range(0.22f, 0.32f);
            }
            else if (pick < cawShare + peckShare + 0.12f)
            {
                act = Act.Preen;
                actLength = Random.Range(1f, 2.2f);
            }
            else if (pick < cawShare + peckShare + 0.2f)
            {
                act = Act.Ruffle;
                actLength = 0.7f;
            }
            else
            {
                act = Act.Look;
                actCount = Random.Range(1, 3);
                actLength = Random.Range(0.8f, 1.6f);
                PickLook(70f);
            }
        }

        void PickLook(float range)
        {
            lookYawTarget = Random.Range(-range, range);
            lookPitchTarget = Random.Range(-15f, 12f);
        }

        // --- The pose ---------------------------------------------------------------------------------------

        void Pose()
        {
            float t = act == Act.None ? 0f : Mathf.Clamp01(actClock / Mathf.Max(0.01f, actLength));
            float bump = Mathf.Sin(t * Mathf.PI);

            // Head: looking about, pecks driven down and back, a preen turned right round, caws thrown up.
            float headYaw = lookYaw, headPitch = lookPitch;
            Vector3 headShift = Vector3.zero;
            float bodyDip = 0f, puff = 0f, ruffleLift = 0f;

            switch (act)
            {
                case Act.Peck:
                    float stab = Mathf.Sin(Mathf.Clamp01(t * 1.6f) * Mathf.PI);
                    headPitch = Mathf.Lerp(lookPitch, 62f, stab);
                    headShift = new Vector3(0f, -0.02f, 0.025f) * stab;
                    bodyDip = 14f * stab;
                    break;
                case Act.Preen:
                    float side = Mathf.Sign(Mathf.Sin(actLength * 13f) + 0.01f);
                    headYaw = 125f * side * Mathf.Clamp01(bump * 1.6f);
                    headPitch = 35f * Mathf.Clamp01(bump * 1.6f) + Mathf.Sin(actClock * 40f) * 6f * bump;
                    puff = 0.04f * bump;
                    break;
                case Act.Ruffle:
                    puff = 0.12f * bump;
                    ruffleLift = bump;
                    headPitch = Mathf.Sin(actClock * 45f) * 10f * bump;
                    break;
                case Act.Caw:
                    float call = Mathf.Sin(Mathf.Clamp01(t * 1.4f) * Mathf.PI);
                    headPitch = -28f * call;
                    headShift = new Vector3(0f, 0.01f, 0.02f) * call;
                    bodyDip = 12f * call;
                    ruffleLift = 0.35f * call;
                    puff = 0.05f * call;
                    break;
            }

            // Walking: the pigeon's head thrust forward and held while the body catches up.
            float stride = Mathf.Sin(walkPhase);
            if (gait == Gait.Walk) headShift.z += headBob * moving * (Mathf.Repeat(walkPhase / Mathf.PI, 1f) < 0.5f ? 1f : -0.6f);

            if (head != null)
            {
                head.localPosition = headRest + headShift;
                head.localRotation = headTurn * Quaternion.Euler(headPitch * (1f - spread * 0.7f), headYaw * (1f - spread), 0f);
            }

            // Body: pitched and banked in the air, dipped to peck, rocking with each step, a hop's arc.
            if (model != null)
            {
                float rock = gait == Gait.Walk ? stride * 4f * moving : 0f;
                model.localPosition = modelRest + Vector3.up * (Lift + Mathf.Abs(stride) * 0.006f * moving);
                model.localRotation = modelTurn * Quaternion.Euler(-pitch + bodyDip, 0f, -roll + rock);
                model.localScale = modelScale * (1f + puff);
            }

            Wings(ruffleLift);

            if (tail != null)
            {
                float tailPitch = -tailFlick * 22f * Mathf.Sin(tailFlick * Mathf.PI) + pitch * 0.3f * spread - flare * 25f;
                tail.localRotation = tailTurn * Quaternion.Euler(tailPitch, 0f, 0f);
                tail.localScale = Vector3.Scale(tailScale, new Vector3(1f + 0.35f * Mathf.Max(spread, flare), 1f, 1f));
            }

            if (legs != null)
            {
                float swing = gait == Gait.Walk ? stride * 25f * moving : 0f;
                legs.localRotation = legsTurn * Quaternion.Euler(Mathf.Lerp(75f, swing, legsDown), 0f, 0f);
                legs.localScale = legsScale * Mathf.Lerp(0.55f, 1f, legsDown);
            }
        }

        void Wings(float ruffle)
        {
            // Spread: beating about the level, the tips trailing a moment behind the arms; a slight V
            // held to glide; thrown forward and up to flare.
            float beat = Mathf.Sin(phase) * depth;
            float lag = Mathf.Sin(phase - 0.9f) * depth;
            float openRoll = dihedral * (1f - depth) + beat * flapDegrees + flare * 28f;
            float openYaw = -flare * 24f + depth * 4f;
            float tipRoll = lag * flapDegrees * 0.55f + flare * 10f;
            float tipYaw = 8f * (1f - depth) - flare * 6f;

            // Folded: swung back along the body and turned on edge against the side. A ruffle lifts
            // them out a little.
            float foldRoll = foldRaise + ruffle * 18f;
            float foldYaw = foldBack - ruffle * 22f;

            float s = spread;
            float wingRoll = Mathf.Lerp(foldRoll, openRoll, s);
            float yaw = Mathf.Lerp(foldYaw, openYaw, s);
            float twist = Mathf.Lerp(foldTwist + ruffle * 25f, 0f, Ease(s));
            // Folded, the hand lies on back along the arm and in over the tail, its tip crossing its pair's.
            float tRoll = Mathf.Lerp(foldTip.y, tipRoll, s);
            float tYaw = Mathf.Lerp(-foldTip.x, tipYaw, s);

            // Scaled across the chord before it is turned, so it narrows front to back, not in length.
            var narrow = new Vector3(1f, 1f, Mathf.Lerp(foldNarrow, 1f, Ease(s)));
            if (wingRight != null)
            {
                wingRight.localRotation = wingRightTurn * Wing(yaw, wingRoll, twist);
                wingRight.localScale = Vector3.Scale(wingRightScale, narrow);
            }

            if (wingLeft != null)
            {
                wingLeft.localRotation = wingLeftTurn * Wing(-yaw, -wingRoll, twist);
                wingLeft.localScale = Vector3.Scale(wingLeftScale, narrow);
            }
            var shorter = new Vector3(Mathf.Lerp(foldHand, 1f, Ease(s)), 1f, 1f);
            if (tipRight != null)
            {
                tipRight.localRotation = tipRightTurn * Quaternion.AngleAxis(tYaw, Vector3.up) * Quaternion.AngleAxis(tRoll, Vector3.forward);
                tipRight.localScale = Vector3.Scale(tipRightScale, shorter);
            }

            if (tipLeft != null)
            {
                tipLeft.localRotation = tipLeftTurn * Quaternion.AngleAxis(-tYaw, Vector3.up) * Quaternion.AngleAxis(-tRoll, Vector3.forward);
                tipLeft.localScale = Vector3.Scale(tipLeftScale, shorter);
            }
        }

        /// <summary>Twisted about its length first, then raised or lowered, then swung back.</summary>
        static Quaternion Wing(float yaw, float roll, float twist) =>
            Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(roll, Vector3.forward) * Quaternion.AngleAxis(twist, Vector3.right);

        static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Editor wiring, for the builder.</summary>
        public void Configure(Transform bodyModel, Transform headPart, Transform left, Transform right, Transform leftTip,
                              Transform rightTip, Transform tailPart, Transform legsPart, Gait walk, float step, float bob,
                              float caws, float pecks, float flap, float twist = -122f, float back = 82f, float raise = 4f)
        {
            foldTwist = twist;
            foldBack = back;
            foldRaise = raise;
            model = bodyModel;
            head = headPart;
            wingLeft = left;
            wingRight = right;
            tipLeft = leftTip;
            tipRight = rightTip;
            tail = tailPart;
            legs = legsPart;
            gait = walk;
            stepMetres = step;
            headBob = bob;
            cawShare = caws;
            peckShare = pecks;
            flapDegrees = flap;
            captured = false;
        }
    }
}
