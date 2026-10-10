using System;
using System.Collections.Generic;
using FoodStreet.Visitors;
using LastOrders.Jobs;
using LastOrders.Night;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LastOrders.Ambience
{
    /// <summary>
    /// The level's wild birds, kept going round wherever the player is looking.
    ///
    /// By day: small flocks of pigeons and sparrows come in to land on open ground, peck about, and
    /// burst up when anyone - a worker, a customer, a car, a zombie, a tap of the player's finger -
    /// comes too close, then settle again somewhere quieter or leave. Crows sit along the compound
    /// wall and the suburb rooftops, watching, cawing now and then, and go when a zombie comes near or
    /// their bit of wall is knocked down. Every so often a flock crosses high over the whole level,
    /// its shadows sliding over the ground below.
    ///
    /// As dusk falls they all fly off north to roost in the forest; at night bats flicker about over
    /// the compound instead; at dawn the birds come back.
    ///
    /// Birds are pooled: one that has flown out of sight is put away and used again.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbientBirds : MonoBehaviour
    {
        /// <summary>Somewhere a bird can sit above the ground: a wall top, a roof ridge. Pinned to the
        /// mesh it sits on, so it moves and vanishes with it - a wall knocked down takes its crows' perch.</summary>
        [Serializable]
        public struct Perch
        {
            public Renderer surface;
            public Vector3 local;
            public float yaw;

            public bool Usable => surface != null && surface.enabled && surface.gameObject.activeInHierarchy;
            public Vector3 Point => surface.transform.TransformPoint(local);
        }

        enum Mode { Arriving, Grounded, Perched, Leaving, Flyover, Bats }

        sealed class Flock
        {
            public readonly List<AmbientBird> Birds = new List<AmbientBird>();
            public Mode Mode;
            public Vector3 Spot;
            public float Radius;
            public float Until;
            public int Perch = -1;

            // Flyovers: a moving point the birds keep station on.
            public Vector3 Anchor, Direction;
            public float Speed, Travelled, Length;
            public readonly List<Vector3> Slots = new List<Vector3>();
        }

        [Header("Birds")]
        [SerializeField] AmbientBird[] pigeons;
        [SerializeField] AmbientBird[] sparrows;
        [SerializeField] AmbientBird[] crows;
        [SerializeField] AmbientBird[] bats;

        [Header("Where")]
        [Tooltip("The ground birds may land on, x/z.")]
        [SerializeField] Rect area = new Rect(-44f, -40f, 128f, 116f);

        [Tooltip("Roots whose ground is out of bounds whole, floor and all: indoors.")]
        [SerializeField] string[] indoors = { "Restaurant" };

        [Tooltip("Wall tops and roof ridges, found by the setup tool.")]
        [SerializeField] Perch[] perches = Array.Empty<Perch>();

        [Tooltip("Where they fly off to at dusk: the forest.")]
        [SerializeField] Vector3 roost = new Vector3(18f, 20f, 115f);

        [Header("How many")]
        [SerializeField, Min(0)] int groundFlocks = 3;
        [SerializeField] Vector2Int pigeonFlock = new Vector2Int(4, 8);
        [SerializeField] Vector2Int sparrowFlock = new Vector2Int(3, 6);
        [SerializeField, Range(0f, 1f)] float sparrowShare = 0.4f;
        [SerializeField, Min(0)] int perchedCrows = 4;

        [Tooltip("Seconds between flocks crossing overhead.")]
        [SerializeField] Vector2 flyoverEvery = new Vector2(18f, 40f);
        [SerializeField] Vector2Int flyoverSize = new Vector2Int(4, 9);
        [SerializeField, Min(0)] int batCount = 5;

        [Header("Nerves")]
        [Tooltip("A tap this many pixels (at 1080p) from a settled bird puts its flock up.")]
        [SerializeField] float tapPixels = 70f;

        [Tooltip("Metres: a zombie puts a flock up from further than a person does.")]
        [SerializeField] float zombieFright = 7f;

        [SerializeField] float vehicleFright = 6f;

        readonly List<Flock> flocks = new List<Flock>();
        readonly Dictionary<AmbientBird.Kind, Stack<AmbientBird>> pool = new Dictionary<AmbientBird.Kind, Stack<AmbientBird>>();
        readonly List<Transform> people = new List<Transform>();
        readonly List<Transform> zombies = new List<Transform>();
        readonly List<Transform> vehicles = new List<Transform>();
        readonly HashSet<int> perchesTaken = new HashSet<int>();

        GroundMap ground;
        Transform cage;

        /// <summary>Which ground is open, as the birds see it: shared with the butterflies.</summary>
        public GroundMap Ground => ground;
        float nextLook, nextMap, nextCheck, nextFlyover, nextArrival;
        bool started;

        void Start()
        {
            cage = new GameObject("Birds").transform;
            cage.SetParent(transform, false);

            ground = new GroundMap(area, 1f);
            ground.Rebuild(transform.root, indoors);
            nextMap = Random.Range(45f, 60f);
            LookAround();

            nextFlyover = Random.Range(4f, 10f);
            nextArrival = 2f;

            // A level that opens by day opens with birds already about.
            if (AmbientDaylight.Day > 0.5f)
            {
                for (int i = 0; i < groundFlocks - 1; i++) SpawnGroundFlock(true);
                for (int i = 0; i < perchedCrows; i++) SpawnPerchedCrow(true);
            }

            started = true;
        }

        void OnDisable()
        {
            if (!started) return;

            foreach (Flock flock in flocks)
                foreach (AmbientBird bird in flock.Birds)
                    if (bird != null) Store(bird);
            flocks.Clear();
            perchesTaken.Clear();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if ((nextLook -= dt) <= 0f) LookAround();
            if ((nextMap -= dt) <= 0f)
            {
                // A few milliseconds over every mesh in the level: rare, as buildings go up rarely.
                ground.Rebuild(transform.root, indoors);
                nextMap = Random.Range(45f, 60f);
            }

            bool day = AmbientDaylight.Day > 0.5f;
            bool night = AmbientDaylight.Night > 0.5f;

            bool check = (nextCheck -= dt) <= 0f;
            if (check) nextCheck = 0.2f;

            Tap();

            for (int i = flocks.Count - 1; i >= 0; i--)
            {
                Flock flock = flocks[i];
                Tick(flock, dt, check, day, night);
                if (flock.Birds.Count == 0)
                {
                    if (flock.Perch >= 0) perchesTaken.Remove(flock.Perch);
                    flocks.RemoveAt(i);
                }
            }

            if (day) KeepDayBirds(dt);
            if (night) KeepBats();
        }

        // --- Keeping numbers up ------------------------------------------------------------------------------

        void KeepDayBirds(float dt)
        {
            if ((nextArrival -= dt) <= 0f)
            {
                nextArrival = Random.Range(3f, 8f);
                if (Count(Mode.Grounded) + Count(Mode.Arriving) < groundFlocks) SpawnGroundFlock(false);
                else if (CountPerched() < perchedCrows) SpawnPerchedCrow(false);
            }

            if ((nextFlyover -= dt) <= 0f)
            {
                nextFlyover = Random.Range(flyoverEvery.x, flyoverEvery.y);
                SpawnFlyover();
            }
        }

        void KeepBats()
        {
            if (batCount <= 0 || bats == null || bats.Length == 0 || Count(Mode.Bats) > 0) return;

            var flock = new Flock { Mode = Mode.Bats, Spot = AmbientView.Focus + Flat(Random.insideUnitSphere) * 6f, Radius = 9f };
            for (int i = 0; i < batCount; i++)
            {
                AmbientBird bat = Take(bats);
                Vector3 from = flock.Spot + Flat(Random.onUnitSphere).normalized * (AmbientView.Reach + 15f) + Vector3.up * Random.Range(8f, 14f);
                bat.Launch(from, (flock.Spot - from).normalized * bat.CruiseSpeed);
                bat.FlyTo(BatGoal(flock));
                flock.Birds.Add(bat);
            }

            flocks.Add(flock);
        }

        void SpawnGroundFlock(bool alreadyThere)
        {
            bool sparrow = sparrows != null && sparrows.Length > 0 && Random.value < sparrowShare;
            AmbientBird[] kind = sparrow ? sparrows : pigeons;
            if (kind == null || kind.Length == 0) return;

            Vector2Int size = sparrow ? sparrowFlock : pigeonFlock;
            int count = Random.Range(size.x, size.y + 1);
            float radius = Mathf.Lerp(0.9f, 1.8f, Mathf.InverseLerp(3f, 8f, count));

            if (!FindGround(AmbientView.Focus, AmbientView.Reach * 0.85f, radius, kind[0].Fright + 3f, out Vector3 spot)) return;

            var flock = new Flock { Mode = alreadyThere ? Mode.Grounded : Mode.Arriving, Spot = spot, Radius = radius, Until = Time.time + Random.Range(35f, 90f) };

            // In from downwind, into the wind, as birds land.
            Vector3 across = new Vector3(-AmbientWind.Heading.z, 0f, AmbientWind.Heading.x);
            Vector3 entry = spot + AmbientWind.Heading * (AmbientView.Reach + 18f) + across * Random.Range(-14f, 14f) + Vector3.up * Random.Range(9f, 15f);

            for (int i = 0; i < count; i++)
            {
                AmbientBird bird = Take(kind);
                Vector3 seat = Seat(spot, radius);
                if (alreadyThere)
                {
                    bird.Settle(seat, Random.Range(0f, 360f), false, radius * 0.6f);
                }
                else
                {
                    Vector3 from = entry + Random.insideUnitSphere * 2.5f;
                    bird.Launch(from, (seat - from).normalized * bird.CruiseSpeed);
                    bird.LandAt(seat, 0f, false, radius * 0.6f);
                }

                flock.Birds.Add(bird);
            }

            flocks.Add(flock);
        }

        void SpawnPerchedCrow(bool alreadyThere)
        {
            if (crows == null || crows.Length == 0 || perches == null || perches.Length == 0) return;

            int index = PickPerch();
            if (index < 0) return;

            Perch perch = perches[index];
            AmbientBird crow = Take(crows);
            var flock = new Flock { Mode = alreadyThere ? Mode.Perched : Mode.Arriving, Spot = perch.Point, Perch = index, Until = Time.time + Random.Range(40f, 120f) };
            perchesTaken.Add(index);

            if (alreadyThere)
            {
                crow.Settle(perch.Point, perch.yaw, true, 0f);
            }
            else
            {
                Vector3 from = perch.Point + Flat(Random.onUnitSphere).normalized * (AmbientView.Reach + 15f) + Vector3.up * Random.Range(8f, 14f);
                crow.Launch(from, (perch.Point - from).normalized * crow.CruiseSpeed);
                crow.LandAt(perch.Point, perch.yaw, true, 0f);
            }

            flock.Birds.Add(crow);
            flocks.Add(flock);
        }

        void SpawnFlyover()
        {
            bool crowFlock = crows != null && crows.Length > 0 && Random.value < 0.3f;
            AmbientBird[] kind = crowFlock ? crows : pigeons;
            if (kind == null || kind.Length == 0) return;

            Vector3 focus = AmbientView.Focus;
            float reach = AmbientView.Reach;
            Vector3 dir = Quaternion.Euler(0f, Random.Range(-80f, 80f), 0f) * AmbientWind.Heading;
            if (Random.value < 0.35f) dir = -dir;
            Vector2 offset = Random.insideUnitCircle * reach * 0.45f;
            Vector3 through = focus + new Vector3(offset.x, 0f, offset.y);
            float height = Random.Range(10f, 18f);

            var flock = new Flock
            {
                Mode = Mode.Flyover,
                Direction = dir,
                Speed = kind[0].CruiseSpeed * Random.Range(0.85f, 1f),
                Length = (reach + 35f) * 2f,
            };
            flock.Anchor = through - dir * (reach + 35f) + Vector3.up * height;

            int count = Random.Range(flyoverSize.x, flyoverSize.y + 1);
            Quaternion turn = Quaternion.LookRotation(dir);
            for (int i = 0; i < count; i++)
            {
                // A loose skein: each a little behind and to one side of the last.
                float back = i * Random.Range(0.9f, 1.6f);
                float side = (i % 2 == 0 ? 1f : -1f) * Mathf.Ceil(i * 0.5f) * Random.Range(0.7f, 1.3f);
                var slot = new Vector3(side, Random.Range(-0.8f, 0.8f), -back);
                flock.Slots.Add(slot);

                AmbientBird bird = Take(kind);
                bird.Launch(flock.Anchor + turn * slot, dir * flock.Speed);
                flock.Birds.Add(bird);
            }

            flocks.Add(flock);
        }

        // --- Each flock ------------------------------------------------------------------------------------

        void Tick(Flock flock, float dt, bool check, bool day, bool night)
        {
            switch (flock.Mode)
            {
                case Mode.Arriving:
                    if (!day)
                    {
                        Leave(flock, true);
                        break;
                    }

                    bool down = true;
                    foreach (AmbientBird bird in flock.Birds) down &= !bird.Airborne;
                    if (down) flock.Mode = flock.Perch >= 0 ? Mode.Perched : Mode.Grounded;
                    else if (check && flock.Perch < 0 && flock.Birds.Count > 0 && !Calm(flock.Spot, flock.Radius + 1.5f, flock.Birds[0].Fright)) Move(flock, null);
                    else if (flock.Perch >= 0 && !perches[flock.Perch].Usable) Leave(flock, false);
                    break;

                case Mode.Grounded:
                    if (!day) Leave(flock, true);
                    else if (check && Threat(flock, out Vector3 from)) Scare(flock, from);
                    else if (check && !ground.IsOpen(flock.Spot, flock.Radius * 0.6f)) Move(flock, null);
                    else if (Time.time > flock.Until)
                    {
                        if (Random.value < 0.6f) Move(flock, null);
                        else Leave(flock, false);
                    }

                    break;

                case Mode.Perched:
                    if (!day) Leave(flock, true);
                    else if (!perches[flock.Perch].Usable) Scare(flock, flock.Spot - Vector3.up);
                    else if (check && Threat(flock, out Vector3 threat)) Scare(flock, threat);
                    else if (Time.time > flock.Until) Leave(flock, false);
                    break;

                case Mode.Leaving:
                    for (int i = flock.Birds.Count - 1; i >= 0; i--)
                    {
                        AmbientBird bird = flock.Birds[i];
                        if (bird.Airborne && Gone(bird.transform.position))
                        {
                            Store(bird);
                            flock.Birds.RemoveAt(i);
                        }
                    }

                    break;

                case Mode.Flyover:
                    flock.Anchor += flock.Direction * (flock.Speed * dt);
                    flock.Travelled += flock.Speed * dt;
                    Quaternion turn = Quaternion.LookRotation(flock.Direction);
                    for (int i = flock.Birds.Count - 1; i >= 0; i--)
                    {
                        AmbientBird bird = flock.Birds[i];
                        float wobble = Mathf.Sin(Time.time * 0.9f + i * 1.7f) * 0.6f;
                        bird.FlyTo(flock.Anchor + turn * (flock.Slots[i] + new Vector3(wobble, wobble * 0.4f, 0f)), 1.2f);
                        if (flock.Travelled > flock.Length * 0.6f && Gone(bird.transform.position))
                        {
                            Store(bird);
                            flock.Birds.RemoveAt(i);
                            flock.Slots.RemoveAt(i);
                        }
                    }

                    if (flock.Travelled > flock.Length * 1.5f)
                    {
                        foreach (AmbientBird bird in flock.Birds) Store(bird);
                        flock.Birds.Clear();
                    }

                    break;

                case Mode.Bats:
                    if (!night)
                    {
                        Leave(flock, true);
                        break;
                    }

                    foreach (AmbientBird bat in flock.Birds)
                        if (bat.HasArrived(1.8f) || Random.value < dt * 0.7f) bat.FlyTo(BatGoal(flock));

                    // The swarm drifts after the view, so they stay where the player is looking.
                    flock.Spot = Vector3.MoveTowards(flock.Spot, AmbientView.Focus, dt * 1.5f);
                    break;
            }
        }

        Vector3 BatGoal(Flock flock)
        {
            Vector2 p = Random.insideUnitCircle * flock.Radius;
            return flock.Spot + new Vector3(p.x, Random.Range(2.5f, 7f), p.y);
        }

        /// <summary>Up and away from <paramref name="from"/>, the nearest first; then down again somewhere
        /// quieter, or gone.</summary>
        void Scare(Flock flock, Vector3 from)
        {
            foreach (AmbientBird bird in flock.Birds)
            {
                Vector3 away = Flat(bird.transform.position - from);
                float distance = away.magnitude;
                bird.TakeOff(away, Mathf.Clamp01(distance / 8f) * 0.25f + Random.Range(0f, 0.15f));
            }

            bool settleAgain = AmbientDaylight.Day > 0.5f && flock.Perch < 0 && Random.value < 0.7f;
            if (!settleAgain || !Move(flock, from)) Leave(flock, false);
        }

        /// <summary>The flock flies to new ground nearby. False if there is none to be had.</summary>
        bool Move(Flock flock, Vector3? from)
        {
            Vector3 near = flock.Spot;
            if (from.HasValue)
            {
                // Away from what frightened them, and on toward the middle of the view.
                Vector3 away = Flat(flock.Spot - from.Value);
                near = flock.Spot + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * 16f;
                near = Vector3.Lerp(near, AmbientView.Focus, 0.3f);
            }

            float fright = flock.Birds.Count > 0 ? flock.Birds[0].Fright : 4f;
            if (!FindGround(near, 18f, flock.Radius, fright + 3f, out Vector3 spot, flock.Spot, 9f)) return false;

            flock.Spot = spot;
            flock.Mode = Mode.Arriving;
            flock.Until = Time.time + Random.Range(35f, 90f);
            foreach (AmbientBird bird in flock.Birds)
            {
                if (!bird.Airborne) bird.TakeOff(Flat(spot - bird.transform.position), Random.Range(0f, 1.2f));
                bird.LandAt(Seat(spot, flock.Radius), 0f, false, flock.Radius * 0.6f);
            }

            return true;
        }

        void Leave(Flock flock, bool toRoost)
        {
            if (flock.Perch >= 0)
            {
                perchesTaken.Remove(flock.Perch);
                flock.Perch = -1;
            }

            flock.Mode = Mode.Leaving;
            Vector3 exit = toRoost
                ? roost
                : AmbientView.Focus + Flat(Random.onUnitSphere).normalized * (AmbientView.Reach + 60f) + Vector3.up * Random.Range(12f, 20f);

            foreach (AmbientBird bird in flock.Birds)
            {
                if (!bird.Airborne) bird.TakeOff(Flat(exit - bird.transform.position), Random.Range(0f, toRoost ? 3f : 0.6f));
                bird.FlyTo(exit + Random.insideUnitSphere * 6f);
            }
        }

        // --- Fright -----------------------------------------------------------------------------------------

        void LookAround()
        {
            nextLook = 1.5f;
            people.Clear();
            zombies.Clear();
            vehicles.Clear();

            foreach (WorkerAgent worker in FindObjectsByType<WorkerAgent>(FindObjectsSortMode.None))
                if (!worker.IsSheltering) people.Add(worker.transform);
            foreach (Visitor visitor in FindObjectsByType<Visitor>(FindObjectsSortMode.None)) people.Add(visitor.transform);
            foreach (EnemyAgent enemy in FindObjectsByType<EnemyAgent>(FindObjectsSortMode.None))
                if (enemy.IsAlive) zombies.Add(enemy.transform);
            foreach (VisitorVehicle vehicle in FindObjectsByType<VisitorVehicle>(FindObjectsSortMode.None)) vehicles.Add(vehicle.transform);
        }

        /// <summary>Someone too close to any bird of the flock: where they are.</summary>
        bool Threat(Flock flock, out Vector3 from)
        {
            bool perched = flock.Mode == Mode.Perched;
            foreach (AmbientBird bird in flock.Birds)
            {
                Vector3 at = bird.transform.position;
                float person = perched ? bird.Fright * 0.6f : bird.Fright;
                if (Near(people, at, person, out from)) return true;
                if (Near(zombies, at, Mathf.Max(zombieFright, bird.Fright), out from)) return true;
                if (!perched && Near(vehicles, at, vehicleFright, out from)) return true;
            }

            from = default;
            return false;
        }

        static bool Near(List<Transform> who, Vector3 at, float radius, out Vector3 from)
        {
            float limit = radius * radius;
            foreach (Transform t in who)
            {
                if (t == null) continue;
                Vector3 p = t.position;
                if ((p - at).sqrMagnitude < limit)
                {
                    from = p;
                    return true;
                }
            }

            from = default;
            return false;
        }

        /// <summary>True if nobody is within their fright of <paramref name="spot"/>, plus a margin.</summary>
        bool Calm(Vector3 spot, float margin, float fright)
        {
            return !Near(people, spot, fright + margin, out _) && !Near(zombies, spot, zombieFright + margin, out _) &&
                   !Near(vehicles, spot, vehicleFright + margin, out _);
        }

        /// <summary>A tap near a settled bird puts its flock up: the one thing the player can do to them.</summary>
        void Tap()
        {
            if (!Input.GetMouseButtonDown(0)) return;

            Camera view = AmbientView.Camera;
            if (view == null) return;

            Vector2 tap = Input.mousePosition;
            float limit = tapPixels * Screen.height / 1080f;
            AmbientView.GroundUnder(tap, out Vector3 ground);

            foreach (Flock flock in flocks)
            {
                if (flock.Mode != Mode.Grounded && flock.Mode != Mode.Perched) continue;

                foreach (AmbientBird bird in flock.Birds)
                {
                    Vector3 screen = view.WorldToScreenPoint(bird.transform.position);
                    if (screen.z <= 0f || ((Vector2)screen - tap).sqrMagnitude > limit * limit) continue;

                    Scare(flock, flock.Mode == Mode.Perched ? bird.transform.position - view.transform.forward * 3f : ground);
                    break;
                }
            }
        }

        // --- Places -----------------------------------------------------------------------------------------

        bool FindGround(Vector3 near, float range, float radius, float calm, out Vector3 spot, Vector3? not = null, float notWithin = 0f)
        {
            for (int attempt = 0; attempt < 30; attempt++)
            {
                // Mostly where the player is looking; sometimes anywhere, so the edges have birds too.
                Vector2 p = Random.insideUnitCircle * (attempt < 20 ? range : range * 2f);
                var candidate = new Vector3(near.x + p.x, 0f, near.z + p.y);

                if (!ground.IsOpen(candidate, radius + 0.4f)) continue;
                if (!Calm(candidate, 2f, calm)) continue;
                if (not.HasValue && Flat(candidate - not.Value).sqrMagnitude < notWithin * notWithin) continue;
                if (Crowded(candidate, 7f)) continue;

                spot = candidate;
                return true;
            }

            spot = default;
            return false;
        }

        bool Crowded(Vector3 spot, float within)
        {
            foreach (Flock flock in flocks)
                if ((flock.Mode == Mode.Grounded || flock.Mode == Mode.Arriving) && flock.Perch < 0 &&
                    Flat(flock.Spot - spot).sqrMagnitude < within * within) return true;
            return false;
        }

        int PickPerch()
        {
            Vector3 focus = AmbientView.Focus;
            float reach = AmbientView.Reach * 1.2f;
            int fallback = -1;

            for (int attempt = 0; attempt < 24; attempt++)
            {
                int index = Random.Range(0, perches.Length);
                if (perchesTaken.Contains(index) || !perches[index].Usable) continue;

                Vector3 point = perches[index].Point;
                if (!Calm(point, 0f, 3f)) continue;
                if (Flat(point - focus).sqrMagnitude < reach * reach) return index;
                fallback = index;
            }

            return fallback;
        }

        static Vector3 Seat(Vector3 spot, float radius)
        {
            Vector2 p = Random.insideUnitCircle * radius;
            return spot + new Vector3(p.x, 0f, p.y);
        }

        /// <summary>Out of sight and well away: safe to put away.</summary>
        static bool Gone(Vector3 position)
        {
            if (AmbientView.Sees(position, 0.15f)) return false;
            return Flat(position - AmbientView.Focus).magnitude > AmbientView.Reach + 12f || position.y > 40f;
        }

        int Count(Mode mode)
        {
            int n = 0;
            foreach (Flock flock in flocks)
                if (flock.Mode == mode && flock.Perch < 0) n++;
            return n;
        }

        int CountPerched()
        {
            int n = 0;
            foreach (Flock flock in flocks)
                if (flock.Perch >= 0) n++;
            return n;
        }

        // --- The pool ---------------------------------------------------------------------------------------

        AmbientBird Take(AmbientBird[] kind)
        {
            AmbientBird prefab = kind[Random.Range(0, kind.Length)];
            if (pool.TryGetValue(prefab.Species, out Stack<AmbientBird> stack))
            {
                // Pooled birds of a kind are interchangeable apart from plumage; a different one is fine.
                while (stack.Count > 0)
                {
                    AmbientBird spare = stack.Pop();
                    if (spare != null) return spare;
                }
            }

            AmbientBird bird = Instantiate(prefab, cage);
            bird.gameObject.SetActive(false);
            return bird;
        }

        void Store(AmbientBird bird)
        {
            bird.gameObject.SetActive(false);
            if (!pool.TryGetValue(bird.Species, out Stack<AmbientBird> stack))
            {
                stack = new Stack<AmbientBird>();
                pool[bird.Species] = stack;
            }

            stack.Push(bird);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(AmbientBird[] pigeonKinds, AmbientBird[] sparrowKinds, AmbientBird[] crowKinds, AmbientBird[] batKinds,
                              Rect landingArea, Perch[] found, Vector3 roostPoint)
        {
            pigeons = pigeonKinds;
            sparrows = sparrowKinds;
            crows = crowKinds;
            bats = batKinds;
            area = landingArea;
            perches = found;
            roost = roostPoint;
        }
    }
}
