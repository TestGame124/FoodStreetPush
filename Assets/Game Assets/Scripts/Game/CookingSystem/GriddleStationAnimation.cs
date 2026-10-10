using UnityEngine;

/// <summary>
/// Brings the griddle corner to life while its station is cooking: the pilot lamp comes on, two
/// eggs, two hash browns and two slices of French toast are laid on the plate one after another,
/// the eggs sizzle, steam lifts off and fat spits; and the spatula works along the plate, sliding
/// under each hash brown and slice in turn and flipping it, a puff of steam where it lands. When
/// the last order is done the food goes, the spatula is laid back in its corner and the lamp goes out.
///
/// "Cooking" is the station having anything in progress (<see cref="CookeryStation.recipiesInProgress"/>),
/// so it follows the station however a dish was started. The change is eased in and out rather than
/// switched, so the plate fills up and clears.
///
/// The parts are built by the Last Orders / Kitchen griddle builder; this only moves them. Every
/// part lives in the same space (the corner's), so the poses below are in that space.
/// </summary>
[DisallowMultipleComponent]
public sealed class GriddleStationAnimation : MonoBehaviour
{
    [Tooltip("The station whose cooking drives this. Found among the parents when left empty.")]
    [SerializeField] CookeryStation station;

    [Tooltip("Plays the cooking animation with nothing cooking, for checking how it looks.")]
    [SerializeField] bool previewBusy;

    [Header("On the plate")]
    [Tooltip("Eggs frying sunny side up, their origin in the middle of the white on the plate.")]
    [SerializeField] Transform[] eggs;

    [Tooltip("What the spatula turns - hash browns and slices of French toast - each with its origin in its middle.")]
    [SerializeField] Transform[] flips;

    [Tooltip("Its origin in the middle of its blade, lying flat; its handle back toward -Z.")]
    [SerializeField] Transform spatula;

    [Tooltip("The pilot lamp on the front panel, lit while the griddle is on.")]
    [SerializeField] GameObject lamp;

    [Header("Effects")]
    [SerializeField] ParticleSystem steam;

    [Tooltip("Fat spitting off the plate: bright specks hopping up.")]
    [SerializeField] ParticleSystem spatter;

    [Header("Feel")]
    [SerializeField] float warmUpSeconds = 1f;
    [SerializeField] float coolDownSeconds = 1.6f;
    [SerializeField] float steamPerSecond = 4f;
    [SerializeField] float spatterPerSecond = 6f;
    [SerializeField] float flipSeconds = 1.1f;

    [Tooltip("Seconds the spatula waits between flips, at random between the two.")]
    [SerializeField] Vector2 pause = new Vector2(0.35f, 0.9f);

    [Tooltip("How high a flipped hash brown or slice goes, in metres.")]
    [SerializeField] float flipHeight = 0.11f;

    enum Stage { Rest, Hold, Flip, Return }

    float work;
    Stage stage;
    float clock, wait;
    int target;
    bool landed;
    Vector3 spatulaRest, holdPosition, startPosition;
    Quaternion spatulaRestRotation, holdRotation, startRotation;
    Vector3[] eggRest, flipRest;
    Quaternion[] flipRestRotation;
    Vector3[] eggRestScale, flipRestScale;
    bool[] flipped;

    /// <summary>The spatula held level, handle back toward the cook, blade pointing into the plate.</summary>
    Quaternion Working => spatulaRestRotation;

    /// <summary>Whether the station has anything cooking (or the preview is on).</summary>
    public bool Busy => previewBusy || (station != null && station.recipiesInProgress != null && station.recipiesInProgress.Count > 0);

    /// <summary>0 to 1: how far the griddle has picked up to full work.</summary>
    public float Work => work;

    void Awake()
    {
        if (station == null) station = GetComponentInParent<CookeryStation>();

        if (spatula != null)
        {
            spatulaRest = spatula.localPosition;
            spatulaRestRotation = spatula.localRotation;
        }

        eggRest = Positions(eggs);
        eggRestScale = Scales(eggs);
        flipRest = Positions(flips);
        flipRestScale = Scales(flips);
        flipRestRotation = new Quaternion[flips != null ? flips.Length : 0];
        for (int i = 0; i < flipRestRotation.Length; i++) flipRestRotation[i] = flips[i] != null ? flips[i].localRotation : Quaternion.identity;
        flipped = new bool[flipRestRotation.Length];
    }

