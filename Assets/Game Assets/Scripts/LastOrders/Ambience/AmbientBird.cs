using UnityEngine;
using Random = UnityEngine.Random;

namespace LastOrders.Ambience
{
    /// <summary>
    /// One of the level's wild birds: where it is and what it is doing - flying somewhere, coming in
    /// to land, settled on the ground or a perch, or bursting up off it.
    ///
    /// <see cref="AmbientBirds"/> gives the orders - fly there, land here, get away from that - and
    /// this carries them out the way the bird would: it turns at a bird's rate rather than snapping
    /// round, comes down a glide slope and in on a curve that bleeds off its speed, flares and drops
    /// its feet; it jumps up and away from a fright with the wings beating hard. Settled, it wanders
    /// about the spot it landed on - a pigeon walks, a sparrow hops - or shuffles round on a perch.
    /// <see cref="BirdBody"/> is told how to hold itself each frame.
    /// </summary>
    [DefaultExecutionOrder(1250)]
    [DisallowMultipleComponent]
    public sealed class AmbientBird : MonoBehaviour
    {
        public enum Kind { Pigeon, Sparrow, Crow, Bat }

        public enum State { Flying, Landing, Settled, TakingOff }

        [SerializeField] Kind kind;
        [SerializeField] BirdBody body;

        [Header("Flight")]
        [Tooltip("Metres a second, level flight.")]
        [SerializeField, Min(1f)] float cruiseSpeed = 7f;

        [Tooltip("Degrees a second it can turn.")]
        [SerializeField, Min(10f)] float turnDegrees = 150f;

        [Tooltip("Wing beats a second.")]
        [SerializeField, Min(1f)] float flapHz = 6.5f;

        [Tooltip("How much of level flight is spent gliding on still wings.")]
        [SerializeField, Range(0f, 1f)] float glideShare = 0.35f;

        [Tooltip("Small birds' flight: a burst of beats, then the wings shut a moment, rising and dipping.")]
        [SerializeField] bool bounding;

        [Header("On the ground")]
        [Tooltip("Metres a second walking. Hoppers ignore it.")]
        [SerializeField] float walkSpeed = 0.35f;

        [Tooltip("Metres a hop. Zero walks instead.")]
        [SerializeField] float hopMetres;

        [Header("Nerves")]
        [Tooltip("How close someone can come before it takes off, metres.")]
        [SerializeField, Min(0.5f)] float fright = 4f;

        public Kind Species => kind;
        public float Fright => fright;
        public float CruiseSpeed => cruiseSpeed;
        public State Current { get; private set; }
        public bool Perched { get; private set; }
        public bool Airborne => Current != State.Settled;
        public Vector3 Velocity => velocity;

        Vector3 velocity;
        float yawRate, lastYaw;

        // Flying toward a point, or toward a landing.
        Vector3 goal;
        float speedScale = 1f;
        bool landing;
        Vector3 landPoint;
        float landYaw;
        bool landPerched;
        float wanderRadius;

        // The landing: a curve from where the approach began down onto the spot.
        Vector3 b0, b1, b2, b3;
        float landSeconds;

        float clock;
        float startleDelay;
        Vector3 away;

        float pattern;
        bool gliding;

        // Settled.
        Vector3 home;
        Vector3 stepTarget;
        bool stepping;
        float nextWander;
        float hopClock = -1f, hopRest;
        Vector3 hopFrom, hopTo;
        float turnTo, turnClock = -1f;
        float facing;

        // --- Orders -----------------------------------------------------------------------------------------

        /// <summary>Appears at <paramref name="position"/> already flying.</summary>
        public void Launch(Vector3 position, Vector3 startVelocity)
        {
            gameObject.SetActive(true);
            transform.position = position;
            velocity = startVelocity;
            if (startVelocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(Flat(startVelocity));
            lastYaw = transform.eulerAngles.y;
            Current = State.Flying;
            Perched = false;
            landing = false;
            goal = position + startVelocity * 4f;

            body.Settled = false;
            body.Spread = 1f;
            body.FlapDepth = 0.8f;
            body.LegsDown = 0f;
            body.Flare = 0f;
            body.Snap();
        }

        /// <summary>Appears already sat at <paramref name="point"/>, for a level that opens with birds about.</summary>
        public void Settle(Vector3 point, float yaw, bool perched, float wander)
        {
            gameObject.SetActive(true);
            transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
            velocity = Vector3.zero;
            BeginSettled(point, yaw, perched, wander);
            body.Snap();
        }

        /// <summary>Flies toward <paramref name="point"/> without landing: a waypoint, a formation's
        /// slot, the way out. <paramref name="pace"/> scales its cruising speed.</summary>
        public void FlyTo(Vector3 point, float pace = 1f)
        {
            goal = point;
            speedScale = pace;
            landing = false;
            if (Current == State.Settled) TakeOff(Flat(point - transform.position), 0f);
        }

        /// <summary>Comes down onto <paramref name="point"/>, facing <paramref name="yaw"/> if it is a
        /// perch, then wanders up to <paramref name="wander"/> metres about it.</summary>
        public void LandAt(Vector3 point, float yaw, bool perched, float wander)
        {
            landing = true;
            landPoint = point;
            landYaw = yaw;
            landPerched = perched;
            wanderRadius = wander;
            speedScale = 1f;
            if (Current == State.Settled) TakeOff(Flat(point - transform.position), 0f);
        }

        /// <summary>
        /// Off the ground, away along <paramref name="direction"/> after <paramref name="delay"/> seconds:
        /// a flock does not go up as one, the nearest bird first and the rest a beat behind.
        /// </summary>
        public void TakeOff(Vector3 direction, float delay)
        {
            if (Current != State.Settled) return;

            away = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            away = Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f) * away;
            startleDelay = delay;
            Current = State.TakingOff;
            clock = 0f;
            body.Startle();
        }

