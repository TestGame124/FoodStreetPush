using FoodStreet.Navigation;
using LastOrders.Audio;
using UnityEngine;

namespace LastOrders.Perimeter
{
    /// <summary>
    /// An automatic gate in the compound wall: two leaves that slide apart for anyone coming
    /// through, and never for a zombie.
    ///
    /// Nobody taps it. Cars, visitors and staff all walk authored routes, and every step tells
    /// the gates where it is heading; the gate whose opening that route runs through opens ahead
    /// of them and shuts again a moment after the last one is through. A zombie's steps ask
    /// nothing, so to a zombie the gate is just more wall - one it can batter down, after which
    /// it is a gap for everyone until dawn rebuilds it.
    ///
    /// While a zombie is close the gate will not start to open. Anyone who wants through waits
    /// at it, the lamps on the posts show red, and it opens for them the moment the zombie is
    /// dead or gone.
    ///
    /// Local X runs along the opening and the leaves slide along it, one each way, behind the
    /// wall on either side.
    /// </summary>
    public sealed class CompoundGate : CompoundStructure, IPassageGate
    {
        [SerializeField] float maxHealth = 450f;

        [Header("Opening")]
        [SerializeField] float openSeconds = 0.9f;
        [SerializeField] float closeSeconds = 1.2f;

        [Tooltip("Seconds it stays open after the last car or person is through.")]
        [SerializeField] float holdOpenSeconds = 1.5f;

        [Tooltip("With a zombie this close it stays shut, whoever is waiting.")]
        [SerializeField] float lockRadius = 6f;

        [Tooltip("How far from the gate line people stop while it is shut; cars stop this far plus " +
                 "half their length. Keep it short of the route points either side of the gate.")]
        [SerializeField] float waitDistance = 2f;

        [Header("Parts")]
        [Tooltip("Slides toward local -X to open.")]
        [SerializeField] Transform leftLeaf;

        [Tooltip("Slides toward local +X to open.")]
        [SerializeField] Transform rightLeaf;

        [Tooltip("How far each leaf slides, in metres. Half the opening clears it completely.")]
        [SerializeField] float slideDistance = 5f;

        [Tooltip("The leaves, the track and anything else that goes when it is knocked down.")]
        [SerializeField] GameObject standingVisuals;

        [Tooltip("What is left of it once it has been knocked down.")]
        [SerializeField] GameObject brokenVisuals;

        [Tooltip("The lamps on the posts: green open, amber shut, red while a zombie is keeping it shut.")]
        [SerializeField] Renderer[] lamps;

        [SerializeField] Color lampOpen = new Color(0.35f, 0.95f, 0.4f);
        [SerializeField] Color lampShut = new Color(1f, 0.72f, 0.2f);
        [SerializeField] Color lampLocked = new Color(1f, 0.22f, 0.18f);
        [SerializeField] Color lampOff = new Color(0.2f, 0.2f, 0.2f);

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        readonly GateMechanism mechanism = new GateMechanism();

        MaterialPropertyBlock block;
        Vector3 leftShut;
        Vector3 rightShut;
        Color lampShown = Color.clear;

        public override float MaxHealth => Mathf.Max(1f, maxHealth);

        /// <summary>0 shut, 1 fully open.</summary>
        public float OpenAmount => mechanism.OpenAmount;

        public bool IsLocked => mechanism.IsLocked;

        /// <summary>Standing, and shut enough to stop a zombie.</summary>
        public override bool IsInTheWay => base.IsInTheWay && mechanism.StopsHostiles;

        protected override void Awake()
        {
            base.Awake();

            if (leftLeaf != null) leftShut = leftLeaf.localPosition;
            if (rightLeaf != null) rightShut = rightLeaf.localPosition;
        }

        protected override void Update()
        {
            base.Update();

            mechanism.OpenSeconds = openSeconds;
            mechanism.CloseSeconds = closeSeconds;
            mechanism.HoldOpenSeconds = holdOpenSeconds;

            bool threatened = IsPresent && !IsDestroyed && composition != null &&
                              composition.Compound.HostileWithin(transform.position, lockRadius);

            mechanism.Tick(Time.deltaTime, threatened);
            SoundSliding();

            PoseLeaves();
            ShowLamps();
        }

        float heardOpen;
        int heardDirection;

