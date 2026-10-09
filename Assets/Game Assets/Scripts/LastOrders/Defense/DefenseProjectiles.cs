using System.Collections.Generic;
using LastOrders.Data;
using LastOrders.Effects;
using UnityEngine;

namespace LastOrders.Defense
{
    /// <summary>
    /// Draws a thrown shot on its way: a slice of flaming toast lobbed in an arc, tumbling and
    /// trailing fire and smoke, from the thrower to whatever it was thrown at, bursting where it
    /// lands.
    ///
    /// An add-on to a <see cref="DefensePost"/>, like <see cref="DefenseRecoil"/>. The rules
    /// decide when a thrown shot lands and what it does (<see cref="DefenseUnit"/>); this only
    /// flies something there in the same time, following the target as it walks so the shot
    /// comes down on it. The arc and the tumble come from the post's
    /// <see cref="DefenseMotionProfile"/>. A hitscan shot is ignored.
    ///
    /// What flies is one hidden object under it, copied into a little pool of its own the first
    /// few times it is needed. Any particle systems on it are its trail: they emit while it flies,
    /// and when it lands it vanishes but the trail is left to die away where it was before the copy
    /// goes back in the pool. The burst where it lands is a pooled effect, played through the
    /// game's effects pool so landings that overlap each get their own.
    ///
    /// A thrower can have more than one thing to throw - a crop cannon's tomato, then a cob, then a
    /// cabbage - each with its own burst where it lands. They are thrown in turn.
    /// </summary>
    public sealed class DefenseProjectiles : MonoBehaviour
    {
        [SerializeField] DefensePost post;

        [Tooltip("What flies. Kept hidden; copies of it are made as they are needed. Particle " +
                 "systems on it are its trail.")]
        [SerializeField] GameObject projectile;

        [Tooltip("Optional. Played where a shot comes down.")]
        [SerializeField] PooledEffect impact;

        [Tooltip("Optional. More things it throws, in turn with the first. Kept hidden, like it.")]
        [SerializeField] GameObject[] moreProjectiles;

        [Tooltip("Optional. Where each of those comes down, by the same index. A missing one uses the impact above.")]
        [SerializeField] PooledEffect[] moreImpacts;

        /// <summary>One copy of the projectile, and the parts of it that are switched as it flies and lands.</summary>
        sealed class Shot
        {
            /// <summary>Which of the things it throws this is: 0 the first, then the extras in order.</summary>
            public int Kind;
            public Transform Body;
            public Renderer[] Renderers;
            public ParticleSystem[] Trail;
        }

        sealed class Flight
        {
            public Shot Shot;
            public IDefenseTarget Target;
            public Vector3 From;
            public Vector3 To;
            public float Seconds;
            public float Elapsed;
        }

        readonly List<Flight> flying = new List<Flight>();
        readonly List<Shot> cooling = new List<Shot>();
        readonly Dictionary<int, Stack<Shot>> spare = new Dictionary<int, Stack<Shot>>();

        int next;

        void Awake()
        {
            if (post == null) post = GetComponentInParent<DefensePost>();
            if (projectile != null) projectile.SetActive(false);

            if (moreProjectiles == null) return;
            foreach (GameObject extra in moreProjectiles)
                if (extra != null) extra.SetActive(false);
        }

        int Kinds => 1 + (moreProjectiles != null ? moreProjectiles.Length : 0);

        GameObject Template(int kind) => kind == 0 ? projectile : moreProjectiles[kind - 1];

        PooledEffect ImpactFor(int kind) =>
            kind > 0 && moreImpacts != null && kind - 1 < moreImpacts.Length && moreImpacts[kind - 1] != null ? moreImpacts[kind - 1] : impact;

        void OnEnable()
        {
            if (post != null) post.Fired += HandleFired;
        }

        void OnDisable()
        {
            if (post != null) post.Fired -= HandleFired;

            // Anything in the air or still trailing smoke when the post goes is put straight away.
            foreach (Flight f in flying) Store(f.Shot);
            foreach (Shot s in cooling) Store(s);
            flying.Clear();
            cooling.Clear();
        }

        void HandleFired(Vector3 from, Vector3 to, DefenseShot shot)
        {
            if (!shot.InFlight || projectile == null) return;

            // In turn, skipping any extra left empty.
            int kind = next;
            for (int tries = 0; tries < Kinds && Template(kind) == null; tries++) kind = (kind + 1) % Kinds;
            if (Template(kind) == null) return;
            next = (kind + 1) % Kinds;

            Shot s = spare.TryGetValue(kind, out Stack<Shot> ready) && ready.Count > 0 ? ready.Pop() : Make(kind);

            // Placed before anything plays, so the trail starts here and does not streak in from where it last landed.
            s.Body.position = from;
            s.Body.gameObject.SetActive(true);
            foreach (Renderer r in s.Renderers) r.enabled = true;
            foreach (ParticleSystem p in s.Trail)
            {
                p.Clear(false);
                p.Play(false);
            }

            flying.Add(new Flight { Shot = s, Target = shot.Target, From = from, To = to, Seconds = shot.FlightSeconds });
        }