        public bool HasArrived(float within) => !landing && (goal - transform.position).sqrMagnitude < within * within;

        // --- Each frame ------------------------------------------------------------------------------------

        void OnEnable()
        {
            lastYaw = transform.eulerAngles.y;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            switch (Current)
            {
                case State.Flying: Fly(dt); break;
                case State.Landing: Land(dt); break;
                case State.Settled: Sit(dt); break;
                case State.TakingOff: Rise(dt); break;
            }

            float yaw = transform.eulerAngles.y;
            float rate = Mathf.DeltaAngle(lastYaw, yaw) / dt;
            lastYaw = yaw;
            yawRate = Mathf.Lerp(yawRate, rate, 1f - Mathf.Exp(-dt * 8f));
        }

        void Fly(float dt)
        {
            Vector3 position = transform.position;
            Vector3 target = goal;
            float speed = cruiseSpeed * speedScale;

            if (landing)
            {
                // Down a glide slope toward the spot, then in on the landing curve.
                Vector3 flat = Flat(landPoint - position);
                float reach = flat.magnitude;
                float approach = Mathf.Clamp(cruiseSpeed * 0.7f, 3f, 6f);
                if (reach < approach && position.y - landPoint.y < approach * 1.2f)
                {
                    BeginLanding();
                    return;
                }

                target = landPoint + Vector3.up * Mathf.Clamp(reach * 0.45f, 1.2f, 25f);
                speed = Mathf.Lerp(cruiseSpeed * 0.75f, cruiseSpeed, Mathf.InverseLerp(approach, approach * 4f, reach));
            }
            else
            {
                // Easing into a moving slot or a waypoint rather than overshooting it.
                float gap = (goal - position).magnitude;
                if (gap < 4f) speed *= Mathf.Lerp(0.55f, 1f, gap / 4f);
            }

            Vector3 want = target - position;
            if (want.sqrMagnitude < 1e-4f) want = transform.forward;
            want.Normalize();
            want.y = Mathf.Clamp(want.y, -0.6f, 0.65f);
            want.Normalize();

            Vector3 dir = velocity.sqrMagnitude > 0.01f ? velocity.normalized : transform.forward;
            float turn = turnDegrees * (kind == Kind.Bat ? 2.2f : 1f);
            dir = Vector3.RotateTowards(dir, want, turn * Mathf.Deg2Rad * dt, 0f);

            if (kind == Kind.Bat)
            {
                // A bat's flight is a flicker of changes of mind.
                float flick = Mathf.PerlinNoise(Time.time * 3.1f, GetInstanceID() * 0.37f) - 0.5f;
                dir = Quaternion.Euler(0f, flick * 500f * dt, 0f) * dir;
            }

            float current = Mathf.MoveTowards(velocity.magnitude, speed, dt * 6f);
            velocity = dir * current;
            position += velocity * dt;

            if (bounding) position.y += Mathf.Sin(pattern * Mathf.PI * 2f / 0.67f) * 0.25f * dt;
            if (position.y < 0.8f) position.y = 0.8f;

            transform.position = position;
            Face(velocity, dt, 10f);
            Wingbeat(dt);
        }

        void BeginLanding()
        {
            Current = State.Landing;
            clock = 0f;

            b0 = transform.position;
            b3 = landPoint;
            Vector3 run = Flat(b3 - b0);
            float length = Mathf.Max(0.5f, (b3 - b0).magnitude);
            landSeconds = Mathf.Clamp(length / (cruiseSpeed * 0.55f), 0.9f, 2.6f);

            Vector3 heading = velocity.sqrMagnitude > 0.01f ? velocity.normalized : run.normalized;
            b1 = b0 + heading * (length * 0.35f);
            b1.y = Mathf.Max(b1.y, b3.y + 0.4f);
            b2 = b3 + Vector3.up * Mathf.Clamp(length * 0.25f, 0.4f, 1.2f) - run.normalized * Mathf.Min(0.5f, run.magnitude * 0.2f);
        }

