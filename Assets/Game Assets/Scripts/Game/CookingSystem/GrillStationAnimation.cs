using LastOrders.Audio;
using UnityEngine;

/// <summary>
/// Brings the grill corner to life while its station is cooking: the coals glow up from a banked
/// red to orange, flames lick up between the bars, smoke rolls up into the hood and sparks drift;
/// two cobs, three skewers, a fish and halves of tomato are laid on the grate one after another;
/// and the tongs work along it, taking hold of a cob, a skewer or the fish, lifting it and turning
/// it over, and letting it down - the fat flares up under it in a burst of flame, smoke and sparks.
/// When the last order is done the food comes off, the tongs are laid back on the side table and
/// the fire dies back to a glow.
///
/// "Cooking" is the station having anything in progress (<see cref="CookeryStation.recipiesInProgress"/>),
/// so it follows the station however a dish was started. The change is eased in and out rather
/// than switched, so the fire takes a moment to catch and a little longer to die down.
///
/// The parts are built by the Last Orders / Kitchen grill builder; this only moves them. Every part
/// lives in the same space (the corner's), so the poses below are in that space.
/// </summary>
[DisallowMultipleComponent]
public sealed class GrillStationAnimation : MonoBehaviour
{
    [Tooltip("The station whose cooking drives this. Found among the parents when left empty.")]
    [SerializeField] CookeryStation station;

    [Tooltip("Plays the cooking animation with nothing cooking, for checking how it looks.")]
    [SerializeField] bool previewBusy;

    [Header("On the grate")]
    [Tooltip("Everything cooked on the grate, in the order it is laid on.")]
    [SerializeField] Transform[] food;

    [Tooltip("What the tongs turn, in the order they go to it - each lying along X with its origin in its middle.")]
    [SerializeField] Transform[] turns;

    [Tooltip("How far each of those is turned over about its length, in degrees: a quarter for a cob, half for a skewer or the fish.")]
    [SerializeField] float[] turnDegrees;

    [Tooltip("Their origin between the tips of the jaws, lying flat with the handles back toward -Z.")]
    [SerializeField] Transform tongs;

    [Header("Fire")]
    [Tooltip("Rows of flame tongues on the coals, licking up between the bars. Scaled in height only.")]
    [SerializeField] Transform[] flames;

    [Tooltip("The glowing bed under the coals: its colour runs from banked to hot.")]
    [SerializeField] Renderer coals;

    [SerializeField] Color coalsBanked = new Color(0.4f, 0.1f, 0.04f);
    [SerializeField] Color coalsHot = new Color(0.95f, 0.42f, 0.12f);

    [SerializeField] ParticleSystem smoke;
    [SerializeField] ParticleSystem sparks;

    [Header("Feel")]
    [SerializeField] float warmUpSeconds = 1.4f;
    [SerializeField] float coolDownSeconds = 2.5f;
    [SerializeField] float smokePerSecond = 5f;
    [SerializeField] float sparksPerSecond = 3f;
    [SerializeField] float turnSeconds = 1.3f;

    [Tooltip("Seconds the tongs wait between turns, at random between the two.")]
    [SerializeField] Vector2 pause = new Vector2(0.4f, 1.1f);

    [Tooltip("How high the flames burn with nothing cooking, against 1 when cooking: below the bars.")]
    [SerializeField, Range(0f, 1f)] float bankedFire = 0.2f;

    enum Stage { Rest, Hold, Turn, Return }

    float work;
    Stage stage;
    float clock, wait, flare;
    int target;
    bool landed;
    Vector3 tongsRest, holdPosition, startPosition;
    Quaternion tongsRestRotation, holdRotation, startRotation;
    Vector3 tongsRestScale;
    Vector3[] foodRestScale, turnRest, flameRest;
    Quaternion[] turnRestRotation;
    float[] turned;
    MaterialPropertyBlock block;

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    /// <summary>The tongs held out over the grate, jaws forward, handles up toward the cook.</summary>
    static readonly Quaternion Working = Quaternion.Euler(32f, 0f, 0f);

