using LastOrders.Audio;
using UnityEngine;

namespace FoodStreet.Visitors
{
    /// <summary>
    /// Toon life for a visitor's car: the body riding on its springs, the wheels rolling and
    /// steering, the door swinging open for whoever gets out or in, and the engine's chug and
    /// smoke.
    ///
    /// Purely a reader. It watches the car's <see cref="VisitorVehicle.Stage"/> and how its root
    /// moved since the last frame, and moves only its own parts - the body, the wheels, the doors
    /// - so the car drives exactly as it did before it had any of this. Everything is run off
    /// springs rather than keyframes: the body squats as it pulls away, dips its nose and squashes
    /// down when it stops, leans out of a turn, hops on every chug of an engine ticking over and
    /// rocks when a door is slammed. Exaggerated, as the rest of the game's props are.
    ///
    /// A parked car is quiet: engine off, door shut. Told to leave, it holds for
    /// <see cref="VisitorVehicle.PullAwaySeconds"/> - door open and slammed, the engine catching
    /// with a cough of smoke - before it rolls.
    /// </summary>
    [DefaultExecutionOrder(40)]
    public sealed class VehicleAnimation : MonoBehaviour
    {
        [SerializeField] VisitorVehicle vehicle;

        [Header("Parts")]
        [Tooltip("Everything sprung: the bodywork, cabin and load. Pivots about its own origin, " +
                 "which sits at axle height.")]
        [SerializeField] Transform body;

        [Tooltip("Wheel pivots at the axles, rolling about their local X.")]
        [SerializeField] Transform[] wheels;

        [Tooltip("One per wheel: whether it steers (the front pair).")]
        [SerializeField] bool[] steers;

        [SerializeField, Min(0.05f)] float wheelRadius = 0.4f;

        [Tooltip("Hinged parts that open at a stop, each turning about its local Y.")]
        [SerializeField] Transform[] doors;

        [Tooltip("One per door: degrees about its local Y when fully open.")]
        [SerializeField] float[] doorAngles;

        [Tooltip("Which side the doors are on, +1 right or -1 left: the body rocks toward them on a slam.")]
        [SerializeField] float doorSide = 1f;

        [Tooltip("Doors stay open the whole stop - a bus waiting for passengers - instead of " +
                 "shutting once the driver is out.")]
        [SerializeField] bool doorsOpenWhileParked;

        [Tooltip("The engine keeps running at a stop instead of being switched off.")]
        [SerializeField] bool idleWhileParked;

        [Header("Effects")]
        [Tooltip("Puffs from the tailpipe, emitted one per chug of the engine.")]
        [SerializeField] ParticleSystem exhaust;

        [Tooltip("The cough of smoke as the engine catches.")]
        [SerializeField] ParticleSystem backfire;

        [Tooltip("Dust kicked up behind the rear wheels while it is moving.")]
        [SerializeField] ParticleSystem[] wheelDust;

        [Tooltip("A puff of dust from under the wheels as it pulls up.")]
        [SerializeField] ParticleSystem stopDust;

        [Header("Feel")]
        [Tooltip("Degrees of nose-up per metre per second squared of acceleration.")]
        [SerializeField] float pitchPerAcceleration = 1.4f;

        [Tooltip("Degrees of lean per metre per second squared of cornering.")]
        [SerializeField] float rollPerCornering = 1.6f;

        [SerializeField] float maxPitch = 6f;
        [SerializeField] float maxRoll = 7f;

        [Tooltip("How hard the springs pull back, per second squared.")]
        [SerializeField] float stiffness = 95f;

        [Tooltip("How quickly the springs settle, per second.")]
        [SerializeField] float damping = 8.5f;

        [Tooltip("Seconds between chugs of the engine ticking over; quicker as the car speeds up.")]
        [SerializeField] float chugInterval = 0.4f;

        [Tooltip("How far the bodywork shakes while the engine runs, in metres.")]
        [SerializeField] float engineShake = 0.005f;

        [Tooltip("How high the road's bumps lift the body at speed, in metres.")]
        [SerializeField] float roadBumps = 0.025f;

        [Tooltip("Speed the dust starts at, metres per second.")]
        [SerializeField] float dustSpeed = 1.5f;

        // Rest poses, captured once so pooling and re-enabling never accumulate drift.
        Vector3 bodyPosition, bodyScale;
        Quaternion bodyRotation;
        Quaternion[] wheelRest, doorRest;
        bool captured;

        // Measured motion.
        Vector3 lastPosition;
        float lastYaw, speed, acceleration, yawRate, travelled;
        bool primed;