        void Land(float dt)
        {
            clock += dt;
            float u = Mathf.Clamp01(clock / landSeconds);
            float s = 1f - (1f - u) * (1f - u);

            Vector3 before = transform.position;
            Vector3 at = Bezier(s);
            transform.position = at;
            velocity = (at - before) / dt;

            Vector3 tangent = Bezier(Mathf.Min(1f, s + 0.02f)) - Bezier(Mathf.Max(0f, s - 0.02f));
            Face(tangent, dt, 8f);

            body.Settled = false;
            body.Spread = 1f;
            body.Flare = AmbientWind.Smoothstep(0.5f, 0.88f, u);
            body.FlapDepth = Mathf.Lerp(0.25f, 1f, body.Flare);
            body.FlapHz = flapHz * 1.15f;
            body.LegsDown = AmbientWind.Smoothstep(0.35f, 0.75f, u);
            body.Pitch = Mathf.Lerp(-8f, 28f, body.Flare);
            body.Roll = 0f;

            if (u >= 1f) BeginSettled(landPoint, landPerched ? landYaw : transform.eulerAngles.y, landPerched, wanderRadius);
        }

        void BeginSettled(Vector3 point, float yaw, bool perched, float wander)
        {
            Current = State.Settled;
            Perched = perched;
            landing = false;
            home = point;
            wanderRadius = wander;
            facing = yaw;
            transform.SetPositionAndRotation(point, Quaternion.Euler(0f, yaw, 0f));
            velocity = Vector3.zero;
            stepping = false;
            hopClock = -1f;
            turnClock = -1f;
            nextWander = Random.Range(1f, 4f);

            body.Settled = true;
            body.Spread = 0f;
            body.Flare = 0f;
            body.FlapDepth = 0f;
            body.LegsDown = 1f;
            body.Pitch = 0f;
            body.Roll = 0f;
            body.Lift = 0f;
        }

        void Rise(float dt)
        {
            if (startleDelay > 0f)
            {
                startleDelay -= dt;
                // A beat crouched, head up, before the jump.
                body.Settled = false;
                body.Pitch = -8f;
                return;
            }

            if (clock == 0f)
            {
                velocity = away * Mathf.Max(2f, cruiseSpeed * 0.3f) + Vector3.up * 3.2f;
                Perched = false;
            }

            clock += dt;
            velocity += (away * 2.5f + Vector3.up * Mathf.Lerp(3f, 0f, clock / 0.7f)) * dt;
            transform.position += velocity * dt;
            Face(velocity, dt, 12f);

            body.Settled = false;
            body.Spread = 1f;
            body.FlapDepth = 1f;
            body.FlapHz = flapHz * 1.3f;
            body.Flare = 0f;
            body.LegsDown = Mathf.Clamp01(1f - clock * 2.5f);
            body.Pitch = 22f;
            body.Roll = 0f;
            body.Lift = 0f;

            if (clock >= 0.7f)
            {
                Current = State.Flying;
                if (!landing && (goal - transform.position).sqrMagnitude < 4f) goal = transform.position + away * 25f + Vector3.up * 8f;
            }
        }

        void Sit(float dt)
        {
            body.Settled = true;
            body.Spread = 0f;
            body.FlapDepth = 0f;
            body.LegsDown = 1f;
            body.Pitch = 0f;
            body.Roll = 0f;

            if (turnClock >= 0f)
            {
                // Turning round on a perch: a little hop, about-face.
                turnClock += dt;
                float u = Mathf.Clamp01(turnClock / 0.45f);
                transform.rotation = Quaternion.Euler(0f, Mathf.LerpAngle(facing, turnTo, u), 0f);
                body.Lift = Mathf.Sin(u * Mathf.PI) * 0.06f;
                if (u >= 1f)
                {
                    facing = turnTo;
                    turnClock = -1f;
                    body.Lift = 0f;
                }

                return;
            }

            if (hopClock >= 0f)
            {
                Hop(dt);
                return;
            }

            if (stepping)
            {
                Step(dt);
                return;
            }

            nextWander -= dt;
            if (nextWander > 0f || body.Busy) return;
            nextWander = Random.Range(1.5f, 5f);

            if (Perched)
            {
                if (Random.value < 0.5f)
                {
                    turnTo = facing + (Random.value < 0.5f ? 180f : Random.Range(-60f, 60f));
                    turnClock = 0f;
                }

                return;
            }

            if (wanderRadius <= 0.05f) return;

            Vector2 offset = Random.insideUnitCircle * wanderRadius;
            stepTarget = home + new Vector3(offset.x, 0f, offset.y);
            if (hopMetres > 0f)
            {
                hopClock = 0f;
                hopRest = 0f;
                NextHop();
            }
            else
            {
                stepping = true;
            }
        }

