using System.Collections.Generic;
using FoodStreet.Visitors;
using LastOrders.Ambience;
using LastOrders.Livestock;
using LastOrders.Night;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LastOrders.Jobs
{
    /// <summary>
    /// What a worker does with themselves between jobs, so they are never a statue on their spot.
    ///
    /// Standing about, they shift and fidget - hands on hips a while, arms folded, a scratch of the
    /// head, a stretch first thing in the morning, the back of a wrist across the brow in the heat of
    /// the day - and their eyes go to whatever comes by: a customer walking up (sometimes a wave), a
    /// car pulling in, the hens, a bird landing close, anything moving out past the wall. Now and
    /// then they stroll off a few metres, look about, and wander back. Two with nothing to do meet up
    /// and talk: facing each other, taking turns, the one listening nodding along, a laugh between.
    ///
    /// The moment a job comes up, all of that is dropped: <see cref="WorkerAgent"/> claims it the
    /// same frame and walks off; held gestures let go so the arms swing. At work, the eyes go to what
    /// is being worked on, and the hands play the worker controller's work loop.
    ///
    /// Gestures need the worker controller (see <see cref="WorkerGestures"/>); on any other the
    /// worker still looks about and strolls. Strolls and chats need the level's open-ground map
    /// (<see cref="AmbientBirds"/>), so nobody walks through a wall; without one, only workers already
    /// close together chat.
    /// </summary>
    [DefaultExecutionOrder(1160)] // After WorkerAgent has decided whether there is a job.
    [RequireComponent(typeof(WorkerAgent))]
    [DisallowMultipleComponent]
    public sealed class WorkerLife : MonoBehaviour
    {
        enum Plan { Rest, Stroll, Chat }

        [Header("Idling")]
        [Tooltip("How far from where they wait they wander, metres.")]
        [SerializeField] float strollRadius = 8f;

        [Tooltip("A share of walking speed for a stroll.")]
        [SerializeField, Range(0.2f, 1f)] float strollPace = 0.45f;

        [Tooltip("Seconds standing about before thinking about doing something else.")]
        [SerializeField] Vector2 restSeconds = new Vector2(7f, 15f);

        [Tooltip("Seconds between fidgets while standing.")]
        [SerializeField] Vector2 betweenGestures = new Vector2(2.5f, 6f);

        [Tooltip("How far another idle worker can be to walk over and talk.")]
        [SerializeField] float chatRange = 16f;

        [Header("Noticing")]
        [Tooltip("How far away a person or a thing moving catches their eye.")]
        [SerializeField] float noticeRange = 10f;

        [Tooltip("Chance of a wave to a customer walking by close.")]
        [SerializeField, Range(0f, 1f)] float waveChance = 0.25f;

        [Tooltip("Seconds at least between one wave and the next.")]
        [SerializeField] float waveEvery = 20f;

        [Tooltip("Seconds after a chat before starting another.")]
        [SerializeField] Vector2 betweenChats = new Vector2(35f, 75f);

        static readonly List<WorkerLife> everyone = new List<WorkerLife>();

        // Shared, refreshed by whichever worker gets there first: the things worth a look.
        static readonly List<Transform> people = new List<Transform>();
        static readonly List<Transform> vehicles = new List<Transform>();
        static readonly List<Transform> zombies = new List<Transform>();
        static readonly List<Transform> animals = new List<Transform>();
        static float listedAt = -10f;

        WorkerAgent agent;
        Animator animator;
        CharacterLookAt look;
        Transform head;
        bool canGesture, swings;

        Plan plan;
        float planLeft;
        Vector3 spot;
        WorkerLife partner;
        bool speaking, arrived;
        float turnLeft, chatLeft, strollTimeout;

        Gesture current;
        float gestureLeft, nextGesture;
        Transform pointAt;

        Transform noticed;
        float noticeLeft, nextScan, nextGlance;
        Vector3 glance;
        bool glancing;
        readonly Dictionary<int, float> seen = new Dictionary<int, float>();

        Vector3 lastPosition;
        float stillFor;
        float lastWave = -100f, chatAgain;
        bool wasIdle;
        GroundMap ground;
        AmbientBirds birds;

        bool Standing => stillFor > 0.4f;

        void Awake()
        {
            agent = GetComponent<WorkerAgent>();
            animator = GetComponentInChildren<Animator>();
            swings = GetComponent<WorkerToolSwing>() != null;
            if (animator != null)
            {
                look = animator.GetComponent<CharacterLookAt>();
                head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            }

            canGesture = WorkerGestures.CanGesture(animator);
            lastPosition = transform.position;
        }

        void OnEnable() => everyone.Add(this);

        void OnDisable()
        {
            everyone.Remove(this);
            StopIdling();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            bool moved = (transform.position - lastPosition).sqrMagnitude > 1e-6f;
            lastPosition = transform.position;
            stillFor = moved ? 0f : stillFor + dt;

            if (canGesture) animator.SetInteger(WorkerGestures.WorkStyleId, swings ? WorkerGestures.WorkSwing : WorkerGestures.WorkHands);

            bool idle = agent.isActiveAndEnabled && agent.IsIdle && !agent.IsWorking && !agent.IsSheltering;
            if (!idle)
            {
                if (wasIdle) StopIdling();
                wasIdle = false;
                Busy(dt, moved);
                return;
            }

            if (!wasIdle) StartIdling();
            wasIdle = true;

            if (birds == null) Ground();
            Notice(dt, true);
            switch (plan)
            {
                case Plan.Rest: Rest(dt); break;
                case Plan.Stroll: Stroll(dt); break;
                case Plan.Chat: Chat(dt); break;
            }

            Gestures(dt, moved);
            Gaze(dt);
            TurnToGaze(dt);
        }

        // --- Idle plans ----------------------------------------------------------------------------

        void StartIdling()
        {
            plan = Plan.Rest;
            planLeft = Random.Range(restSeconds.x, restSeconds.y) * 0.5f;
            nextGesture = Random.Range(1f, 3.5f);
            agent.IdleSpot = null;
        }

        void StopIdling()
        {
            EndChat();
            LetGo();
            plan = Plan.Rest;
            if (agent != null) agent.IdleSpot = null;
        }

        void Rest(float dt)
        {
            // Back from a stroll or a chat: home at a stroll, not a dash.
            Vector3 home = agent.HomePoint;
            float away = Flat(home - transform.position).magnitude;
            if (away > agent.ArriveRadius + 0.2f)
            {
                agent.IdleSpot = home;
                agent.IdlePace = strollPace;
                return;
            }

            agent.IdleSpot = null;
            if (!Standing) return;

            planLeft -= dt;
            if (planLeft > 0f || (WorkerGestures.IsHeld(current) && gestureLeft > 1f)) return;

            float roll = Random.value;
            if (roll < 0.45f && Time.time >= chatAgain && TryChat()) return;
            if (roll < 0.8f && TryStroll()) return;
            planLeft = Random.Range(restSeconds.x, restSeconds.y);
        }

        bool TryStroll()
        {
            GroundMap map = Ground();
            if (map == null) return false;

            Vector3 home = agent.HomePoint;
            for (int attempt = 0; attempt < 14; attempt++)
            {
                Vector2 p = Random.insideUnitCircle * strollRadius;
                var candidate = new Vector3(home.x + p.x, home.y, home.z + p.y);
                if (Flat(candidate - transform.position).magnitude < 2.5f) continue;
                if (!map.IsOpen(candidate, 0.5f) || !Clear(transform.position, candidate)) continue;

                LetGo();
                plan = Plan.Stroll;
                spot = candidate;
                arrived = false;
                agent.IdleSpot = spot;
                agent.IdlePace = strollPace;
                planLeft = Random.Range(5f, 12f);
                strollTimeout = 25f;
                return true;
            }

            return false;
        }

        void Stroll(float dt)
        {
            strollTimeout -= dt;
            if (!arrived && Flat(spot - transform.position).magnitude <= agent.ArriveRadius + 0.15f && Standing)
            {
                arrived = true;
                // Look out the way they walked, at whatever drew them there.
                glance = transform.position + transform.forward * Random.Range(4f, 9f) + Vector3.up * Random.Range(0.3f, 1.4f);
                glancing = true;
                nextGlance = Random.Range(1.5f, 3f);
            }

            if (arrived) planLeft -= dt;
            if ((arrived && planLeft <= 0f) || strollTimeout <= 0f)
            {
                plan = Plan.Rest;
                planLeft = Random.Range(restSeconds.x, restSeconds.y);
            }
        }

        bool TryChat()
        {
            WorkerLife best = null;
            float bestDistance = chatRange;
            foreach (WorkerLife other in everyone)
            {
                if (other == this || !other.wasIdle || other.plan != Plan.Rest || other.partner != null || !other.Standing ||
                    Time.time < other.chatAgain) continue;

                float distance = Flat(other.transform.position - transform.position).magnitude;
                if (distance >= bestDistance) continue;
                if (!(Ground() == null ? distance < 5f : Clear(transform.position, other.transform.position))) continue;

                best = other;
                bestDistance = distance;
            }

            if (best == null) return false;

            // Meet in the middle, an arm's length apart, facing. A walk stops its arrive radius short
            // of where it is aimed, so each aims that much past their own place.
            Vector3 middle = (transform.position + best.transform.position) * 0.5f;
            Vector3 across = Flat(best.transform.position - transform.position);
            across = across.sqrMagnitude > 1e-4f ? across.normalized : transform.forward;
            float apart = 0.6f;

            float length = Random.Range(12f, 24f);
            BeginChat(best, middle - across * Mathf.Max(0.05f, apart - agent.ArriveRadius), true, length);
            best.BeginChat(this, middle + across * Mathf.Max(0.05f, apart - best.agent.ArriveRadius), false, length);
            return true;
        }

        void BeginChat(WorkerLife other, Vector3 at, bool talksFirst, float length)
        {
            LetGo();
            plan = Plan.Chat;
            partner = other;
            spot = at;
            arrived = false;
            speaking = talksFirst;
            turnLeft = Random.Range(0.6f, 1.4f);
            chatLeft = length;
            strollTimeout = 25f;
            agent.IdleSpot = at;
            agent.IdlePace = strollPace;
        }

        void Chat(float dt)
        {
            if (partner == null || partner.partner != this || !partner.wasIdle)
            {
                EndChat();
                return;
            }

            strollTimeout -= dt;
            if (!arrived && Flat(spot - transform.position).magnitude <= agent.ArriveRadius + 0.2f && Standing) arrived = true;
            if (!arrived && strollTimeout <= 0f)
            {
                partner.EndChat();
                EndChat();
                return;
            }

            if (!arrived || !partner.arrived) return;

            // Taking turns: the one talking talks with their hands; the other nods along.
            if (speaking)
            {
                turnLeft -= dt;
                if (current == Gesture.None && turnLeft > 0.8f) Play(Random.value < 0.5f ? Gesture.TalkA : Gesture.TalkB);

                if (turnLeft <= 0f)
                {
                    speaking = false;
                    partner.speaking = true;
                    partner.turnLeft = Random.Range(2.4f, 5f);

                    // Something said was funny.
                    if (Random.value < 0.22f)
                    {
                        Play(Gesture.Laugh);
                        partner.Play(Gesture.Laugh);
                    }
                    else if (Random.value < 0.15f)
                    {
                        partner.Play(Gesture.Shrug);
                    }
                }
            }
            else if (current == Gesture.None && Random.value < dt * 0.08f)
            {
                Play(Random.value < 0.5f ? Gesture.ArmsCrossed : Gesture.HandsOnHips, Random.Range(3f, 6f));
            }

            chatLeft -= dt;
            if (chatLeft <= 0f && speaking)
            {
                partner.EndChat();
                EndChat();
            }
        }

        void EndChat()
        {
            if (plan == Plan.Chat)
            {
                plan = Plan.Rest;
                planLeft = Random.Range(restSeconds.x, restSeconds.y);
                chatAgain = Time.time + Random.Range(betweenChats.x, betweenChats.y);
                LetGo();
            }

            partner = null;
            speaking = false;
            arrived = false;
        }

        // --- Gestures -------------------------------------------------------------------------------

        void Gestures(float dt, bool moved)
        {
            if (current != Gesture.None)
            {
                gestureLeft -= dt;

                // On the move, nobody keeps their arms folded.
                if (gestureLeft <= 0f || (moved && WorkerGestures.IsHeld(current))) LetGo();
                return;
            }

            if (!Standing || plan == Plan.Chat || (plan == Plan.Stroll && !arrived)) return;

            nextGesture -= dt;
            if (nextGesture > 0f) return;
            nextGesture = Random.Range(betweenGestures.x, betweenGestures.y);

            Fidget();
        }

        /// <summary>Something to do with their hands, weighted by the time of day and what is about.</summary>
        void Fidget()
        {
            float darkness = AmbientDaylight.Darkness;
            bool morning = darkness > 0.12f && darkness < 0.45f;
            bool day = AmbientDaylight.Day > 0.5f;

            float hips = 16f, fold = 14f, scratch = 9f, check = 9f, brow = day ? 7f : 0f, stretch = morning ? 16f : 4f;
            float shrug = 3f, point = noticed != null ? 9f : 0f, nothing = 22f;
            float total = hips + fold + scratch + check + brow + stretch + shrug + point + nothing;
            float pick = Random.value * total;

            if ((pick -= nothing) < 0f) return;
            if ((pick -= hips) < 0f) { Play(Gesture.HandsOnHips, Random.Range(4f, 9f)); return; }
            if ((pick -= fold) < 0f) { Play(Gesture.ArmsCrossed, Random.Range(5f, 10f)); return; }
            if ((pick -= scratch) < 0f) { Play(Gesture.ScratchHead); return; }
            if ((pick -= check) < 0f) { Play(Gesture.CheckHand); return; }
            if ((pick -= brow) < 0f) { Play(Gesture.WipeBrow); return; }
            if ((pick -= stretch) < 0f) { Play(Gesture.Stretch); return; }
            if ((pick -= shrug) < 0f) { Play(Gesture.Shrug); return; }

            pointAt = noticed;
            Play(Gesture.Point);
        }

        /// <summary>Starts a gesture; a held one is let go after <paramref name="hold"/> seconds.</summary>
        void Play(Gesture gesture, float hold = 0f)
        {
            if (!canGesture) return;

            animator.SetInteger(WorkerGestures.GestureId, (int)gesture);
            animator.SetTrigger(WorkerGestures.ActId);
            current = gesture;
            gestureLeft = WorkerGestures.IsHeld(gesture) ? Mathf.Max(1f, hold) : WorkerGestures.Seconds(gesture) + 0.15f;
        }

        void LetGo()
        {
            if (current == Gesture.None) return;

            current = Gesture.None;
            pointAt = null;
            if (canGesture)
            {
                animator.SetInteger(WorkerGestures.GestureId, 0);
                animator.ResetTrigger(WorkerGestures.ActId);
            }
        }

        // --- Eyes -------------------------------------------------------------------------------------

        /// <summary>Picks out something worth a look: new, moving, close - a zombie before anything.</summary>
        void Notice(float dt, bool idle)
        {
            noticeLeft -= dt;
            if (noticeLeft <= 0f) noticed = null;

            if ((nextScan -= dt) > 0f) return;
            nextScan = 0.4f;

            List();
            Vector3 me = transform.position;
            Transform best = null;
            float bestScore = 0f;

            Consider(zombies, 2.2f, 1.8f);
            Consider(people, 1f, 1f);
            Consider(vehicles, 0.9f, 1.4f);
            Consider(animals, 0.5f, 0.8f);

            // The wild birds, when one comes down close.
            Transform cage = birds != null ? birds.transform.Find("Birds") : null;
            if (cage != null)
                foreach (Transform bird in cage)
                    if (bird.gameObject.activeSelf && Flat(bird.position - me).sqrMagnitude < 36f) Score(bird, 0.6f);

            if (best == null || best == noticed) return;
            if (noticed != null && noticeLeft > 0.5f && bestScore < 1.5f) return;

            noticed = best;
            noticeLeft = Random.Range(1.6f, 3.6f);
            seen[best.GetInstanceID()] = Time.time;

            // A customer walking by close, and nothing else on: a wave.
            if (idle && current == Gesture.None && Standing && plan != Plan.Chat && people.Contains(best) &&
                Flat(best.position - me).magnitude < 7f && Time.time - lastWave > waveEvery && Random.value < waveChance &&
                best.GetComponent<WorkerAgent>() == null)
            {
                lastWave = Time.time;
                Play(Gesture.Wave);
            }

            void Consider(List<Transform> things, float weight, float reach)
            {
                foreach (Transform t in things) Score(t, weight, reach);
            }

            void Score(Transform t, float weight, float reach = 1f)
            {
                if (t == null || t == transform || t.IsChildOf(transform)) return;

                float distance = Flat(t.position - me).magnitude;
                float range = noticeRange * reach;
                if (distance > range) return;

                // New things catch the eye; something looked at a moment ago, less so.
                float freshness = seen.TryGetValue(t.GetInstanceID(), out float when) ? Mathf.Clamp01((Time.time - when - 8f) / 20f) : 1f;
                float score = weight * (1.2f - distance / range) * (0.25f + freshness);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = t;
                }
            }
        }

        void Gaze(float dt)
        {
            if (look == null) return;

            if (plan == Plan.Chat && partner != null && arrived)
            {
                // The listener nods now and then; the speaker looks at who they are talking to.
                float nod = speaking ? 0f : Mathf.Max(0f, Mathf.Sin(Time.time * 1.3f + GetInstanceID())) * Mathf.Sin(Time.time * 11f) * 0.07f;
                look.Watch(partner.head != null ? partner.head : partner.transform, Vector3.up * (partner.head != null ? nod : 1.5f + nod));
                return;
            }

            if (current == Gesture.Point && pointAt != null)
            {
                look.Watch(pointAt, Vector3.up * 0.8f);
                return;
            }

            if (current == Gesture.CheckHand && animator != null)
            {
                Transform hand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                if (hand != null)
                {
                    look.Watch(hand, Vector3.zero, 0.8f);
                    return;
                }
            }

            if (current == Gesture.Stretch || current == Gesture.ScratchHead || current == Gesture.Laugh)
            {
                look.Watch(transform.position + transform.forward * 4f + Vector3.up * (current == Gesture.Stretch ? 4f : 2.2f), 0.6f);
                return;
            }

            if (noticed != null)
            {
                look.Watch(noticed, Vector3.up * (people.Contains(noticed) || zombies.Contains(noticed) ? 1.5f : 0.6f));
                return;
            }

            // Nothing in particular: the eyes wander - out across the yard, down at the ground, up at the sky.
            nextGlance -= dt;
            if (nextGlance <= 0f)
            {
                nextGlance = Random.Range(1.8f, 4.5f);
                glancing = Random.value < 0.75f;
                if (glancing)
                {
                    float yaw = Random.Range(-110f, 110f);
                    Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Flat(transform.forward).normalized;
                    float lift = Random.value < 0.15f ? Random.Range(3f, 7f) : Random.Range(-0.6f, 1.6f);
                    glance = transform.position + dir * Random.Range(3f, 12f) + Vector3.up * lift;
                }
            }

            if (glancing) look.Watch(glance, 0.75f);
            else look.Release();
        }

        /// <summary>Standing still, they turn to face what they look at once it is well off to one side.</summary>
        void TurnToGaze(float dt)
        {
            if (!Standing) return;

            Vector3 target;
            float tolerance;
            if (plan == Plan.Chat && partner != null && arrived)
            {
                target = partner.transform.position;
                tolerance = 12f;
            }
            else if (noticed != null)
            {
                target = noticed.position;
                tolerance = 75f;
            }
            else return;

            Vector3 to = Flat(target - transform.position);
            if (to.sqrMagnitude < 0.04f) return;

            float off = Vector3.Angle(Flat(transform.forward), to);
            if (off < tolerance) return;

            Quaternion want = Quaternion.LookRotation(to);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, 140f * dt);
        }

        // --- At work and on the move -----------------------------------------------------------------

        void Busy(float dt, bool moved)
        {
            LetGo();
            if (look == null) return;

            if (agent.IsSheltering)
            {
                look.Release();
                return;
            }

            // At a job: eyes on the work.
            Job job = agent.Current;
            if (agent.IsWorking && job != null && job.Target != null)
            {
                look.Watch(job.Target, Vector3.up * 0.5f, 0.7f);
                return;
            }

            // Walking: eyes ahead, with a glance at anything passing close.
            Notice(dt, false);
            if (noticed != null && Vector3.Angle(transform.forward, Flat(noticed.position - transform.position)) < 100f)
                look.Watch(noticed, Vector3.up * 1.2f, 0.7f);
            else
                look.Release();
        }

        // --- Helpers ----------------------------------------------------------------------------------

        GroundMap Ground()
        {
            if (birds == null) birds = FindFirstObjectByType<AmbientBirds>();
            if (birds != null) ground = birds.Ground;
            return ground;
        }

        /// <summary>A straight walk from <paramref name="from"/> to <paramref name="to"/> crosses nothing standing.</summary>
        bool Clear(Vector3 from, Vector3 to)
        {
            GroundMap map = Ground();
            if (map == null) return false;

            Vector3 run = Flat(to - from);
            float length = run.magnitude;
            for (float d = 1.2f; d < length - 1.2f; d += 0.5f)
                if (!map.IsOpen(from + run * (d / length), 0.3f)) return false;

            return true;
        }

        /// <summary>Who and what is about: one list for every worker, refreshed every second or so.</summary>
        static void List()
        {
            if (Time.time - listedAt < 1.2f) return;
            listedAt = Time.time;

            people.Clear();
            vehicles.Clear();
            zombies.Clear();
            animals.Clear();

            foreach (Visitor visitor in FindObjectsByType<Visitor>(FindObjectsSortMode.None)) people.Add(visitor.transform);
            foreach (WorkerAgent worker in FindObjectsByType<WorkerAgent>(FindObjectsSortMode.None))
                if (!worker.IsSheltering) people.Add(worker.transform);
            foreach (VisitorVehicle vehicle in FindObjectsByType<VisitorVehicle>(FindObjectsSortMode.None)) vehicles.Add(vehicle.transform);
            foreach (EnemyAgent enemy in FindObjectsByType<EnemyAgent>(FindObjectsSortMode.None))
                if (enemy.IsAlive) zombies.Add(enemy.transform);
            foreach (ChickenBody hen in FindObjectsByType<ChickenBody>(FindObjectsSortMode.None)) animals.Add(hen.transform);
            foreach (CowBody cow in FindObjectsByType<CowBody>(FindObjectsSortMode.None)) animals.Add(cow.transform);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
