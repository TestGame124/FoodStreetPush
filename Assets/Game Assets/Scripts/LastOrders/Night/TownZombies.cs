using System;
using System.Collections.Generic;
using LastOrders.Core;
using LastOrders.DayCycle;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LastOrders.Night
{
    /// <summary>
    /// The zombies in the ruined town by day: a few in each patch of ground around the
    /// compound, ambling about and going nowhere in particular.
    ///
    /// They are scenery with a job. Nothing shoots them and they never come in - but a player
    /// who can see them all day through the fence never has to be told what the night is going
    /// to be. At dusk they stop wandering and drift toward the gate, drawn by the lights, and by
    /// full dark they are gone: what comes up the road after that is the night's own wave.
    ///
    /// Deliberately not on the nav grid or in the night's list of enemies. They walk straight
    /// lines inside their own patch, which is all an amble needs, and the guard towers do not
    /// waste a single shot on them.
    /// </summary>
    [DefaultExecutionOrder(1180)]
    public sealed class TownZombies : MonoBehaviour
    {
        [Serializable]
        public struct Area
        {
            public string name;

            [Tooltip("Centre of the patch, world space. Keep it clear of buildings and off the road.")]
            public Vector3 centre;

            [Tooltip("Width (X) and depth (Z) of the patch in metres.")]
            public Vector2 size;

            [Min(0)] public int count;

            public Area(string name, Vector3 centre, Vector2 size, int count)
            {
                this.name = name;
                this.centre = centre;
                this.size = size;
                this.count = count;
            }

            public Vector3 RandomPoint() => centre + new Vector3(
                Random.Range(-0.5f, 0.5f) * size.x, 0f, Random.Range(-0.5f, 0.5f) * size.y);

            /// <summary>The point of the patch closest to <paramref name="target"/>.</summary>
            public Vector3 Closest(Vector3 target) => new Vector3(
                Mathf.Clamp(target.x, centre.x - size.x * 0.5f, centre.x + size.x * 0.5f), centre.y,
                Mathf.Clamp(target.z, centre.z - size.y * 0.5f, centre.z + size.y * 0.5f));
        }

        sealed class Wanderer
        {
            public Transform Body;
            public int Area;
            public Vector3 Target;
            public float Speed;
            public float Pause;
        }

        [SerializeField] GameLifetimeScope scope;

        [Tooltip("One zombie body with no agent on it: they are never shot at and never navigate.")]
        [SerializeField] GameObject wandererPrefab;

        [SerializeField] Area[] areas;

        [Tooltip("What draws them at dusk. The gate - the light and the noise are all coming from there.")]
        [SerializeField] Transform lure;

        [Tooltip("Metres per second, slowest to fastest. Slower than the night's zombies: nothing " +
                 "has their attention yet.")]
        [SerializeField] Vector2 speedRange = new Vector2(0.35f, 0.7f);

        [Tooltip("Seconds stood still between one amble and the next.")]
        [SerializeField] Vector2 pauseRange = new Vector2(1.5f, 6f);

        [Tooltip("How much faster they move once the lights have their attention.")]
        [SerializeField] float duskSpeedMultiplier = 1.5f;

        [SerializeField] float turnSpeed = 120f;

        [Tooltip("Their skin, multiplied over the character art. The same sickly hue as the night's zombies.")]
        [SerializeField] Color tint = new Color(0.55f, 0.65f, 0.45f);

        readonly List<Wanderer> wanderers = new List<Wanderer>();

        GameComposition composition;
        DayPhase phase = DayPhase.Day;
        bool shown;

        public int Count => wanderers.Count;

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (wandererPrefab == null)
            {
                Debug.LogError("[TownZombies] No wanderer prefab, so the town is empty by day. Run " +
                               "'Last Orders / Hospital & Zombies / Set Up (Current Scene)'.", this);
                enabled = false;
                return;
            }

            Spawn();

            if (composition != null)
            {
                composition.Machine.OnPhaseChanged += HandlePhaseChanged;
                phase = composition.Machine.CurrentPhase;
            }

            Apply(phase, scatter: true);
        }

        void OnDestroy()
        {
            if (composition != null) composition.Machine.OnPhaseChanged -= HandlePhaseChanged;
        }

        void Spawn()
        {
            if (areas == null) return;

            var block = new MaterialPropertyBlock();

            for (int a = 0; a < areas.Length; a++)
            {
                for (int i = 0; i < areas[a].count; i++)
                {
                    GameObject body = Instantiate(wandererPrefab, areas[a].RandomPoint(),
                                                  Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), transform);
                    body.name = $"TownZombie_{areas[a].name}_{i}";

                    Tint(body, tint, block);

                    wanderers.Add(new Wanderer { Body = body.transform, Area = a });
                }
            }
        }

        void HandlePhaseChanged(DayPhase previous, DayPhase current) => Apply(current, scatter: !shown);

        /// <summary>
        /// Out by day, drawn in at dusk, gone at night. Coming back after a night they are put
        /// somewhere new in their patch, so the morning town is not a still of last evening's.
        /// </summary>
        void Apply(DayPhase current, bool scatter)
        {
            phase = current;

            bool show = current == DayPhase.Dawn || current == DayPhase.Day || current == DayPhase.Dusk;
            bool wasShown = shown;
            shown = show;

            for (int i = 0; i < wanderers.Count; i++)
            {
                Wanderer w = wanderers[i];
                if (w.Body == null) continue;

                if (w.Body.gameObject.activeSelf != show) w.Body.gameObject.SetActive(show);
                if (!show) continue;

                if (scatter || !wasShown) w.Body.position = areas[w.Area].RandomPoint();

                w.Speed = Random.Range(speedRange.x, speedRange.y);
                w.Pause = Random.Range(0f, pauseRange.y);
                w.Target = current == DayPhase.Dusk ? DuskPoint(w.Area) : areas[w.Area].RandomPoint();

                if (current == DayPhase.Dusk)
                {
                    // Everyone looks up at once when the lights come on.
                    w.Pause = Random.Range(0f, 1.2f);
                    w.Speed *= duskSpeedMultiplier;
                }
            }
        }

        /// <summary>The edge of the patch nearest the gate, give or take - they bunch up, not queue.</summary>
        Vector3 DuskPoint(int area)
        {
            Vector3 toward = lure != null ? lure.position : transform.position;
            Vector3 edge = areas[area].Closest(toward);
            Vector2 jitter = Random.insideUnitCircle * 1.5f;

            return areas[area].Closest(edge + new Vector3(jitter.x, 0f, jitter.y));
        }

        void Update()
        {
            if (!shown) return;

            // The morning screen stops the world, and they are part of it.
            if (composition != null && composition.Time.IsPaused) return;

            float dt = Time.deltaTime;

            for (int i = 0; i < wanderers.Count; i++)
            {
                Wanderer w = wanderers[i];
                if (w.Body == null) continue;

                if (w.Pause > 0f)
                {
                    w.Pause -= dt;
                    continue;
                }

                Vector3 here = w.Body.position;
                Vector3 goal = new Vector3(w.Target.x, here.y, w.Target.z);
                Vector3 offset = goal - here;
                float distance = offset.magnitude;

                if (distance <= 0.2f)
                {
                    // At dusk they stay where the lights drew them. By day they pick somewhere
                    // else to be, eventually.
                    w.Pause = phase == DayPhase.Dusk ? float.MaxValue : Random.Range(pauseRange.x, pauseRange.y);
                    if (phase != DayPhase.Dusk) w.Target = areas[w.Area].RandomPoint();
                    continue;
                }

                Vector3 direction = offset / distance;
                w.Body.position = here + direction * Mathf.Min(w.Speed * dt, distance);
                w.Body.rotation = Quaternion.RotateTowards(w.Body.rotation, Quaternion.LookRotation(direction),
                                                           turnSpeed * dt);
            }
        }

        /// <summary>
        /// Multiplies the character art by the zombie hue. A property block rather than a new
        /// material, so a town full of them still batches; the blob shadow is left alone.
        /// </summary>
        public static void Tint(GameObject body, Color colour, MaterialPropertyBlock block)
        {
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is SpriteRenderer) continue;

                renderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, colour);
                block.SetColor(ColorId, colour);
                renderer.SetPropertyBlock(block);
            }
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(GameLifetimeScope lifetimeScope, GameObject prefab, Area[] patches, Transform drawnTo)
        {
            scope = lifetimeScope;
            wandererPrefab = prefab;
            areas = patches;
            lure = drawnTo;
        }

        void OnDrawGizmosSelected()
        {
            if (areas == null) return;

            Gizmos.color = new Color(0.55f, 0.75f, 0.4f, 0.8f);
            foreach (Area area in areas)
                Gizmos.DrawWireCube(area.centre + Vector3.up * 0.1f, new Vector3(area.size.x, 0.2f, area.size.y));
        }
    }
}