        /// <summary>The leaves are heard as they start to slide, apart or back together - not while they stand still.</summary>
        void SoundSliding()
        {
            float open = mechanism.OpenAmount;
            float change = open - heardOpen;
            heardOpen = open;
            if (Mathf.Abs(change) < 1e-4f) return;

            int direction = change > 0f ? 1 : -1;
            if (direction == heardDirection) return;

            heardDirection = direction;
            SoundController.Play(direction > 0 ? SoundId.GateOpen : SoundId.GateClose, transform.position);
        }

        // --- Who gets through ---------------------------------------------------------------------

        public void RequestPassage(Vector3 from, Vector3 to, float reach)
        {
            if (!IsPresent || IsDestroyed) return;

            // A little wider than the opening, so a route that runs close to a post still counts.
            if (MovementBarriers.CrossesOpening(from, to, transform.position, Yaw, length * 0.5f + 0.5f, reach))
                mechanism.Request();
        }

        public override bool Blocks(Vector3 from, Vector3 to, float radius, MoverKind mover)
        {
            if (!IsPresent || IsDestroyed) return false;

            if (mover == MoverKind.Hostile)
            {
                return mechanism.StopsHostiles &&
                       MovementBarriers.StepEnters(from, to, transform.position, Yaw, HalfSize, radius);
            }

            // Everyone else waits short of it until it is open. Walking away, or along it, is never
            // refused, so nobody is caught in front of it as it shuts.
            return !mechanism.LetsFriendliesThrough && HeadsIntoShutGate(from, to, radius);
        }

        /// <summary>
        /// True for a step that closes on the leaves from in front of the opening, within the wait
        /// distance plus <paramref name="reach"/> - the length of bonnet ahead of the mover. Only
        /// the approach counts: a car on the road going past, however close, is never stopped.
        /// </summary>
        bool HeadsIntoShutGate(Vector3 from, Vector3 to, float reach)
        {
            Quaternion inverse = Quaternion.Euler(0f, -Yaw, 0f);
            Vector3 before = inverse * (from - transform.position);
            Vector3 after = inverse * (to - transform.position);

            float band = Mathf.Max(thickness * 0.5f, waitDistance) + Mathf.Max(0f, reach);

            if (Mathf.Abs(after.x) > length * 0.5f + 0.3f || Mathf.Abs(after.z) >= band) return false;

            return Mathf.Abs(after.z) < Mathf.Abs(before.z) - 1e-4f;
        }

        // --- Drawing ------------------------------------------------------------------------------

        void PoseLeaves()
        {
            // Eased at both ends, the way a motor starts and stops.
            float t = Mathf.SmoothStep(0f, 1f, mechanism.OpenAmount);

            if (leftLeaf != null) leftLeaf.localPosition = leftShut + Vector3.left * (slideDistance * t);
            if (rightLeaf != null) rightLeaf.localPosition = rightShut + Vector3.right * (slideDistance * t);
        }

        void ShowLamps()
        {
            if (lamps == null || lamps.Length == 0) return;

            Color colour = IsDestroyed ? lampOff
                : mechanism.IsLocked ? lampLocked
                : mechanism.OpenAmount > 0.01f ? lampOpen
                : lampShut;

            if (colour == lampShown) return;
            lampShown = colour;

            if (block == null) block = new MaterialPropertyBlock();

            for (int i = 0; i < lamps.Length; i++)
            {
                if (lamps[i] == null) continue;

                lamps[i].GetPropertyBlock(block);
                block.SetColor(BaseColorId, colour);
                block.SetColor(ColorId, colour);
                lamps[i].SetPropertyBlock(block);
            }
        }

        protected override void ApplyVisuals()
        {
            bool down = IsDestroyed;

            if (standingVisuals != null && standingVisuals.activeSelf == down) standingVisuals.SetActive(!down);
            if (brokenVisuals != null && brokenVisuals.activeSelf != down) brokenVisuals.SetActive(down);
        }

        protected override void OnFallen() => mechanism.Reset();

        protected override void OnRepaired(bool wasDown) => mechanism.Reset();

        /// <summary>Editor wiring for the moving parts, for the setup tool.</summary>
        public void ConfigureGate(float health, Transform left, Transform right, float slide, GameObject standing,
                                  GameObject broken, Renderer[] postLamps, float wait)
        {
            maxHealth = health;
            leftLeaf = left;
            rightLeaf = right;
            slideDistance = slide;
            standingVisuals = standing;
            brokenVisuals = broken;
            lamps = postLamps;
            waitDistance = wait;
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
            DrawCircle(transform.position + Vector3.up * 0.05f, lockRadius);
        }

        static void DrawCircle(Vector3 centre, float radius)
        {
            const int segments = 40;
            Vector3 previous = centre + new Vector3(radius, 0f, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = centre + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