        /// <summary>Walking: turn toward the spot first, then step there.</summary>
        void Step(float dt)
        {
            Vector3 to = Flat(stepTarget - transform.position);
            if (to.magnitude < 0.03f)
            {
                stepping = false;
                facing = transform.eulerAngles.y;
                return;
            }

            float want = Quaternion.LookRotation(to).eulerAngles.y;
            float yaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, want, 260f * dt);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (Mathf.Abs(Mathf.DeltaAngle(yaw, want)) < 30f)
                transform.position = Vector3.MoveTowards(transform.position, new Vector3(stepTarget.x, home.y, stepTarget.z), walkSpeed * dt);
        }

        /// <summary>Hopping: quick little arcs, a beat between each.</summary>
        void Hop(float dt)
        {
            if (hopRest > 0f)
            {
                hopRest -= dt;
                return;
            }

            hopClock += dt;
            float u = Mathf.Clamp01(hopClock / 0.17f);
            transform.position = Vector3.Lerp(hopFrom, hopTo, u);
            body.Lift = Mathf.Sin(u * Mathf.PI) * 0.06f;

            if (u < 1f) return;

            body.Lift = 0f;
            if (Flat(stepTarget - transform.position).magnitude < 0.05f)
            {
                hopClock = -1f;
                facing = transform.eulerAngles.y;
                return;
            }

            hopRest = Random.Range(0.08f, 0.3f);
            NextHop();
        }

        void NextHop()
        {
            hopClock = 0f;
            hopFrom = transform.position;
            Vector3 to = Flat(stepTarget - hopFrom);
            float length = Mathf.Min(hopMetres * Random.Range(0.6f, 1.1f), to.magnitude);
            hopTo = hopFrom + (to.sqrMagnitude > 1e-4f ? to.normalized : transform.forward) * length;
            if (to.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(to);
        }

        /// <summary>How the wings work in level flight, by kind.</summary>
        void Wingbeat(float dt)
        {
            pattern += dt;
            body.Settled = false;
            body.Flare = 0f;
            body.LegsDown = 0f;
            body.FlapHz = flapHz;

            float climb = velocity.y;
            float speed = velocity.magnitude;

            if (kind == Kind.Bat)
            {
                body.Spread = 1f;
                body.FlapDepth = 1f;
            }
            else if (bounding)
            {
                // Beat, beat, beat - shut - beat: a finch's dipping flight.
                bool shut = Mathf.Repeat(pattern, 0.67f) > 0.45f && climb < 1.5f;
                body.Spread = shut ? 0.12f : 1f;
                body.FlapDepth = 1f;
            }
            else
            {
                if (pattern > 0f)
                {
                    gliding = !gliding && Random.value < glideShare * 1.6f;
                    pattern = gliding ? -Random.Range(0.6f, 1.5f) : -Random.Range(0.8f, 2f);
                }

                bool mustBeat = climb > 0.6f || speed < cruiseSpeed * speedScale * 0.8f;
                bool glide = (gliding || climb < -1.2f) && !mustBeat;
                body.Spread = 1f;
                body.FlapDepth = glide ? 0f : (climb > 0.5f ? 1f : 0.75f);
            }

            float level = Mathf.Max(0.1f, Flat(velocity).magnitude);
            body.Pitch = Mathf.Clamp(Mathf.Atan2(climb, level) * Mathf.Rad2Deg * 0.6f, -25f, 25f);
            body.Roll = Mathf.Clamp(yawRate * 0.22f, -45f, 45f);
        }

        void Face(Vector3 direction, float dt, float sharpness)
        {
            Vector3 flat = Flat(direction);
            if (flat.sqrMagnitude < 1e-4f) return;
            Quaternion want = Quaternion.LookRotation(flat);
            transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-dt * sharpness));
        }

        Vector3 Bezier(float s)
        {
            float r = 1f - s;
            return r * r * r * b0 + 3f * r * r * s * b1 + 3f * r * s * s * b2 + s * s * s * b3;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>Editor wiring, for the builder.</summary>
        public void Configure(Kind species, BirdBody bodyPart, float cruise, float turn, float beats, float glides,
                              bool bounds, float walk, float hop, float nerves)
        {
            kind = species;
            body = bodyPart;
            cruiseSpeed = cruise;
            turnDegrees = turn;
            flapHz = beats;
            glideShare = glides;
            bounding = bounds;
            walkSpeed = walk;
            hopMetres = hop;
            fright = nerves;
        }
    }
}