        // Springs: value and velocity.
        float pitch, pitchVelocity, roll, rollVelocity, lift, liftVelocity, squash, squashVelocity;
        float door, doorVelocity, doorTarget;

        float spin, steer, chugIn, startShake, seed;
        bool engineOn;

        VisitorVehicle.VehicleStage lastStage;

        /// <summary>
        /// Whoever is driving stays in their seat at every stop - a drive-through's lane, where the
        /// car creeps up a place at a time and the order comes out of the window - so the door stays
        /// shut and the engine ticks over.
        /// </summary>
        public bool DriverStaysIn { get; set; }

        // Scheduled moments, in Time.time; negative when nothing is due.
        float doorOpenAt = -1f, doorCloseAt = -1f, engineStartAt = -1f, engineStopAt = -1f;

        public void Configure(VisitorVehicle owner, Transform sprung, Transform[] wheelPivots, bool[] steering, float radius,
                              Transform[] hinged, float[] openAngles, float side, bool openWhileParked, bool idleAtStop,
                              ParticleSystem smoke, ParticleSystem cough, ParticleSystem[] dust, ParticleSystem pullUp)
        {
            vehicle = owner;
            body = sprung;
            wheels = wheelPivots;
            steers = steering;
            wheelRadius = radius;
            doors = hinged;
            doorAngles = openAngles;
            doorSide = side;
            doorsOpenWhileParked = openWhileParked;
            idleWhileParked = idleAtStop;
            exhaust = smoke;
            backfire = cough;
            wheelDust = dust;
            stopDust = pullUp;
        }

        void Awake()
        {
            if (vehicle == null) vehicle = GetComponent<VisitorVehicle>();
            Capture();
            seed = Random.value * 100f;
        }

        void Capture()
        {
            if (captured) return;
            captured = true;

            if (body != null)
            {
                bodyPosition = body.localPosition;
                bodyRotation = body.localRotation;
                bodyScale = body.localScale;
            }

            wheelRest = new Quaternion[wheels != null ? wheels.Length : 0];
            for (int i = 0; i < wheelRest.Length; i++) wheelRest[i] = wheels[i] != null ? wheels[i].localRotation : Quaternion.identity;

            doorRest = new Quaternion[doors != null ? doors.Length : 0];
            for (int i = 0; i < doorRest.Length; i++) doorRest[i] = doors[i] != null ? doors[i].localRotation : Quaternion.identity;
        }

        void OnEnable()
        {
            Capture();

            // Taken from the pool: every spring at rest, nothing scheduled, the doors shut.
            primed = false;
            speed = acceleration = yawRate = 0f;
            pitch = pitchVelocity = roll = rollVelocity = lift = liftVelocity = squash = squashVelocity = 0f;
            door = doorVelocity = doorTarget = 0f;
            startShake = 0f;
            doorOpenAt = doorCloseAt = engineStartAt = engineStopAt = -1f;

            lastStage = vehicle != null ? vehicle.Stage : VisitorVehicle.VehicleStage.Idle;
            engineOn = IsDriving(lastStage);

            if (exhaust != null) exhaust.Play();
            SetDust(false);
            Pose();
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || vehicle == null) return;

            Watch();
            Measure(dt);
            Schedule();
            Engine(dt);
            Springs(dt);
            Doors(dt);
            Wheels(dt);
            Pose();
        }

        // --- What the car is doing ----------------------------------------------------------------

        static bool IsDriving(VisitorVehicle.VehicleStage stage) =>
            stage == VisitorVehicle.VehicleStage.DrivingIn || stage == VisitorVehicle.VehicleStage.DrivingOut;

        void Watch()
        {
            VisitorVehicle.VehicleStage stage = vehicle.Stage;
            bool startingUp = vehicle.IsStartingUp;

            if (stage != lastStage)
            {
                // The car snaps to its bay on arrival and is moved about by the pool: neither is
                // driving, so the frame it happens is not measured.
                primed = false;

                if (stage == VisitorVehicle.VehicleStage.Parked) PulledUp();
                else if (stage == VisitorVehicle.VehicleStage.DrivingOut && startingUp) PullingAway();
                else if (IsDriving(stage) && !engineOn) StartEngine(quiet: true);
                else if (!IsDriving(stage)) engineOn = false;

                lastStage = stage;
            }

        }

        /// <summary>Come to a stop: nose down, squashed onto its springs, dust from the wheels, the door opening.</summary>
        void PulledUp()
        {
            pitchVelocity += 55f;
            squashVelocity += 1.4f;
            speed = 0f;
            acceleration = 0f;

            if (stopDust != null) stopDust.Play();

            if (DriverStaysIn)
            {
                doorOpenAt = doorCloseAt = engineStopAt = engineStartAt = -1f;
                return;
            }

            float now = Time.time;
            doorOpenAt = now + 0.2f;
            doorCloseAt = doorsOpenWhileParked ? -1f : now + 1.5f;
            engineStopAt = idleWhileParked ? -1f : now + 0.55f;
            engineStartAt = -1f;
        }

