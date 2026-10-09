using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LastOrders.Livestock
{
    /// <summary>
    /// Walks a pen's animals about inside it: a stroll to somewhere nearby, a stop to peck or graze,
    /// another stroll, never through the fence, the coop, the trough or each other. At dusk each
    /// goes to its bed - the hens under their house, the cow under her roof - and settles down; with
    /// the morning they get up and wander off again.
    ///
    /// While the building is making something one of them is at work, where the work is done: a hen
    /// walks to the nest box, hops up into it and broods there until her egg comes, then flaps and
    /// hops down and another takes her turn; the cow goes to the hay rack and eats in bouts. Fed,
    /// the hens crowd the feeder to peck. With something waiting to be collected, or the trough
    /// empty, they call out every so often - more often once the nest boxes are full.
    ///
    /// Everything is in the pen's own space, so a pen bought, turned or moved in the shop takes its
    /// animals with it. Not on the nav grid and not one of the steering agents: a pen is small, its
    /// walls are its own rectangle, and the zombies' crowd has no business pushing hens about.
    ///
    /// What an animal does when it stops, how it walks and how it works are its body's (<see cref="IAnimalBody"/>).
    /// </summary>
    [DefaultExecutionOrder(1180)]
    public sealed class AnimalHerd : MonoBehaviour
    {
        [Serializable]
        public struct Bed
        {
            [Tooltip("Where it sleeps, in the pen's space.")]
            public Vector3 position;

            [Tooltip("Which way it faces asleep, degrees about Y in the pen's space.")]
            public float yaw;

            public Bed(Vector3 position, float yaw)
            {
                this.position = position;
                this.yaw = yaw;
            }
        }

        [Serializable]
        public struct WorkSpot
        {
            [Tooltip("Where it walks to on the ground, in the pen's space.")]
            public Vector3 approach;

            [Tooltip("Where it works: the same as the approach, or up off the ground (the nest box), hopped up to.")]
            public Vector3 seat;

            [Tooltip("Which way it faces working, degrees about Y in the pen's space.")]
            public float yaw;

            public WorkSpot(Vector3 approach, Vector3 seat, float yaw)
            {
                this.approach = approach;
                this.seat = seat;
                this.yaw = yaw;
            }

            public bool Raised => (seat - approach).sqrMagnitude > 1e-4f;
        }

        enum Mode { Pausing, Walking, ToBed, Asleep, Waking, ToWork, Hop, Working }

        sealed class Walker
        {
            public Transform Body;
            public IAnimalBody Animal;
            public Vector3 Home;
            public Quaternion HomeTurn;
            public Mode Mode;
            public Vector3 Target;
            public float Speed;
            public float Pause;
            public float CallIn = -1f;
            public float Stuck;
            public int Bed;

            // A hop up to the work or back down from it.
            public Vector3 HopFrom, HopTo;
            public float HopClock;
            public Mode AfterHop;
        }

        [Tooltip("The building they belong to: it says when it is bedtime, when they are fed, when one has been laid or milked.")]
        [SerializeField] LivestockPen pen;

        [Tooltip("The animals, children of this transform.")]
        [SerializeField] Transform[] animals;

        [Tooltip("Where they may walk, x and z in this transform's space: inside the fence.")]
        [SerializeField] Rect area = new Rect(-1f, -1f, 2f, 2f);

        [Tooltip("Round things in the pen to walk round, (x, z, radius) in this transform's space: the house's legs, the feeder, the trough.")]
        [SerializeField] Vector3[] keepOut = Array.Empty<Vector3>();

        [Tooltip("Where they sleep, shared out in turn.")]
        [SerializeField] Bed[] beds = Array.Empty<Bed>();

        [Header("Work")]
        [Tooltip("Where one of them goes to make the next egg or churn.")]
        [SerializeField] WorkSpot work;

        [SerializeField] bool hasWork;

        [Tooltip("Stays at it until the egg comes (a hen in the nest box), rather than eating in bouts (the cow at the rack).")]
        [SerializeField] bool workUntilMade = true;

        [Tooltip("Seconds of a bout at the work, shortest to longest, when not working until it is made.")]
        [SerializeField] Vector2 boutSeconds = new Vector2(8f, 14f);

        [Tooltip("Seconds before the next goes to work.")]
        [SerializeField] Vector2 workRest = new Vector2(2f, 5f);

        [Tooltip("What shows the work going on: straw rustled out of the nest, hay falling from the rack.")]
        [SerializeField] ParticleSystem workEffect;

        [SerializeField] float workEffectRate = 2f;

        [Tooltip("Where they gather to eat when fed (pen space): round the feeder.")]
        [SerializeField] Vector3[] feedSpots = Array.Empty<Vector3>();

        [Tooltip("Seconds they crowd the feeder after a feed.")]
        [SerializeField] float feedingSeconds = 9f;

        [Header("Feel")]
        [Tooltip("Metres per second, slowest to fastest stroll.")]
        [SerializeField] Vector2 speedRange = new Vector2(0.45f, 0.9f);

        [Tooltip("Seconds stood about between strolls, besides whatever it finds to do.")]
        [SerializeField] Vector2 pauseRange = new Vector2(0.4f, 2.2f);

        [Tooltip("How far one stroll goes at most.")]
        [SerializeField, Min(0.3f)] float strollReach = 2f;

        [Tooltip("Degrees a second it turns.")]
        [SerializeField] float turnSpeed = 420f;

        [Tooltip("Turns on the spot until it faces within this many degrees of where it is going; 0 walks and turns at once. A cow does not strafe.")]
        [SerializeField] float turnBeforeWalking;

        [Tooltip("Its half width, kept clear of the fence and of the others.")]
        [SerializeField, Min(0.05f)] float bodyRadius = 0.18f;

        [Tooltip("How far its nose reaches in front of its middle, and its rump behind: both kept inside the fence, " +
                 "so a cow never grazes through the rails. Nothing for a hen.")]
        [SerializeField, Min(0f)] float reachAhead, reachBehind;

        [Tooltip("How often a stop is spent doing something (pecking, grazing) rather than just standing.")]
        [SerializeField, Range(0f, 1f)] float idleChance = 0.8f;

        [Tooltip("Quicker on the way to bed.")]
        [SerializeField] float bedtimeSpeed = 1.25f;

        [Tooltip("Seconds between calls while something waits to be collected or the trough is empty, shortest to longest. Halved when full.")]
        [SerializeField] Vector2 callEvery = new Vector2(7f, 12f);

        const float HopSeconds = 0.5f, HopHeight = 0.32f;

        Walker[] walkers = Array.Empty<Walker>();
        Walker worker;
        bool resting;
        float nextWork, workClock, workLength, nextCall, feedingUntil = -1f;

        public int Count => walkers.Length;

        /// <summary>Who is at the work, if anyone: for the tests and the debug overlay.</summary>
        public Transform Worker => worker != null && worker.Mode == Mode.Working ? worker.Body : null;

        void Awake()
        {
            if (pen == null) pen = GetComponentInParent<LivestockPen>();
            int count = animals != null ? animals.Length : 0;
            walkers = new Walker[count];

            for (int i = 0; i < count; i++)
            {
                Transform t = animals[i];
                walkers[i] = new Walker
                {
                    Body = t,
                    // Not ?.: a missing interface can come back as Unity's fake null.
                    Animal = t != null && t.TryGetComponent(out IAnimalBody body) ? body : null,
                    Home = t != null ? t.localPosition : Vector3.zero,
                    HomeTurn = t != null ? t.localRotation : Quaternion.identity,
                    Bed = i,
                };
            }
        }

        void OnEnable()
        {
            if (pen != null)
            {
                pen.Produced += HandleProduced;
                pen.Collected += HandleCollected;
                pen.Fed += HandleFed;
            }

            // Out of the pool or into the level: everyone back where the builder stood them, awake, idle.
            resting = false;
            worker = null;
            nextWork = Random.Range(workRest.x, workRest.y);
            nextCall = Random.Range(callEvery.x, callEvery.y);
            feedingUntil = -1f;
            foreach (Walker w in walkers)
            {
                if (w.Body == null) continue;
                w.Body.localPosition = w.Home;
                w.Body.localRotation = w.HomeTurn;
                w.Mode = Mode.Pausing;
                w.Pause = Random.Range(0f, pauseRange.y);
                w.CallIn = -1f;
                w.Stuck = 0f;
                if (w.Animal != null) w.Animal.Work(false);
            }

            if (workEffect != null)
            {
                var emission = workEffect.emission;
                emission.rateOverTime = 0f;
                if (!workEffect.isPlaying) workEffect.Play();
            }
        }

        void OnDisable()
        {
            if (pen == null) return;
            pen.Produced -= HandleProduced;
            pen.Collected -= HandleCollected;
            pen.Fed -= HandleFed;
        }

        void HandleProduced(LivestockPen from, int stored)
        {
            // The hen in the nest has laid: she stands, flaps and makes way. Otherwise whoever is nearest says so.
            if (worker != null && worker.Mode == Mode.Working && workUntilMade)
            {
                // Stepping down off the nest is a flap of its own.
                StopWork(worker, Mode.Pausing);
                return;
            }

            Walker w = worker ?? (walkers.Length > 0 ? walkers[Random.Range(0, walkers.Length)] : null);
            if (w != null && w.Mode != Mode.Asleep && w.CallIn < 0f) w.CallIn = Random.Range(0f, 0.2f);
        }

        void HandleCollected(LivestockPen from, int count)
        {
            // Somebody reaching into the nest: the hens make a fuss, the cow complains.
            foreach (Walker w in walkers)
                if (w.Mode != Mode.Asleep && w.Mode != Mode.Working && Random.value < 0.75f) w.CallIn = Random.Range(0f, 0.45f);
        }

        void HandleFed(LivestockPen from, int items)
        {
            // Food: everyone to the feeder, and back to work soon after.
            feedingUntil = Time.time + feedingSeconds;
            nextWork = Mathf.Min(nextWork, workUntilMade ? 2.5f : 0.3f);
            foreach (Walker w in walkers)
            {
                if (w.Mode != Mode.Pausing && w.Mode != Mode.Walking) continue;
                w.Mode = Mode.Pausing;
                w.Pause = Random.Range(0f, 0.6f);
            }
        }

        void Update()
        {
            if (pen != null && pen.Paused) return;

            bool bedtime = pen != null && pen.Resting;
            if (bedtime != resting)
            {
                resting = bedtime;
                foreach (Walker w in walkers) Bedtime(w, bedtime);
            }

            float dt = Time.deltaTime;
            if (!resting) Oversee(dt);

            float filling = pen == null ? 0f : pen.Full ? 1f : pen.Progress;
            foreach (Walker w in walkers)
            {
                if (w.Body == null) continue;
                if (w.Animal != null) w.Animal.Filling(filling);
                Tick(w, dt);
            }

            if (workEffect != null)
            {
                var emission = workEffect.emission;
                emission.rateOverTime = worker != null && worker.Mode == Mode.Working ? workEffectRate : 0f;
            }
        }

        /// <summary>Sends one to work when there is work, brings it back when the work stops, and has them call out.</summary>
        void Oversee(float dt)
        {
            if (pen == null) return;

            if (hasWork)
            {
                if (worker != null)
                {
                    bool stop = !pen.Producing;
                    if (worker.Mode == Mode.Working)
                    {
                        workClock += dt;
                        if (!workUntilMade && workClock >= workLength) stop = true;
                        if (workUntilMade && workClock >= 90f) stop = true;
                    }

                    if (stop && (worker.Mode == Mode.Working || worker.Mode == Mode.ToWork)) StopWork(worker, Mode.Pausing);
                }
                else if (pen.Producing)
                {
                    nextWork -= dt;
                    if (nextWork <= 0f) SendToWork();
                }
            }

            if (pen.Stored > 0 || pen.Hungry)
            {
                nextCall -= dt;
                if (nextCall <= 0f)
                {
                    float hurry = pen.Full || pen.Hungry ? 0.5f : 1f;
                    nextCall = Random.Range(callEvery.x, callEvery.y) * hurry;
                    Walker caller = walkers.Length > 0 ? walkers[Random.Range(0, walkers.Length)] : null;
                    if (caller != null && caller.Mode != Mode.Asleep && caller.Mode != Mode.Working && caller.Mode != Mode.Hop && caller.CallIn < 0f)
                        caller.CallIn = 0f;
                }
            }
        }

        void SendToWork()
        {
            // The nearest one standing about, so nobody crosses the whole pen to it.
            Walker best = null;
            float bestDistance = float.MaxValue;
            foreach (Walker w in walkers)
            {
                if (w.Body == null || (w.Mode != Mode.Pausing && w.Mode != Mode.Walking)) continue;
                float d = (w.Body.localPosition - work.approach).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = w;
                }
            }

            if (best == null)
            {
                nextWork = 1f;
                return;
            }

            worker = best;
            worker.Mode = Mode.ToWork;
            worker.Target = work.approach;
            worker.Speed = speedRange.y;
            worker.Stuck = 0f;
            workClock = 0f;
            workLength = Random.Range(boutSeconds.x, boutSeconds.y);
        }

        /// <summary>Ends the worker's turn: down off the nest if it is up there, then <paramref name="after"/>.</summary>
        void StopWork(Walker w, Mode after)
        {
            if (w.Animal != null) w.Animal.Work(false);
            if (w == worker) worker = null;
            nextWork = Random.Range(workRest.x, workRest.y);

            if (w.Body.localPosition.y > 0.02f)
            {
                Hop(w, w.Body.localPosition, work.approach, after);
                return;
            }

            w.Mode = after;
            if (after == Mode.Pausing) w.Pause = Random.Range(0.3f, 1.2f);
        }

        void Hop(Walker w, Vector3 from, Vector3 to, Mode after)
        {
            w.HopFrom = from;
            w.HopTo = to;
            w.HopClock = 0f;
            w.AfterHop = after;
            w.Mode = Mode.Hop;
            if (w.Animal != null) w.Animal.Call();
        }

        void Bedtime(Walker w, bool bed)
        {
            if (bed)
            {
                if (w == worker || w.Mode == Mode.Working || w.Mode == Mode.Hop)
                {
                    // Down from the nest first, then to bed.
                    if (w.Animal != null) w.Animal.Work(false);
                    if (w == worker) worker = null;
                    if (w.Body.localPosition.y > 0.02f || w.Mode == Mode.Hop)
                    {
                        Hop(w, w.Body.localPosition, work.approach, Mode.ToBed);
                        w.Target = BedOf(w).position;
                        w.Speed = speedRange.y * bedtimeSpeed;
                        return;
                    }
                }

                w.Mode = Mode.ToBed;
                w.Target = BedOf(w).position;
                w.Speed = speedRange.y * bedtimeSpeed;
                w.CallIn = -1f;
                return;
            }

            if (w.Mode == Mode.Asleep && w.Animal != null) w.Animal.Sleep(false);
            w.Mode = Mode.Waking;
            w.Pause = Random.Range(0.2f, 1.5f);
        }

        Bed BedOf(Walker w)
        {
            if (beds == null || beds.Length == 0) return new Bed(w.Home, w.HomeTurn.eulerAngles.y);
            return beds[w.Bed % beds.Length];
        }

        void Tick(Walker w, float dt)
        {
            if (w.CallIn >= 0f)
            {
                w.CallIn -= dt;
                // A hen brooding keeps quiet; the cow at the rack still moos when a churn fills.
                bool brooding = w.Mode == Mode.Working && workUntilMade;
                if (w.CallIn < 0f && w.Animal != null && w.Mode != Mode.Asleep && w.Mode != Mode.ToBed && !brooding) w.Animal.Call();
            }

            bool busy = w.Animal != null && w.Animal.Busy;

            switch (w.Mode)
            {
                case Mode.Asleep:
                case Mode.Working:
                    return;

                case Mode.Waking:
                case Mode.Pausing:
                    w.Pause -= dt;
                    if (w.Pause > 0f || busy) return;

                    w.Target = PickTarget(w);
                    w.Speed = Random.Range(speedRange.x, speedRange.y);
                    w.Stuck = 0f;
                    w.Mode = Mode.Walking;
                    return;

                case Mode.Walking:
                    if (busy) return;
                    if (Step(w, dt, true)) Stop(w);
                    return;

                case Mode.ToWork:
                    if (busy) return;
                    if (!Step(w, dt, true)) return;

                    // Hemmed in short of it: give up and let another try, rather than hop from wherever it stuck.
                    Vector3 gap = w.Body.localPosition - work.approach;
                    gap.y = 0f;
                    if (gap.sqrMagnitude > 0.35f * 0.35f)
                    {
                        StopWork(w, Mode.Pausing);
                        return;
                    }

                    if (work.Raised)
                    {
                        Hop(w, w.Body.localPosition, work.seat, Mode.Working);
                        return;
                    }

                    // Turned to the rack, then to it.
                    Quaternion face = Quaternion.Euler(0f, work.yaw, 0f);
                    w.Body.localRotation = Quaternion.RotateTowards(w.Body.localRotation, face, turnSpeed * dt);
                    if (Quaternion.Angle(w.Body.localRotation, face) > 4f) return;
                    BeginWork(w);
                    return;

                case Mode.Hop:
                    w.HopClock += dt;
                    float k = Mathf.Clamp01(w.HopClock / HopSeconds);
                    Vector3 p = Vector3.Lerp(w.HopFrom, w.HopTo, k) + Vector3.up * (4f * HopHeight * k * (1f - k));
                    w.Body.localPosition = p;

                    Vector3 across = w.HopTo - w.HopFrom;
                    across.y = 0f;
                    Quaternion aim = w.AfterHop == Mode.Working ? Quaternion.Euler(0f, work.yaw, 0f)
                        : across.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(across.normalized) : w.Body.localRotation;
                    w.Body.localRotation = Quaternion.RotateTowards(w.Body.localRotation, aim, turnSpeed * 1.5f * dt);
                    if (k < 1f) return;

                    w.Body.localPosition = w.HopTo;
                    if (w.AfterHop == Mode.Working)
                    {
                        w.Body.localRotation = aim;
                        BeginWork(w);
                    }
                    else
                    {
                        w.Mode = w.AfterHop;
                        if (w.Mode == Mode.Pausing) w.Pause = Random.Range(0.3f, 1f);
                    }

                    return;

                case Mode.ToBed:
                    if (busy) return;
                    if (!Step(w, dt, false)) return;

                    // In bed: turned the way it sleeps, then down.
                    Quaternion settle = Quaternion.Euler(0f, BedOf(w).yaw, 0f);
                    w.Body.localRotation = Quaternion.RotateTowards(w.Body.localRotation, settle, turnSpeed * dt);
                    if (Quaternion.Angle(w.Body.localRotation, settle) > 3f) return;

                    w.Mode = Mode.Asleep;
                    if (w.Animal != null) w.Animal.Sleep(true);
                    return;
            }
        }

        void BeginWork(Walker w)
        {
            w.Mode = Mode.Working;
            workClock = 0f;
            if (w.Animal != null) w.Animal.Work(true);
        }

        void Stop(Walker w)
        {
            w.Mode = Mode.Pausing;
            w.Pause = Random.Range(pauseRange.x, pauseRange.y);
            if (w.Animal != null && Random.value < idleChance) w.Pause = Mathf.Max(w.Pause, w.Animal.Idle() * 0.6f);
        }

        /// <summary>One frame's walking toward the target. True once it is there (or can get no nearer).</summary>
        bool Step(Walker w, float dt, bool keepInside)
        {
            Transform t = w.Body;
            Vector3 here = t.localPosition;
            Vector3 to = w.Target - here;
            to.y = 0f;
            float distance = to.magnitude;
            if (distance < 0.1f) return true;

            Vector3 direction = to / distance;

            // Shouldering past the others on the ground and round the things in the pen.
            Vector3 push = Vector3.zero;
            foreach (Walker other in walkers)
            {
                if (other == w || other.Body == null || other.Body.localPosition.y > 0.05f) continue;
                Vector3 offset = here - other.Body.localPosition;
                offset.y = 0f;
                float d = offset.magnitude, range = bodyRadius * 2.4f;
                if (d > 1e-4f && d < range) push += offset / d * (1f - d / range);
            }

            foreach (Vector3 k in keepOut)
            {
                var offset = new Vector3(here.x - k.x, 0f, here.z - k.y);
                float d = offset.magnitude, reach = k.z + bodyRadius + 0.35f;
                if (d > 1e-4f && d < reach) push += offset / d * (1f - (d - k.z - bodyRadius) / 0.35f);
            }

            Vector3 want = direction + Vector3.ClampMagnitude(push, 1f) * 1.2f;
            want.y = 0f;
            if (want.sqrMagnitude < 1e-6f) want = direction;

            Quaternion look = Quaternion.LookRotation(want.normalized, Vector3.up);
            float angle = Quaternion.Angle(t.localRotation, look);
            t.localRotation = Quaternion.RotateTowards(t.localRotation, look, turnSpeed * dt);
            if (turnBeforeWalking > 0f && angle > turnBeforeWalking && distance > 0.3f)
            {
                // Turning on the spot swings her head and rump round: keep both inside.
                if (keepInside) t.localPosition = KeepLength(here, t.localRotation);
                return false;
            }

            // Walks the way it faces, so a turn is an arc; straight in once it is close.
            Vector3 heading = distance > 0.6f ? t.localRotation * Vector3.forward : want.normalized;
            float stride = Mathf.Min(w.Speed * dt, distance);
            Vector3 next = here + heading * stride;
            next = Settle(next, keepInside);
            if (keepInside) next = KeepLength(next, t.localRotation);

            float moved = new Vector2(next.x - here.x, next.z - here.z).magnitude;
            t.localPosition = new Vector3(next.x, here.y, next.z);

            // Pressed against the fence or a mate and getting nowhere: call it there.
            w.Stuck = moved < stride * 0.25f ? w.Stuck + dt : 0f;
            return w.Stuck > 0.6f;
        }

        /// <summary>Back inside the fence and out of anything round in the pen.</summary>
        Vector3 Settle(Vector3 p, bool keepInside)
        {
            foreach (Vector3 k in keepOut)
            {
                var offset = new Vector3(p.x - k.x, 0f, p.z - k.y);
                float d = offset.magnitude, least = k.z + bodyRadius;
                if (d < least) p = d > 1e-4f ? new Vector3(k.x, p.y, k.y) + offset / d * least : p + Vector3.right * least;
            }

            if (!keepInside) return p;
            Rect inside = Inset();
            return new Vector3(Mathf.Clamp(p.x, inside.xMin, inside.xMax), p.y, Mathf.Clamp(p.z, inside.yMin, inside.yMax));
        }

        Rect Inset() => Inset(bodyRadius);

        Rect Inset(float margin)
        {
            float r = Mathf.Min(margin, Mathf.Min(area.width, area.height) * 0.45f);
            return new Rect(area.xMin + r, area.yMin + r, area.width - 2f * r, area.height - 2f * r);
        }

        /// <summary>
        /// <paramref name="p"/> moved just far enough that the animal's nose and rump, facing
        /// <paramref name="turn"/>, are both inside the fence.
        /// </summary>
        Vector3 KeepLength(Vector3 p, Quaternion turn)
        {
            if (reachAhead <= 0f && reachBehind <= 0f) return p;

            Rect inside = Inset(0.1f);
            Vector3 forward = turn * Vector3.forward;
            forward.y = 0f;

            // Twice round: pulling the nose in can push the rump out in a corner.
            for (int pass = 0; pass < 2; pass++)
            {
                p = Pull(p, forward * reachAhead, inside);
                p = Pull(p, -forward * reachBehind, inside);
            }

            return p;
        }

        /// <summary><paramref name="p"/> moved so that <paramref name="p"/> + <paramref name="offset"/> is inside <paramref name="inside"/>.</summary>
        static Vector3 Pull(Vector3 p, Vector3 offset, Rect inside)
        {
            Vector3 end = p + offset;
            return p + new Vector3(Mathf.Clamp(end.x, inside.xMin, inside.xMax) - end.x, 0f, Mathf.Clamp(end.z, inside.yMin, inside.yMax) - end.z);
        }

        Vector3 PickTarget(Walker w)
        {
            Vector3 here = w.Body.localPosition;

            // Just fed: to a place at the feeder, a little to one side or the other.
            if (Time.time < feedingUntil && feedSpots != null && feedSpots.Length > 0)
            {
                Vector3 spot = feedSpots[Random.Range(0, feedSpots.Length)];
                Vector2 jitter = Random.insideUnitCircle * 0.08f;
                return new Vector3(spot.x + jitter.x, here.y, spot.z + jitter.y);
            }

            // Far enough from the fence that she can stand there facing any way.
            Rect inside = Inset(Mathf.Max(bodyRadius, reachAhead));
            Vector3 best = here;
            float bestScore = float.MinValue;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector2 hop = Random.insideUnitCircle * strollReach;
                var p = new Vector3(Mathf.Clamp(here.x + hop.x, inside.xMin, inside.xMax), here.y,
                                    Mathf.Clamp(here.z + hop.y, inside.yMin, inside.yMax));

                float score = Mathf.Min(1f, hop.magnitude);
                foreach (Vector3 k in keepOut)
                {
                    float d = new Vector2(p.x - k.x, p.z - k.y).magnitude;
                    if (d < k.z + bodyRadius * 1.5f) score -= 5f;
                }

                foreach (Walker other in walkers)
                {
                    if (other == w || other.Body == null || other.Body.localPosition.y > 0.05f) continue;
                    Vector3 theirs = other.Mode == Mode.Walking ? other.Target : other.Body.localPosition;
                    float d = new Vector2(p.x - theirs.x, p.z - theirs.z).magnitude;
                    if (d < bodyRadius * 3f) score -= 2f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }

            return best;
        }

        /// <summary>Editor wiring, for the builder that makes the building.</summary>
        public void Configure(LivestockPen owner, Transform[] herd, Rect walkable, Vector3[] avoid, Bed[] sleeping, Vector2 speeds,
                              Vector2 pauses, float reach, float turn, float turnFirst, float radius, float busyChance, float noseReach = 0f,
                              float rumpReach = 0f)
        {
            reachAhead = noseReach;
            reachBehind = rumpReach;
            pen = owner;
            animals = herd;
            area = walkable;
            keepOut = avoid ?? Array.Empty<Vector3>();
            beds = sleeping ?? Array.Empty<Bed>();
            speedRange = speeds;
            pauseRange = pauses;
            strollReach = reach;
            turnSpeed = turn;
            turnBeforeWalking = turnFirst;
            bodyRadius = radius;
            idleChance = busyChance;
        }

        /// <summary>Editor wiring: the work and the feeder.</summary>
        public void ConfigureWork(WorkSpot spot, bool untilMade, Vector2 bouts, Vector2 rests, ParticleSystem effect, float effectRate,
                                  Vector3[] feeding, Vector2 calls)
        {
            hasWork = true;
            work = spot;
            workUntilMade = untilMade;
            boutSeconds = bouts;
            workRest = rests;
            workEffect = effect;
            workEffectRate = effectRate;
            feedSpots = feeding ?? Array.Empty<Vector3>();
            callEvery = calls;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.4f, 0.9f, 0.4f);
            Gizmos.DrawWireCube(new Vector3(area.center.x, 0.02f, area.center.y), new Vector3(area.width, 0.04f, area.height));

            Gizmos.color = new Color(0.95f, 0.5f, 0.3f);
            foreach (Vector3 k in keepOut) Gizmos.DrawWireSphere(new Vector3(k.x, 0.05f, k.y), k.z);

            Gizmos.color = new Color(0.4f, 0.6f, 1f);
            foreach (Bed b in beds)
            {
                Gizmos.DrawWireSphere(b.position, 0.12f);
                Gizmos.DrawLine(b.position, b.position + Quaternion.Euler(0f, b.yaw, 0f) * Vector3.forward * 0.3f);
            }

            if (!hasWork) return;
            Gizmos.color = new Color(1f, 0.85f, 0.3f);
            Gizmos.DrawWireSphere(work.approach, 0.1f);
            Gizmos.DrawWireSphere(work.seat, 0.14f);
            Gizmos.DrawLine(work.seat, work.seat + Quaternion.Euler(0f, work.yaw, 0f) * Vector3.forward * 0.3f);
        }
    }
}