        Shot Make(int kind)
        {
            GameObject copy = Instantiate(Template(kind), transform);
            var trail = copy.GetComponentsInChildren<ParticleSystem>(true);

            // The trail's renderers are the particles' own; only the slice itself is hidden on landing.
            var renderers = new List<Renderer>();
            foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
                if (!(r is ParticleSystemRenderer)) renderers.Add(r);

            return new Shot { Kind = kind, Body = copy.transform, Renderers = renderers.ToArray(), Trail = trail };
        }

        void Update()
        {
            Fly();
            Cool();
        }

        void Fly()
        {
            if (flying.Count == 0) return;

            DefenseMotionProfile motion = post != null ? post.Motion : null;
            float spin = motion != null ? motion.projectileSpin : 0f;
            float height = motion != null ? motion.targetHeight : 0.9f;
            Vector3 axis = motion != null && motion.projectileSpinAxis.sqrMagnitude > 1e-6f ? motion.projectileSpinAxis.normalized : Vector3.right;
            bool facesFlight = motion != null && motion.projectileFacesFlight;
            float dt = Time.deltaTime;

            for (int i = flying.Count - 1; i >= 0; i--)
            {
                Flight f = flying[i];
                f.Elapsed += dt;

                // Follows the target while it lives, so the shot lands on it and not where it was.
                IDefenseTarget target = f.Target;
                if (target != null && !(target is Object o && o == null) && target.IsAlive)
                    f.To = target.Position + Vector3.up * height;

                float t = Mathf.Clamp01(f.Elapsed / f.Seconds);
                Transform body = f.Shot.Body;
                body.position = motion != null ? motion.LobPoint(f.From, f.To, t) : Vector3.Lerp(f.From, f.To, t);

                // Level along the line to the target, or - for something thrown point first - along the arc itself.
                Vector3 across = f.To - f.From;
                if (facesFlight && motion != null)
                    across = motion.LobPoint(f.From, f.To, Mathf.Min(1f, t + 0.02f)) - motion.LobPoint(f.From, f.To, Mathf.Max(0f, t - 0.02f));
                else across.y = 0f;

                Quaternion heading = across.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(across) : body.rotation;
                body.rotation = heading * Quaternion.AngleAxis(f.Elapsed * spin, axis);

                if (t < 1f) continue;

                if (post != null) post.PlayEffect(ImpactFor(f.Shot.Kind), f.To);
                Land(f.Shot);
                flying.RemoveAt(i);
            }
        }

        /// <summary>The slice is gone; its trail stops emitting and is left to fade where it is.</summary>
        void Land(Shot s)
        {
            foreach (Renderer r in s.Renderers) r.enabled = false;
            foreach (ParticleSystem p in s.Trail) p.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            cooling.Add(s);
        }

        /// <summary>Copies whose trails have died away go back in the pool.</summary>
        void Cool()
        {
            for (int i = cooling.Count - 1; i >= 0; i--)
            {
                Shot s = cooling[i];
                bool alive = false;
                foreach (ParticleSystem p in s.Trail)
                    if (p.IsAlive(false)) { alive = true; break; }

                if (alive) continue;

                Store(s);
                cooling.RemoveAt(i);
            }
        }

        void Store(Shot s)
        {
            if (s.Body == null) return;

            foreach (ParticleSystem p in s.Trail) p.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            s.Body.gameObject.SetActive(false);

            if (!spare.TryGetValue(s.Kind, out Stack<Shot> ready)) spare[s.Kind] = ready = new Stack<Shot>();
            ready.Push(s);
        }

        /// <summary>Editor wiring, for the prefab generator.</summary>
        public void Configure(DefensePost owner, GameObject thrown, PooledEffect landing)
        {
            post = owner;
            projectile = thrown;
            impact = landing;
            moreProjectiles = new GameObject[0];
            moreImpacts = new PooledEffect[0];
        }

        /// <summary>Editor wiring for a thrower with several things to throw, in turn, each landing in its own burst.</summary>
        public void Configure(DefensePost owner, GameObject[] thrown, PooledEffect[] landings)
        {
            post = owner;
            projectile = thrown.Length > 0 ? thrown[0] : null;
            impact = landings.Length > 0 ? landings[0] : null;

            moreProjectiles = new GameObject[Mathf.Max(0, thrown.Length - 1)];
            moreImpacts = new PooledEffect[moreProjectiles.Length];
            for (int i = 0; i < moreProjectiles.Length; i++)
            {
                moreProjectiles[i] = thrown[i + 1];
                moreImpacts[i] = i + 1 < landings.Length ? landings[i + 1] : null;
            }
        }
    }
}