        /// <summary>Told to go from a standstill: the door opens and slams, then the engine catches.</summary>
        void PullingAway()
        {
            float now = Time.time;
            float hold = vehicle.PullAwaySeconds;

            if (DriverStaysIn)
            {
                doorOpenAt = doorCloseAt = engineStopAt = -1f;
                engineStartAt = engineOn ? -1f : now + Mathf.Max(0f, hold - 0.45f);
                return;
            }

            if (doorsOpenWhileParked)
            {
                doorOpenAt = -1f;
                doorCloseAt = now + 0.05f;
            }
            else
            {
                doorOpenAt = now;
                doorCloseAt = now + Mathf.Min(0.6f, hold * 0.45f);
            }

            engineStopAt = -1f;
            engineStartAt = engineOn ? -1f : now + Mathf.Max(0f, hold - 0.45f);
        }

        void Schedule()
        {
            float now = Time.time;

            if (doorOpenAt >= 0f && now >= doorOpenAt)
            {
                doorOpenAt = -1f;
                doorTarget = 1f;
            }

            if (doorCloseAt >= 0f && now >= doorCloseAt && doorOpenAt < 0f)
            {
                doorCloseAt = -1f;
                doorTarget = 0f;
                SoundController.Play(SoundId.CarDoor, transform.position);
            }

            if (engineStopAt >= 0f && now >= engineStopAt)
            {
                engineStopAt = -1f;
                StopEngine();
            }

            if (engineStartAt >= 0f && now >= engineStartAt)
            {
                engineStartAt = -1f;
                StartEngine(quiet: false);
            }
        }

        void StartEngine(bool quiet)
        {
            engineOn = true;
            chugIn = 0.1f;

            if (quiet) return;

            // It catches with a cough and a shudder: up on its springs, stretched, rocking.
            startShake = 1f;
            liftVelocity += 0.45f;
            squashVelocity -= 1.6f;
            rollVelocity += 30f * (Random.value < 0.5f ? -1f : 1f);
            if (backfire != null) backfire.Play();
        }

        void StopEngine()
        {
            if (!engineOn) return;

            // One last shudder as it dies.
            engineOn = false;
            rollVelocity += 18f;
            squashVelocity += 0.5f;
            Puff(2);
        }

        // --- Motion -------------------------------------------------------------------------------

        void Measure(float dt)
        {
            Vector3 position = transform.position;
            float yaw = transform.eulerAngles.y;

            if (!primed || (position - lastPosition).sqrMagnitude > 16f)
            {
                // A jump, not a drive: the pool or the bay moved it.
                primed = true;
                lastPosition = position;
                lastYaw = yaw;
                if (wheelDust != null)
                    foreach (ParticleSystem dust in wheelDust)
                        if (dust != null) dust.Clear();
                return;
            }

            Vector3 moved = position - lastPosition;
            float rawSpeed = Mathf.Clamp(Vector3.Dot(moved, transform.forward) / dt, -12f, 12f);
            float rawYawRate = Mathf.Clamp(Mathf.DeltaAngle(lastYaw, yaw) / dt, -240f, 240f);

            float before = speed;
            speed = Mathf.Lerp(speed, rawSpeed, 1f - Mathf.Exp(-14f * dt));
            acceleration = Mathf.Lerp(acceleration, Mathf.Clamp((speed - before) / dt, -14f, 14f), 1f - Mathf.Exp(-10f * dt));
            yawRate = Mathf.Lerp(yawRate, rawYawRate, 1f - Mathf.Exp(-10f * dt));
            travelled += Mathf.Abs(speed) * dt;

            lastPosition = position;
            lastYaw = yaw;

            SetDust(Mathf.Abs(speed) > dustSpeed && IsDriving(vehicle.Stage));
        }

        void Engine(float dt)
        {
            startShake = Mathf.MoveTowards(startShake, 0f, dt * 2.2f);
            if (!engineOn) return;

            // Putt-putt: a hop and a puff on every chug, quicker as it gets going.
            chugIn -= dt;
            if (chugIn > 0f) return;

            float pace = Mathf.Clamp01(Mathf.Abs(speed) / 6f);
            chugIn = chugInterval * Mathf.Lerp(1f, 0.45f, pace) * Random.Range(0.85f, 1.15f);

            liftVelocity += Mathf.Lerp(0.09f, 0.04f, pace);
            squashVelocity += 0.18f;
            Puff(1);
        }