    /// <summary>Whether the station has anything cooking (or the preview is on).</summary>
    public bool Busy => previewBusy || (station != null && station.recipiesInProgress != null && station.recipiesInProgress.Count > 0);

    /// <summary>0 to 1: how far the grill has warmed up to full cooking.</summary>
    public float Heat => work;

    void Awake()
    {
        if (station == null) station = GetComponentInParent<CookeryStation>();

        if (tongs != null)
        {
            tongsRest = tongs.localPosition;
            tongsRestRotation = tongs.localRotation;
            tongsRestScale = tongs.localScale;
        }

        foodRestScale = new Vector3[Count(food)];
        for (int i = 0; i < foodRestScale.Length; i++) foodRestScale[i] = food[i] != null ? food[i].localScale : Vector3.one;

        turnRest = new Vector3[Count(turns)];
        turnRestRotation = new Quaternion[turnRest.Length];
        turned = new float[turnRest.Length];
        for (int i = 0; i < turnRest.Length; i++)
        {
            turnRest[i] = turns[i] != null ? turns[i].localPosition : Vector3.zero;
            turnRestRotation[i] = turns[i] != null ? turns[i].localRotation : Quaternion.identity;
        }

        flameRest = new Vector3[Count(flames)];
        for (int i = 0; i < flameRest.Length; i++) flameRest[i] = flames[i] != null ? flames[i].localScale : Vector3.one;

        block = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        // Left running with nothing to give out: the animation sets their rates and bursts.
        if (smoke != null) smoke.Play(true);
        if (sparks != null) sparks.Play(true);

        stage = Stage.Rest;
        if (tongs != null)
        {
            tongs.localPosition = tongsRest;
            tongs.localRotation = tongsRestRotation;
            tongs.localScale = tongsRestScale;
        }

        Apply(0f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float rate = Busy ? 1f / Mathf.Max(0.01f, warmUpSeconds) : -1f / Mathf.Max(0.01f, coolDownSeconds);
        work = Mathf.Clamp01(work + rate * dt);
        Apply(dt);
    }

    void Apply(float dt)
    {
        float h = Smooth(work);
        float t = Time.time;
        flare = Mathf.Max(0f, flare - dt * 2.4f);

        Fire(h, t);

        // Laid on one after another as the work picks up, taken off in the same order as it winds down.
        int n = Count(food);
        for (int i = 0; i < n; i++)
        {
            float shown = Smooth((work - 0.2f - 0.5f * i / Mathf.Max(1, n)) / 0.18f);
            Transform part = food[i];
            if (part == null) continue;

            bool visible = shown > 0.01f;
            if (part.gameObject.activeSelf != visible) part.gameObject.SetActive(visible);
            if (visible) part.localScale = foodRestScale[i] * shown;
        }

        Tongs(dt);

        if (smoke != null)
        {
            var emission = smoke.emission;
            emission.rateOverTime = h > 0.05f ? smokePerSecond * h * (1f + flare) : 0f;
        }

        if (sparks != null)
        {
            var emission = sparks.emission;
            emission.rateOverTime = h > 0.2f ? sparksPerSecond * h : 0f;
        }
    }

    // --- The fire ---------------------------------------------------------------------------------

    /// <summary>
    /// The coals' glow and the flames over them, both flickering, banked low with nothing cooking and
    /// thrown up by a flare when fat drips. Flames are scaled in height only (a row of tongues drawn
    /// narrower would close up), and a row burnt down to nothing is switched off rather than drawn flat.
    /// </summary>
    void Fire(float h, float t)
    {
        if (coals != null)
        {
            float glow = Mathf.Lerp(0.55f, 1f, h) * (0.92f + 0.08f * Mathf.Sin(t * 7f) + 0.05f * Mathf.Sin(t * 17.3f)) + 0.25f * flare;
            coals.GetPropertyBlock(block);
            block.SetColor(BaseColor, Color.Lerp(coalsBanked, coalsHot, h) * glow);
            coals.SetPropertyBlock(block);
        }

        float level = Mathf.Lerp(bankedFire, 1f, h) + 0.5f * flare * h;
        for (int i = 0; i < Count(flames); i++)
        {
            Transform flame = flames[i];
            if (flame == null) continue;

            float phase = i * 2.4f;
            float lick = 0.82f + 0.13f * Mathf.Sin(t * 13f + phase) + 0.07f * Mathf.Sin(t * 29f + phase * 1.7f);
            float height = level * lick;

            bool lit = height > 0.02f;
            if (flame.gameObject.activeSelf != lit) flame.gameObject.SetActive(lit);
            if (!lit) continue;

            Vector3 s = flameRest[i];
            flame.localScale = new Vector3(s.x, s.y * height, s.z);
        }
    }

    // --- The tongs --------------------------------------------------------------------------------

    /// <summary>
    /// The tongs' round: they wait, go to the next cob, skewer or fish, turn it over and back off;
    /// once the work winds down they go back to the side table.
    /// </summary>
    void Tongs(float dt)
    {
        if (tongs == null || Count(turns) == 0) return;

        switch (stage)
        {
            case Stage.Rest:
                tongs.localPosition = tongsRest;
                tongs.localRotation = tongsRestRotation;
                tongs.localScale = tongsRestScale;
                if (work > 0.9f) Hold(tongsRest, tongsRestRotation, 0.3f);
                break;

            case Stage.Hold:
                wait -= dt;
                tongs.localPosition = holdPosition + Vector3.up * (0.008f * Mathf.Sin(Time.time * 4f));
                tongs.localRotation = holdRotation;
                tongs.localScale = Jaws(1f);
                if (wait > 0f) break;

                if (work > 0.9f) BeginTurn();
                else
                {
                    stage = Stage.Return;
                    clock = 0f;
                }

                break;

            case Stage.Turn:
                Turn(dt);
                break;

            case Stage.Return:
                clock += dt;
                float k = Smooth(clock / 0.7f);
                tongs.localPosition = Vector3.Lerp(holdPosition, tongsRest, k) + Vector3.up * (0.06f * Mathf.Sin(Mathf.PI * k));
                tongs.localRotation = Quaternion.Slerp(holdRotation, tongsRestRotation, k);
                tongs.localScale = Vector3.Lerp(Jaws(1f), tongsRestScale, k);
                if (clock >= 0.7f) stage = Stage.Rest;
                break;
        }
    }

    void Hold(Vector3 at, Quaternion turn, float seconds)
    {
        stage = Stage.Hold;
        holdPosition = at;
        holdRotation = turn;
        wait = seconds;
    }

    void BeginTurn()
    {
        SoundController.Play(SoundId.FoodFlip, tongs != null ? tongs.position : transform.position);
        stage = Stage.Turn;
        clock = 0f;
        landed = false;
        startPosition = tongs.localPosition;
        startRotation = tongs.localRotation;
    }

    /// <summary>
    /// One turn of the piece at <see cref="target"/>: the tongs come over it, jaws open (0 - 0.3),
    /// go down to it (0.3 - 0.42) and close (0.42 - 0.5), lift it and turn it over (0.5 - 0.82), let
    /// it down and open (0.82 - 0.9), and rise off toward the cook (0.9 - 1).
    /// </summary>
    void Turn(float dt)
    {
        clock += dt;
        float k = Mathf.Clamp01(clock / Mathf.Max(0.2f, turnSeconds));
        Transform item = turns[target];
        Vector3 at = turnRest[target];
        Vector3 above = at + new Vector3(0f, 0.12f, -0.03f);
        Vector3 off = at + new Vector3(0.04f, 0.16f, -0.16f);
        float degrees = turnDegrees != null && target < turnDegrees.Length ? turnDegrees[target] : 180f;

        float hop = 0f, roll = 0f;
        if (k < 0.3f)
        {
            float s = Smooth(k / 0.3f);
            tongs.localPosition = Vector3.Lerp(startPosition, above, s) + Vector3.up * (0.05f * Mathf.Sin(Mathf.PI * s));
            tongs.localRotation = Quaternion.Slerp(startRotation, Working, s);
            tongs.localScale = Jaws(1f + 0.25f * s);
        }
        else if (k < 0.5f)
        {
            tongs.localPosition = Vector3.Lerp(above, at, Smooth((k - 0.3f) / 0.12f));
            tongs.localRotation = Working;
            tongs.localScale = Jaws(Mathf.Lerp(1.25f, 0.62f, Smooth((k - 0.42f) / 0.08f)));
        }
        else if (k < 0.82f)
        {
            float s = (k - 0.5f) / 0.32f;
            hop = 0.07f * Mathf.Sin(Mathf.PI * s);
            roll = degrees * Smooth(s);
            tongs.localPosition = at + Vector3.up * hop;
            tongs.localRotation = Quaternion.AngleAxis(-0.25f * roll, Vector3.right) * Working;
            tongs.localScale = Jaws(0.62f);
        }
        else if (k < 0.9f)
        {
            roll = degrees;
            tongs.localPosition = at;
            tongs.localRotation = Working;
            tongs.localScale = Jaws(Mathf.Lerp(0.62f, 1.15f, Smooth((k - 0.82f) / 0.08f)));
        }
        else
        {
            roll = degrees;
            float s = Smooth((k - 0.9f) / 0.1f);
            tongs.localPosition = Vector3.Lerp(at, off, s);
            tongs.localRotation = Working;
            tongs.localScale = Jaws(1.15f);
        }

        if (item != null)
        {
            item.localPosition = at + Vector3.up * hop;
            item.localRotation = turnRestRotation[target] * Quaternion.AngleAxis(turned[target] + roll, Vector3.right);
        }

        // Down the other way up - checked outside the stages, so a long frame cannot skip it.
        if (k >= 0.82f && !landed)
        {
            landed = true;
            Land(item);
        }

        if (k >= 1f)
        {
            turned[target] = Mathf.Repeat(turned[target] + degrees, 360f);
            target = (target + 1) % turns.Length;
            Hold(off, Working, Random.Range(pause.x, pause.y));
        }
    }

    /// <summary>The fat a turned piece lets go of: the fire flares up, a gust of smoke and a spray of sparks where it lands.</summary>
    void Land(Transform item)
    {
        flare = 1f;
        SoundController.Play(SoundId.FatFlare, item != null ? item.position : transform.position);
        if (item == null) return;

        var at = new ParticleSystem.EmitParams { position = item.position + Vector3.up * 0.03f, applyShapeToPosition = true };
        if (smoke != null) smoke.Emit(at, 4);
        if (sparks != null) sparks.Emit(at, 10);
    }

    /// <summary>The tongs' scale with the jaws <paramref name="open"/> (1 as built, less closed on something).</summary>
    Vector3 Jaws(float open) => new Vector3(tongsRestScale.x * open, tongsRestScale.y, tongsRestScale.z);

    // --- Helpers ----------------------------------------------------------------------------------

    static int Count<T>(T[] parts) => parts != null ? parts.Length : 0;

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    /// <summary>Wires the parts in, for the builder that makes them.</summary>
    public void Configure(Transform[] onTheGrate, Transform[] toTurn, float[] turnBy, Transform grillTongs, Transform[] flameRows, Renderer coalBed,
                          ParticleSystem grillSmoke, ParticleSystem grillSparks)
    {
        food = onTheGrate;
        turns = toTurn;
        turnDegrees = turnBy;
        tongs = grillTongs;
        flames = flameRows;
        coals = coalBed;
        smoke = grillSmoke;
        sparks = grillSparks;
    }
}
