using UnityEngine;
using Random = UnityEngine.Random;

namespace LastOrders.Ambience
{
    /// <summary>
    /// A butterfly: a jinking, bobbing flight from one spot to the next about the patch of ground it
    /// keeps to, the wings clapping up over the back several times a second; now and then it drops
    /// to the grass to rest with them held up, opening them flat to bask. A gust pushes it off its line.
    ///
    /// <see cref="AmbientInsects"/> says where its patch is and moves it on when the player looks away.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Butterfly : MonoBehaviour
    {
        [SerializeField] Transform wingLeft, wingRight;

        [Tooltip("Wing beats a second.")]
        [SerializeField] float flapHz = 9f;

        [Tooltip("Metres a second about its patch.")]
        [SerializeField] float speed = 1.3f;

        Vector3 home;
        float roam = 3f;
        Vector3 target;
        Vector3 velocity;
        float phase, bob;
        float resting = -1f, restLength;
        float bask, baskTarget, nextBask;
        Quaternion leftRest, rightRest;
        bool captured;

        public Vector3 Home => home;
        public bool Resting => resting >= 0f;

        void Awake() => Capture();

        void Capture()
        {
            if (captured) return;
            captured = true;
            if (wingLeft != null) leftRest = wingLeft.localRotation;
            if (wingRight != null) rightRest = wingRight.localRotation;
        }

        /// <summary>Keeps to <paramref name="patch"/>, roaming up to <paramref name="radius"/> metres.</summary>
        public void Keep(Vector3 patch, float radius)
        {
            home = patch;
            roam = radius;
            if (resting >= 0f && (transform.position - home).sqrMagnitude > roam * roam * 4f) resting = -1f;
            Pick();
        }

        /// <summary>Appears at <paramref name="at"/>, flying.</summary>
        public void Place(Vector3 at)
        {
            gameObject.SetActive(true);
            transform.position = at;
            velocity = Vector3.zero;
            resting = -1f;
            phase = Random.value * 10f;
            bob = Random.value * 10f;
            Pick();
        }

        void Update()
        {
            Capture();
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (resting >= 0f) Rest(dt);
            else Fly(dt);
        }

        void Fly(float dt)
        {
            Vector3 position = transform.position;
            Vector3 to = target - position;
            float far = (home - position).magnitude;

            // Hurrying back across open ground, dawdling about its patch.
            float pace = speed * Mathf.Lerp(1f, 2.6f, Mathf.InverseLerp(roam, roam * 3f, far));
            Vector3 want = to.sqrMagnitude > 1e-4f ? to.normalized * pace : Vector3.zero;

            // The jink: a wandering sideways push that never lets it fly straight.
            float n = Mathf.PerlinNoise(Time.time * 1.7f, GetInstanceID() * 0.13f) - 0.5f;
            Vector3 across = new Vector3(-want.z, 0f, want.x);
            want += across * n * 2.4f;

            velocity = Vector3.Lerp(velocity, want, 1f - Mathf.Exp(-dt * 3f));
            velocity += AmbientWind.VelocityAt(position, 0.9f) * dt * 1.5f;

            bob += dt * Mathf.PI * 2f * 1.6f;
            position += velocity * dt + Vector3.up * (Mathf.Sin(bob) * 0.35f * dt);
            position.y = Mathf.Max(0.12f, position.y);
            transform.position = position;

            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            if (flat.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 1f - Mathf.Exp(-dt * 6f));

            phase += dt * flapHz * Mathf.PI * 2f;
            float open = 0.5f + 0.5f * Mathf.Sin(phase);
            Wings(Mathf.Lerp(78f, 8f, open));

            if (to.sqrMagnitude < 0.15f)
            {
                // Down to rest a while, or on to somewhere else.
                if (Random.value < 0.3f && far < roam)
                {
                    resting = 0f;
                    restLength = Random.Range(2f, 6f);
                    transform.position = new Vector3(position.x, 0.08f, position.z);
                    baskTarget = 0f;
                    nextBask = Random.Range(0.4f, 1.5f);
                }
                else
                {
                    Pick();
                }
            }
        }

        void Rest(float dt)
        {
            resting += dt;

            // Wings up over the back, opened out flat now and then to bask.
            nextBask -= dt;
            if (nextBask <= 0f)
            {
                baskTarget = baskTarget > 0.5f ? 0f : 1f;
                nextBask = Random.Range(0.6f, 1.8f);
            }

            bask = Mathf.MoveTowards(bask, baskTarget, dt * 2.5f);
            Wings(Mathf.Lerp(82f, 12f, bask) + Mathf.Sin(Time.time * 3f) * 3f);

            if (resting >= restLength || AmbientWind.GustAt(transform.position) > 0.8f)
            {
                resting = -1f;
                velocity = Vector3.up * 0.8f;
                Pick();
            }
        }

        void Pick()
        {
            Vector2 p = Random.insideUnitCircle * roam;
            target = home + new Vector3(p.x, Random.Range(0.35f, 1.6f), p.y);
        }

        void Wings(float up)
        {
            if (wingRight != null) wingRight.localRotation = rightRest * Quaternion.AngleAxis(up, Vector3.forward);
            if (wingLeft != null) wingLeft.localRotation = leftRest * Quaternion.AngleAxis(-up, Vector3.forward);
        }

        /// <summary>Editor wiring, for the builder.</summary>
        public void Configure(Transform left, Transform right, float beats, float metresPerSecond)
        {
            wingLeft = left;
            wingRight = right;
            flapHz = beats;
            speed = metresPerSecond;
            captured = false;
        }
    }
}
