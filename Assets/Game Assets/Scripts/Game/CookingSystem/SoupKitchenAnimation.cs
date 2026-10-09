using UnityEngine;

/// <summary>
/// Brings the soup corner to life while its station is cooking: the fire roars up round the foot
/// of the stockpot and behind the firebox grate, the soup bubbles and turns, the spoon stirs it,
/// steam rolls off the top and the saucepan's lid rattles, puffing. When the last pot is done it
/// all dies back to a banked fire glowing behind the grate.
///
/// "Cooking" is the station having anything in progress (<see cref="CookeryStation.recipiesInProgress"/>),
/// so it follows the station however a cook was started. The change is eased in and out rather
/// than switched, so the fire takes a moment to catch and a little longer to die down.
///
/// The parts are built by the Last Orders / Kitchen soup corner builder; this only moves them.
/// </summary>
[DisallowMultipleComponent]
public sealed class SoupKitchenAnimation : MonoBehaviour
{
    [Tooltip("The station whose cooking drives this. Found among the parents when left empty.")]
    [SerializeField] CookeryStation station;

    [Tooltip("Plays the cooking animation with nothing cooking, for checking how it looks.")]
    [SerializeField] bool previewBusy;

    [Header("Parts")]
    [Tooltip("The soup's surface, turned slowly about its own up axis as it is stirred.")]
    [SerializeField] Transform soup;

    [Tooltip("Pivots on the middle of the pot, so turning it carries the spoon round the soup.")]
    [SerializeField] Transform spoon;

    [SerializeField] Transform lid;

    [Tooltip("Domes that swell on the soup and pop. Children of the soup, so they turn with it.")]
    [SerializeField] Transform[] bubbles;

    [Tooltip("Rings of flame tongues round the foot of the pot, flickering out of step with each other. Out when nothing is cooking.")]
    [SerializeField] Transform[] potFlames;

    [Tooltip("The fire behind the firebox grate. Banked low when nothing is cooking.")]
    [SerializeField] Transform[] fireboxFlames;

    [SerializeField] ParticleSystem steam;

    [Tooltip("Puffs from under the lid, one each time it rattles.")]
    [SerializeField] ParticleSystem lidSteam;

    [Header("Feel")]
    [SerializeField] float warmUpSeconds = 1.2f;
    [SerializeField] float coolDownSeconds = 2.5f;
    [SerializeField] float stirTurnsPerSecond = 0.4f;
    [SerializeField] float steamPerSecond = 7f;

    [Tooltip("How far from the middle of the soup bubbles come up, in metres.")]
    [SerializeField] float bubbleReach = 0.17f;

    [Tooltip("How high the fire behind the grate burns with nothing cooking, against 1 when cooking.")]
    [SerializeField, Range(0f, 1f)] float bankedFire = 0.35f;

    float heat;
    float stir;
    Vector3 soupRest;
    Quaternion soupRestRotation;
    Quaternion spoonRest;
    Vector3 lidRest;
    Quaternion lidRestRotation;
    Vector3[] potFlameRest, fireboxFlameRest;
    Bubble[] bubbleStates;
    float lidWait, lidHop = -1f;
    Vector3 lidAxis = Vector3.right;

    struct Bubble
    {
        public float Clock, Delay, Grow, Size;
    }

    /// <summary>Whether the station has anything cooking (or the preview is on).</summary>
    public bool Busy => previewBusy || (station != null && station.recipiesInProgress != null && station.recipiesInProgress.Count > 0);

    /// <summary>0 to 1: how far the corner has warmed up to full cooking.</summary>
    public float Heat => heat;

    void Awake()
    {
        if (station == null) station = GetComponentInParent<CookeryStation>();

        if (soup != null)
        {
            soupRest = soup.localPosition;
            soupRestRotation = soup.localRotation;
        }

        if (spoon != null) spoonRest = spoon.localRotation;

        if (lid != null)
        {
            lidRest = lid.localPosition;
            lidRestRotation = lid.localRotation;
        }

        potFlameRest = RestScales(potFlames);
        fireboxFlameRest = RestScales(fireboxFlames);

        bubbleStates = new Bubble[bubbles != null ? bubbles.Length : 0];
        for (int i = 0; i < bubbleStates.Length; i++)
        {
            Restart(i);
            bubbleStates[i].Delay = Random.Range(0f, 1.2f);
        }
    }

