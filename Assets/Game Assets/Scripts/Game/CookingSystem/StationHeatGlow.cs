using UnityEngine;

/// <summary>
/// A station's heat showing while it cooks: glowing parts - the heat behind an oven's windows -
/// running from a banked glow up to hot and flickering, lamps that light while it works, and steam
/// or smoke given out in proportion. When the last order is done it all dies back down.
///
/// The glow's colour goes through a property block on each renderer, so the material asset is
/// never changed. "Busy" is the station having anything in progress
/// (<see cref="CookeryStation.recipiesInProgress"/>), eased in and out: an oven takes a while to
/// come up to heat and longer to cool.
/// </summary>
[DisallowMultipleComponent]
public sealed class StationHeatGlow : MonoBehaviour
{
    [Tooltip("The station whose cooking drives this. Found among the parents when left empty.")]
    [SerializeField] CookeryStation station;

    [Tooltip("Plays the heat with nothing cooking, for checking how it looks.")]
    [SerializeField] bool previewBusy;

    [Header("Glow")]
    [Tooltip("Unlit parts whose colour comes up with the heat.")]
    [SerializeField] Renderer[] glows;

    [SerializeField] Color banked = new Color(0.32f, 0.14f, 0.07f);
    [SerializeField] Color hot = new Color(1f, 0.6f, 0.28f);

    [Tooltip("How much the glow wavers, as a fraction, at full heat.")]
    [SerializeField, Range(0f, 0.3f)] float flicker = 0.06f;

    [Header("Lamps")]
    [Tooltip("Lit while the station is on.")]
    [SerializeField] GameObject[] lamps;

    [Header("Steam")]
    [Tooltip("Steam, smoke or heat given out, as many a second as Per Second at full heat.")]
    [SerializeField] ParticleSystem[] emitters;

    [SerializeField] float perSecond = 3f;

    [Header("Feel")]
    [SerializeField] float warmUpSeconds = 1.5f;
    [SerializeField] float coolDownSeconds = 3f;

    float heat;
    MaterialPropertyBlock block;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    /// <summary>Whether the station has anything cooking (or the preview is on).</summary>
    public bool Busy => previewBusy || (station != null && station.recipiesInProgress != null && station.recipiesInProgress.Count > 0);

    /// <summary>0 to 1: how far it has come up to heat.</summary>
    public float Heat => heat;

    void Awake()
    {
        if (station == null) station = GetComponentInParent<CookeryStation>();
        block = new MaterialPropertyBlock();
    }

    void OnEnable()
    {
        // Left running with nothing to give out: the rate is set here.
        if (emitters != null)
            foreach (ParticleSystem ps in emitters)
                if (ps != null) ps.Play(true);

        heat = 0f;
        Apply();
    }

    void Update()
    {
        float rate = Busy ? 1f / Mathf.Max(0.01f, warmUpSeconds) : -1f / Mathf.Max(0.01f, coolDownSeconds);
        heat = Mathf.Clamp01(heat + rate * Time.deltaTime);
        Apply();
    }

    void Apply()
    {
        float h = heat * heat * (3f - 2f * heat);
        float t = Time.time;
        float waver = 1f + flicker * h * (Mathf.Sin(t * 7.3f) * 0.6f + Mathf.Sin(t * 17.9f) * 0.4f);
        Color colour = Color.Lerp(banked, hot, h) * waver;
        colour.a = 1f;

        if (glows != null)
        {
            foreach (Renderer glow in glows)
            {
                if (glow == null) continue;
                glow.GetPropertyBlock(block);
                block.SetColor(BaseColor, colour);
                glow.SetPropertyBlock(block);
            }
        }

        bool lit = heat > 0.05f;
        if (lamps != null)
            foreach (GameObject lamp in lamps)
                if (lamp != null && lamp.activeSelf != lit) lamp.SetActive(lit);

        if (emitters != null)
        {
            foreach (ParticleSystem ps in emitters)
            {
                if (ps == null) continue;
                var emission = ps.emission;
                emission.rateOverTime = h > 0.05f ? perSecond * h : 0f;
            }
        }
    }

    /// <summary>Wires the parts in, for the builder that makes them.</summary>
    public void Configure(Renderer[] glowing, Color whenBanked, Color whenHot, GameObject[] lit, ParticleSystem[] giveOut, float rateAtFullHeat)
    {
        glows = glowing;
        banked = whenBanked;
        hot = whenHot;
        lamps = lit;
        emitters = giveOut;
        perSecond = rateAtFullHeat;
    }
}