        void Springs(float dt)
        {
            // Pitch: squat back under acceleration, nose down braking. Roll: lean out of the turn.
            float pitchTarget = Mathf.Clamp(-acceleration * pitchPerAcceleration, -maxPitch, maxPitch);
            float cornering = speed * yawRate * Mathf.Deg2Rad;
            float rollTarget = Mathf.Clamp(cornering * rollPerCornering, -maxRoll, maxRoll);

            float moving = Mathf.Clamp01(Mathf.Abs(speed) / 3f);
            float bump = (Mathf.PerlinNoise(travelled * 0.9f, seed) - 0.5f) * 2f * roadBumps * moving;

            Spring(ref pitch, ref pitchVelocity, pitchTarget, stiffness, damping, dt);
            Spring(ref roll, ref rollVelocity, rollTarget, stiffness, damping, dt);
            Spring(ref lift, ref liftVelocity, bump, stiffness * 1.6f, damping * 1.2f, dt);
            Spring(ref squash, ref squashVelocity, 0f, stiffness * 1.8f, damping * 1.1f, dt);

            squash = Mathf.Clamp(squash, -0.12f, 0.14f);
        }

        static void Spring(ref float value, ref float velocity, float target, float k, float c, float dt)
        {
            // Semi-implicit Euler, in steps short enough to stay stable at a low frame rate.
            int steps = Mathf.CeilToInt(dt / 0.01f);
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                velocity += (k * (target - value) - c * velocity) * h;
                value += velocity * h;
            }
        }

        void Doors(float dt)
        {
            if (doors == null || doors.Length == 0) return;

            bool closing = doorTarget <= 0f;
            Spring(ref door, ref doorVelocity, doorTarget, closing ? 160f : 70f, closing ? 6f : 9f, dt);

            if (closing && door <= 0f)
            {
                // Slammed: the door stops dead and the car rocks on its springs.
                if (doorVelocity < -1.5f)
                {
                    rollVelocity -= 26f * doorSide;
                    squashVelocity += 0.5f;
                }

                door = 0f;
                doorVelocity = 0f;
            }

            door = Mathf.Min(door, 1.12f);

            for (int i = 0; i < doors.Length; i++)
            {
                if (doors[i] == null) continue;
                float angle = doorAngles != null && i < doorAngles.Length ? doorAngles[i] : -65f;
                doors[i].localRotation = doorRest[i] * Quaternion.Euler(0f, angle * door, 0f);
            }
        }

        void Wheels(float dt)
        {
            if (wheels == null) return;

            spin = Mathf.Repeat(spin + speed * dt / wheelRadius * Mathf.Rad2Deg, 360f);

            // Turned into a bend by how fast the car is turning, held where it was at a stop.
            if (Mathf.Abs(speed) > 0.3f)
            {
                float target = Mathf.Clamp(yawRate * 0.32f * Mathf.Sign(speed), -32f, 32f);
                steer = Mathf.Lerp(steer, target, 1f - Mathf.Exp(-10f * dt));
            }

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null) continue;
                bool turns = steers != null && i < steers.Length && steers[i];
                wheels[i].localRotation = wheelRest[i] * Quaternion.Euler(0f, turns ? steer : 0f, 0f) * Quaternion.Euler(spin, 0f, 0f);
            }
        }

        void Pose()
        {
            if (body == null) return;

            float t = Time.time;
            float shake = engineOn ? engineShake * (1f + 3f * startShake) : 0f;
            float jitter = shake * (Mathf.Sin(t * 81f + seed) * 0.7f + Mathf.Sin(t * 133f + seed * 2f) * 0.3f);
            float wobble = shake * 50f * Mathf.Sin(t * 57f + seed);

            body.localPosition = bodyPosition + Vector3.up * (lift + jitter);
            body.localRotation = bodyRotation * Quaternion.Euler(pitch, 0f, roll + wobble);
            body.localScale = Vector3.Scale(bodyScale, new Vector3(1f + squash * 0.5f, 1f - squash, 1f + squash * 0.5f));
        }

        // --- Particles ----------------------------------------------------------------------------

        void Puff(int count)
        {
            if (exhaust == null) return;
            if (!exhaust.isPlaying) exhaust.Play();
            exhaust.Emit(count);
        }

        void SetDust(bool on)
        {
            if (wheelDust == null) return;

            foreach (ParticleSystem dust in wheelDust)
            {
                if (dust == null) continue;
                var emission = dust.emission;
                if (emission.enabled == on) continue;
                emission.enabled = on;
                if (on && !dust.isPlaying) dust.Play();
            }
        }
    }
}