    void OnEnable()
    {
        // Left running with nothing to give out: the animation sets their rates and bursts.
        if (steam != null) steam.Play(true);
        if (spatter != null) spatter.Play(true);

        stage = Stage.Rest;
        if (spatula != null)
        {
            spatula.localPosition = spatulaRest;
            spatula.localRotation = spatulaRestRotation;
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

        if (lamp != null && lamp.activeSelf != work > 0.02f) lamp.SetActive(work > 0.02f);

        // Laid on one after another as the work picks up - eggs, then hash browns, then toast - and
        // taken off in the same order as it winds down.
        for (int i = 0; i < Count(eggs); i++)
        {
            float shown = Smooth((work - 0.08f - 0.1f * i) / 0.2f);
            float sizzle = 1f + 0.025f * h * Mathf.Sin(t * 31f + i * 2.3f);
            Show(eggs[i], shown, new Vector3(eggRestScale[i].x * shown * sizzle, eggRestScale[i].y * shown, eggRestScale[i].z * shown * sizzle));
        }

        for (int i = 0; i < Count(flips); i++)
        {
            float shown = Smooth((work - 0.28f - 0.1f * i) / 0.2f);
            Show(flips[i], shown, flipRestScale[i] * shown);
        }

        Spatula(dt);

        if (steam != null)
        {
            var emission = steam.emission;
            emission.rateOverTime = h > 0.05f ? steamPerSecond * h : 0f;
        }

        if (spatter != null)
        {
            var emission = spatter.emission;
            emission.rateOverTime = h > 0.3f ? spatterPerSecond * h : 0f;
        }
    }

    // --- The spatula ----------------------------------------------------------------------------

    /// <summary>
    /// The spatula's round: it waits, goes to the next hash brown or slice, slides under it from the
    /// front, flips it and backs off; once the work winds down it goes back to its corner.
    /// </summary>
    void Spatula(float dt)
    {
        if (spatula == null || Count(flips) == 0) return;

        switch (stage)
        {
            case Stage.Rest:
                spatula.localPosition = spatulaRest;
                spatula.localRotation = spatulaRestRotation;
                if (work > 0.85f) Hold(spatulaRest, spatulaRestRotation, 0.2f);
                break;

            case Stage.Hold:
                wait -= dt;
                float idle = Mathf.Sin(Time.time * 5f);
                spatula.localPosition = holdPosition + Vector3.up * (0.006f * Mathf.Max(0f, idle));
                spatula.localRotation = holdRotation;
                if (wait > 0f) break;

                if (work > 0.85f) BeginFlip();
                else
                {
                    stage = Stage.Return;
                    clock = 0f;
                }

                break;

            case Stage.Flip:
                Flip(dt);
                break;

            case Stage.Return:
                clock += dt;
                float k = Smooth(clock / 0.6f);
                spatula.localPosition = Vector3.Lerp(holdPosition, spatulaRest, k) + Vector3.up * (0.04f * Mathf.Sin(Mathf.PI * k));
                spatula.localRotation = Quaternion.Slerp(holdRotation, spatulaRestRotation, k);
                if (clock >= 0.6f) stage = Stage.Rest;
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

    void BeginFlip()
    {
        stage = Stage.Flip;
        clock = 0f;
        startPosition = spatula.localPosition;
        startRotation = spatula.localRotation;
    }

    /// <summary>
    /// One flip of the item at <see cref="target"/>: the spatula travels to just in front of it
    /// (0 - 0.3), slides under it (0.3 - 0.42), lifts it, turning it over toward the back as it goes
    /// up and lands it back where it was (0.42 - 0.78), then backs off toward the cook (0.78 - 1).
    /// </summary>
    void Flip(float dt)
    {
        clock += dt;
        float k = Mathf.Clamp01(clock / Mathf.Max(0.2f, flipSeconds));
        Transform item = flips[target];
        Vector3 at = flipRest[target];
        Vector3 under = new Vector3(at.x, spatulaRest.y, at.z);
        Vector3 front = under + new Vector3(0f, 0f, -0.11f);
        Vector3 back = under + new Vector3(0.05f, 0f, -0.15f);

        if (k < 0.3f)
        {
            float s = Smooth(k / 0.3f);
            spatula.localPosition = Vector3.Lerp(startPosition, front, s) + Vector3.up * (0.05f * Mathf.Sin(Mathf.PI * s));
            spatula.localRotation = Quaternion.Slerp(startRotation, Working, s);
        }
        else if (k < 0.42f)
        {
            spatula.localPosition = Vector3.Lerp(front, under, Smooth((k - 0.3f) / 0.12f));
            spatula.localRotation = Working;
        }
        else if (k < 0.78f)
        {
            float s = (k - 0.42f) / 0.36f;
            float arc = Mathf.Sin(Mathf.PI * s);

            // The wrist: up with the item and tipped forward to throw it over, then down again.
            spatula.localPosition = under + Vector3.up * (0.06f * arc);
            spatula.localRotation = Quaternion.AngleAxis(-40f * arc, Vector3.right) * Working;

            if (item != null)
            {
                Quaternion from = Face(target, flipped[target]), to = Face(target, !flipped[target]);
                item.localPosition = at + Vector3.up * (flipHeight * arc) + new Vector3(0f, 0f, 0.02f * arc);
                item.localRotation = Quaternion.Slerp(from, to, Smooth(s));
            }
        }
        else
        {
            float s = Smooth((k - 0.78f) / 0.22f);
            spatula.localPosition = Vector3.Lerp(under, back, s) + Vector3.up * (0.03f * Mathf.Sin(Mathf.PI * s));
            spatula.localRotation = Working;
        }

        // Down the other way up - checked outside the stages, so a long frame cannot skip the landing.
        if (k >= 0.78f && !landed)
        {
            landed = true;
            flipped[target] = !flipped[target];
            if (item != null)
            {
                item.localPosition = at;
                item.localRotation = Face(target, flipped[target]);
                Land(item);
            }
        }

        if (k >= 1f)
        {
            landed = false;
            target = (target + 1) % flips.Length;
            Hold(back, Working, Random.Range(pause.x, pause.y));
        }
    }

    /// <summary>The way up the item at <paramref name="i"/> lies: as built, or turned over end to end.</summary>
    Quaternion Face(int i, bool over) => over ? flipRestRotation[i] * Quaternion.AngleAxis(180f, Vector3.right) : flipRestRotation[i];

    /// <summary>A puff of steam and a spit of fat where a flipped item comes down.</summary>
    void Land(Transform item)
    {
        var at = new ParticleSystem.EmitParams { position = item.position + Vector3.up * 0.03f, applyShapeToPosition = true };
        if (steam != null) steam.Emit(at, 3);
        if (spatter != null) spatter.Emit(at, 6);
    }

    // --- Helpers ----------------------------------------------------------------------------------

    static void Show(Transform part, float shown, Vector3 scale)
    {
        if (part == null) return;
        bool visible = shown > 0.01f;
        if (part.gameObject.activeSelf != visible) part.gameObject.SetActive(visible);
        if (visible) part.localScale = scale;
    }

    static int Count(Transform[] parts) => parts != null ? parts.Length : 0;

    static Vector3[] Positions(Transform[] parts)
    {
        var rest = new Vector3[Count(parts)];
        for (int i = 0; i < rest.Length; i++) rest[i] = parts[i] != null ? parts[i].localPosition : Vector3.zero;
        return rest;
    }

    static Vector3[] Scales(Transform[] parts)
    {
        var rest = new Vector3[Count(parts)];
        for (int i = 0; i < rest.Length; i++) rest[i] = parts[i] != null ? parts[i].localScale : Vector3.one;
        return rest;
    }

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    /// <summary>Wires the parts in, for the builder that makes them.</summary>
    public void Configure(Transform[] friedEggs, Transform[] toTurn, Transform turner, GameObject pilotLamp, ParticleSystem plateSteam,
                          ParticleSystem fatSpatter)
    {
        eggs = friedEggs;
        flips = toTurn;
        spatula = turner;
        lamp = pilotLamp;
        steam = plateSteam;
        spatter = fatSpatter;
    }
}