    void OnEnable()
    {
        // Both left running with nothing to give out: the animation sets the steam's rate and emits the lid's puffs.
        if (steam != null) steam.Play(true);
        if (lidSteam != null) lidSteam.Play(true);
        Apply(0f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float rate = Busy ? 1f / Mathf.Max(0.01f, warmUpSeconds) : -1f / Mathf.Max(0.01f, coolDownSeconds);
        heat = Mathf.Clamp01(heat + rate * dt);
        Apply(dt);
    }

    void Apply(float dt)
    {
        float h = heat * heat * (3f - 2f * heat);
        float t = Time.time;

        Flicker(potFlames, potFlameRest, h, t);
        Flicker(fireboxFlames, fireboxFlameRest, Mathf.Lerp(bankedFire, 1f, h), t + 3.1f);

        // The spoon goes round with the heat; the soup follows it, a little behind. Wrapped at 20
        // turns, where the soup (at 0.55 of the spoon) has also come round to a whole turn.
        stir = Mathf.Repeat(stir + stirTurnsPerSecond * 360f * h * dt, 7200f);
        if (spoon != null) spoon.localRotation = Quaternion.AngleAxis(stir, Vector3.up) * spoonRest;

        if (soup != null)
        {
            soup.localRotation = Quaternion.AngleAxis(stir * 0.55f, Vector3.up) * soupRestRotation;
            soup.localPosition = soupRest + Vector3.up * (0.004f * h * Mathf.Sin(t * 3.3f));
        }

        Bubbles(h, dt);
        Lid(h, dt);

        if (steam != null)
        {
            var emission = steam.emission;
            emission.rateOverTime = h > 0.05f ? steamPerSecond * h : 0f;
        }
    }

    /// <summary>
    /// Flames scaled up their height by <paramref name="level"/>, each set flickering out of step
    /// with the next. Only their height: a set is a ring or a row of tongues round its origin, and
    /// narrowing it would draw them in toward the middle. A set burnt down to nothing is switched
    /// off rather than drawn flat.
    /// </summary>
    static void Flicker(Transform[] flames, Vector3[] rest, float level, float t)
    {
        if (flames == null) return;

        for (int i = 0; i < flames.Length; i++)
        {
            Transform flame = flames[i];
            if (flame == null) continue;

            float phase = i * 2.4f;
            float lick = 0.82f + 0.13f * Mathf.Sin(t * 13f + phase) + 0.07f * Mathf.Sin(t * 29f + phase * 1.7f);
            float height = level * lick;

            bool lit = height > 0.02f;
            if (flame.gameObject.activeSelf != lit) flame.gameObject.SetActive(lit);
            if (!lit) continue;

            Vector3 s = rest[i];
            flame.localScale = new Vector3(s.x, s.y * height, s.z);
        }
    }

    /// <summary>Bubbles swelling up out of the soup and popping, quicker and more of them the hotter it is.</summary>
    void Bubbles(float h, float dt)
    {
        for (int i = 0; i < bubbleStates.Length; i++)
        {
            Transform bubble = bubbles[i];
            if (bubble == null) continue;

            ref Bubble b = ref bubbleStates[i];
            bool boiling = h > 0.2f;

            if (b.Clock < b.Delay)
            {
                if (boiling) b.Clock += dt;
                Show(bubble, 0f);
                continue;
            }

            b.Clock += dt * Mathf.Lerp(0.6f, 1f, h);
            float grown = b.Clock - b.Delay;
            const float pop = 0.08f;

            float size;
            if (grown < b.Grow) size = b.Size * Mathf.Sqrt(grown / b.Grow);
            else if (grown < b.Grow + pop) size = b.Size * (1f + 0.35f * (grown - b.Grow) / pop);
            else
            {
                size = 0f;
                Restart(i);
                if (!boiling) b.Delay = float.MaxValue;
            }

            Show(bubble, size);
        }

        if (h > 0.2f)
        {
            // Bubbles held back when the soup went off the boil come up again once it is back on.
            for (int i = 0; i < bubbleStates.Length; i++)
                if (bubbleStates[i].Delay == float.MaxValue) Restart(i);
        }
    }

    void Restart(int i)
    {
        Transform bubble = bubbles[i];
        Vector2 at = Random.insideUnitCircle * bubbleReach;
        if (bubble != null) bubble.localPosition = new Vector3(at.x, 0f, at.y);

        bubbleStates[i] = new Bubble
        {
            Clock = 0f,
            Delay = Random.Range(0.05f, 0.9f),
            Grow = Random.Range(0.35f, 0.8f),
            Size = Random.Range(0.6f, 1f),
        };
    }

    static void Show(Transform bubble, float size)
    {
        bool shown = size > 0.01f;
        if (bubble.gameObject.activeSelf != shown) bubble.gameObject.SetActive(shown);
        if (shown) bubble.localScale = Vector3.one * size;
    }

    /// <summary>The saucepan's lid hopping on its rim now and then once the pan is properly on the boil, letting out a puff each time.</summary>
    void Lid(float h, float dt)
    {
        if (lid == null) return;

        if (lidHop >= 0f)
        {
            const float hop = 0.13f;
            lidHop += dt;
            float k = lidHop < hop ? Mathf.Sin(Mathf.PI * lidHop / hop) : 0f;
            lid.localPosition = lidRest + Vector3.up * (0.008f * k);
            lid.localRotation = Quaternion.AngleAxis(6f * k, lidAxis) * lidRestRotation;
            if (lidHop >= hop) lidHop = -1f;
            return;
        }

        lid.localPosition = lidRest;
        lid.localRotation = lidRestRotation;

        if (h < 0.6f) return;

        lidWait -= dt;
        if (lidWait > 0f) return;

        lidHop = 0f;
        lidWait = Random.Range(0.25f, 0.9f) / h;
        float a = Random.Range(0f, Mathf.PI * 2f);
        lidAxis = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        if (lidSteam != null) lidSteam.Emit(Random.Range(1, 3));
    }

    static Vector3[] RestScales(Transform[] parts)
    {
        var rest = new Vector3[parts != null ? parts.Length : 0];
        for (int i = 0; i < rest.Length; i++) rest[i] = parts[i] != null ? parts[i].localScale : Vector3.one;
        return rest;
    }

    /// <summary>Wires the parts in, for the builder that makes them.</summary>
    public void Configure(Transform soupSurface, Transform spoonPivot, Transform panLid, Transform[] soupBubbles, Transform[] flamesRoundPot,
                          Transform[] flamesInFirebox, ParticleSystem potSteam, ParticleSystem steamFromLid)
    {
        soup = soupSurface;
        spoon = spoonPivot;
        lid = panLid;
        bubbles = soupBubbles;
        potFlames = flamesRoundPot;
        fireboxFlames = flamesInFirebox;
        steam = potSteam;
        lidSteam = steamFromLid;
    }
}
