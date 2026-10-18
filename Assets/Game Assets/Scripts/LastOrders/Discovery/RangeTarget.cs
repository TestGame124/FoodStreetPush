using LastOrders.Defense;
using LastOrders.Interaction;
using UnityEngine;

namespace LastOrders.Discovery
{
    /// <summary>
    /// A cardboard zombie on the shooting range: a cut-out on a hinged foot, riding a runner across
    /// its lane. It flips up, slides along for a moment, and flips down again behind the berm; one
    /// hit while it is up knocks it flat. A shot still in the air when it drops is a miss.
    ///
    /// To the practice machine it is just another target, so the machine's own aim, projectiles and
    /// splash all work on it as they would on a zombie.
    /// </summary>
    public sealed class RangeTarget : MonoBehaviour, IDefenseTarget, ITappable, IInteractableObject
    {
        enum Pose
        {
            Down,
            Rising,
            Up,
            Falling,
            Hit,
        }

        [Tooltip("The cut-out on its hinge, facing its local -Z and turned about its local X: upright at 0, folded back flat behind the berm at Down Angle.")]
        [SerializeField] Transform hinge;

        [Tooltip("The trolley on the runner, moved between Rail A and Rail B.")]
        [SerializeField] Transform carriage;

        [Tooltip("The two ends of its runner, in the range's space.")]
        [SerializeField] Vector3 railA;
        [SerializeField] Vector3 railB;

        [SerializeField] float downAngle = 88f;
        [SerializeField] float hitAngle = 80f;
        [SerializeField] float flipSeconds = 0.22f;

        ShootingRange range;
        Pose pose = Pose.Down;
        float poseTime;
        float holdFor;
        float speed;
        float along;
        float direction = 1f;
        float angle;
        float fallFrom;

        public string DisplayName => "Cardboard zombie";

        /// <summary>Only while it stands upright: a target flipping down is already out of reach.</summary>
        public bool IsAlive => pose == Pose.Up || (pose == Pose.Rising && poseTime > flipSeconds * 0.5f);

        public Vector3 Position => carriage != null ? carriage.position : transform.position;

        public bool IsUp => pose == Pose.Up || pose == Pose.Rising;

        void Awake() => SetAngle(downAngle);

        /// <summary>Joins <paramref name="owner"/>'s round, starting at a random point on its runner.</summary>
        public void Attach(ShootingRange owner, float startAlong)
        {
            range = owner;
            along = Mathf.Clamp01(startAlong);
            PlaceCarriage();
        }

        /// <summary>Stands up and slides for <paramref name="seconds"/> at <paramref name="metresPerSecond"/>.</summary>
        public void PopUp(float seconds, float metresPerSecond, bool reverse)
        {
            holdFor = Mathf.Max(0.2f, seconds);
            speed = Mathf.Max(0f, metresPerSecond);
            if (reverse) direction = -direction;
            Enter(Pose.Rising);
        }

        /// <summary>Drops behind the berm, whatever it was doing.</summary>
        public void Drop()
        {
            if (pose == Pose.Down || pose == Pose.Falling) return;
            Enter(Pose.Falling);
        }

        public bool IsDown => pose == Pose.Down;

        public void ApplyDamage(float amount)
        {
            if (!IsAlive || amount <= 0f) return;

            Enter(Pose.Hit);
            if (range != null) range.KnockedDown(this);
        }

        void Update()
        {
            poseTime += Time.deltaTime;
            float t = Mathf.Clamp01(poseTime / flipSeconds);

            switch (pose)
            {
                case Pose.Rising:
                    SetAngle(Mathf.LerpUnclamped(downAngle, 0f, Back(t)));
                    Slide();
                    if (t >= 1f) Enter(Pose.Up);
                    break;

                case Pose.Up:
                    SetAngle(Mathf.Sin(poseTime * 9f) * 2.5f * Mathf.Exp(-poseTime * 3f));
                    Slide();
                    if (poseTime >= holdFor) Enter(Pose.Falling);
                    break;

                case Pose.Falling:
                    SetAngle(Mathf.Lerp(fallFrom, downAngle, t * t));
                    if (t >= 1f) Enter(Pose.Down);
                    break;

                case Pose.Hit:
                    // Knocked backwards hard, a bounce, then it settles flat and is lowered out of the way.
                    float knock = Mathf.Clamp01(poseTime / (flipSeconds * 0.8f));
                    SetAngle(Mathf.LerpUnclamped(0f, hitAngle, Back(knock)) + Mathf.Sin(poseTime * 22f) * 6f * Mathf.Exp(-poseTime * 6f));
                    if (poseTime >= 0.9f) Enter(Pose.Falling);
                    break;
            }
        }

        void Slide()
        {
            if (speed <= 0f) return;

            float length = Vector3.Distance(railA, railB);
            if (length < 0.01f) return;

            along += direction * speed * Time.deltaTime / length;
            if (along > 1f)
            {
                along = 2f - along;
                direction = -1f;
            }
            else if (along < 0f)
            {
                along = -along;
                direction = 1f;
            }

            PlaceCarriage();
        }

        void PlaceCarriage()
        {
            if (carriage == null) return;
            carriage.localPosition = Vector3.Lerp(railA, railB, Mathf.Clamp01(along));
        }

        void Enter(Pose next)
        {
            // From wherever it stands - upright, or knocked over by a hit - it folds the rest of the way down.
            if (next == Pose.Falling) fallFrom = angle;

            pose = next;
            poseTime = 0f;
        }

        void SetAngle(float degrees)
        {
            angle = degrees;
            if (hinge != null) hinge.localRotation = Quaternion.Euler(degrees, 0f, 0f);
        }

        /// <summary>An overshoot ease: past upright and back, the snap of a spring-loaded target.</summary>
        static float Back(float t)
        {
            const float s = 1.9f;
            t -= 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }

        public bool OnTouch() => TapFeedback.Report(OnTapped());

        public string OnTapped() => range != null ? range.Called(this) : null;

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(Transform cutOut, Transform trolley, Vector3 from, Vector3 to)
        {
            hinge = cutOut;
            carriage = trolley;
            railA = from;
            railB = to;
        }
    }
}
