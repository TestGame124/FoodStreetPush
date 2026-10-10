using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace LastOrders.Ambience
{
    /// <summary>
    /// The small life: butterflies about the grass by day, fireflies blinking over it at night.
    ///
    /// Butterflies keep to patches round wherever the player is looking. One left behind out of
    /// sight is given a new patch in view and flies in to it from off screen, so they never pop up in
    /// plain sight. At dusk they go. Fireflies are let out one at a time over open ground about the
    /// view - the grass, not the diner floor - as many a second as the dark allows.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbientInsects : MonoBehaviour
    {
        [Header("Butterflies")]
        [SerializeField] Butterfly[] butterflies;
        [SerializeField, Min(0)] int butterflyCount = 8;

        [Tooltip("Metres a butterfly roams about its patch.")]
        [SerializeField] float roam = 3.5f;

        [Header("Fireflies")]
        [Tooltip("Kept playing in world space; emitted into by hand, over open ground.")]
        [SerializeField] ParticleSystem fireflies;

        [Tooltip("Fireflies a second at full dark.")]
        [SerializeField] float fireflyRate = 7f;

        readonly List<Butterfly> flying = new List<Butterfly>();
        readonly List<float> unseen = new List<float>();
        Transform cage;
        AmbientBirds birds;
        float fireflyDue;

        void Start()
        {
            cage = new GameObject("Butterflies").transform;
            cage.SetParent(transform, false);
            birds = GetComponent<AmbientBirds>();

            if (fireflies != null) fireflies.Play();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            float day = AmbientDaylight.Day;

            Butterflies(dt, day > 0.5f);
            Fireflies(dt);
        }

        void Butterflies(float dt, bool day)
        {
            if (butterflies == null || butterflies.Length == 0) return;

            Vector3 focus = AmbientView.Focus;
            float reach = AmbientView.Reach;

            if (day)
            {
                while (flying.Count < butterflyCount)
                {
                    Butterfly prefab = butterflies[Random.Range(0, butterflies.Length)];
                    Butterfly fly = Instantiate(prefab, cage);
                    Vector3 patch = Patch(focus, reach);
                    // The first ones are already about; later ones come in from off screen.
                    fly.Place(Time.timeSinceLevelLoad < 2f ? patch + Vector3.up : Entry(patch, reach));
                    fly.Keep(patch, roam);
                    flying.Add(fly);
                    unseen.Add(0f);
                }
            }

            for (int i = flying.Count - 1; i >= 0; i--)
            {
                Butterfly fly = flying[i];
                if (fly == null)
                {
                    flying.RemoveAt(i);
                    unseen.RemoveAt(i);
                    continue;
                }

                bool seen = AmbientView.Sees(fly.transform.position, 0.1f);
                unseen[i] = seen ? 0f : unseen[i] + dt;

                if (!day)
                {
                    // Gone to ground for the night: away up out of the view, then put away.
                    fly.Keep(fly.transform.position + Vector3.up * 2f + AmbientWind.Heading * 6f, 0.5f);
                    if (unseen[i] > 1f)
                    {
                        Destroy(fly.gameObject);
                        flying.RemoveAt(i);
                        unseen.RemoveAt(i);
                    }

                    continue;
                }

                // Left behind by the camera: a new patch in view, flown in to from off screen.
                if (unseen[i] > 4f && !AmbientView.Sees(fly.Home, 0f))
                {
                    Vector3 patch = Patch(focus, reach);
                    fly.Place(Entry(patch, reach));
                    fly.Keep(patch, roam);
                    unseen[i] = 0f;
                }
            }
        }

        void Fireflies(float dt)
        {
            if (fireflies == null) return;

            fireflyDue += fireflyRate * AmbientDaylight.Night * dt;
            if (fireflyDue < 1f) return;

            GroundMap ground = birds != null ? birds.Ground : null;
            Vector3 focus = AmbientView.Focus;
            float reach = AmbientView.Reach * 1.3f;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };

            while (fireflyDue >= 1f)
            {
                fireflyDue -= 1f;
                for (int attempt = 0; attempt < 6; attempt++)
                {
                    Vector2 p = Random.insideUnitCircle * reach;
                    var at = new Vector3(focus.x + p.x, 0f, focus.z + p.y);
                    if (ground != null && !ground.IsOpen(at, 0.5f)) continue;

                    at.y = Random.Range(0.3f, 2.2f);
                    emit.position = at;
                    fireflies.Emit(emit, 1);
                    break;
                }
            }
        }

        /// <summary>Somewhere in the open near the middle of the view: over grass, not in the diner or a shed.</summary>
        Vector3 Patch(Vector3 focus, float reach)
        {
            GroundMap ground = birds != null ? birds.Ground : null;
            Vector3 patch = focus;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                Vector2 p = Random.insideUnitCircle * reach * (attempt < 10 ? 0.75f : 1.2f);
                patch = new Vector3(focus.x + p.x, 0f, focus.z + p.y);
                if (ground == null || ground.IsOpen(patch, 1.2f)) return patch;
            }

            return patch;
        }

        static Vector3 Entry(Vector3 patch, float reach)
        {
            Vector2 d = Random.insideUnitCircle.normalized;
            return patch + new Vector3(d.x, 0f, d.y) * (reach + 4f) + Vector3.up * 1.5f;
        }

        /// <summary>Editor wiring, for the setup tool.</summary>
        public void Configure(Butterfly[] kinds, ParticleSystem night)
        {
            butterflies = kinds;
            fireflies = night;
        }
    }
}
