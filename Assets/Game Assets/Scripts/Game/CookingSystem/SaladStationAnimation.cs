using LastOrders.Audio;
using UnityEngine;

/// <summary>
/// Brings the salad corner to life while its station is making something: the knife rocks up and
/// down through a carrot on the board, the rounds it cuts hopping off onto a pile; the servers dig
/// into the big bowl and toss the salad, leaves flipping up out of it and falling back; and every
/// few seconds the dressing cruet lifts, tips over the bowl and pours. When the last order is done
/// it all settles: the knife stops on the board, the servers rest in the bowl, the cruet stands.
///
/// "Busy" is the station having anything in progress (<see cref="CookeryStation.recipiesInProgress"/>),
/// so it follows the station however a dish was started. The change is eased in and out rather
/// than switched, so the work picks up and winds down.
///
/// The parts are built by the Last Orders / Kitchen salad corner builder; this only moves them.
/// Every part lives in the same space (the corner's), so the poses below are in that space.
/// </summary>
[DisallowMultipleComponent]
public sealed class SaladStationAnimation : MonoBehaviour
{
    [Tooltip("The station whose work drives this. Found among the parents when left empty.")]
    [SerializeField] CookeryStation station;

    [Tooltip("Plays the busy animation with nothing being made, for checking how it looks.")]
    [SerializeField] bool previewBusy;

    [Header("Chopping")]
    [Tooltip("Pivots on the tip of its blade, on the board: turning it about its X axis lifts the handle.")]
    [SerializeField] Transform knife;

    [Tooltip("Carrot rounds hopping off the blade onto the pile, one a chop.")]
    [SerializeField] Transform[] bits;

    [Tooltip("Where a round comes off the blade, in the corner's space.")]
    [SerializeField] Vector3 bitFrom;

    [Tooltip("The middle of the pile the rounds land on, in the corner's space.")]
    [SerializeField] Vector3 bitPile;

    [SerializeField] float pileSpread = 0.05f;

    [Header("Tossing")]
    [Tooltip("The heap of salad in the bowl, its origin in the middle of the bowl.")]
    [SerializeField] Transform salad;

    [Tooltip("The salad servers, pivoting on the middle of the bowl.")]
    [SerializeField] Transform servers;

    [Tooltip("Leaves thrown up out of the bowl as it is tossed.")]
    [SerializeField] Transform[] leaves;

    [Header("Dressing")]
    [SerializeField] Transform cruet;

    [Tooltip("Where the cruet is held to pour, over the bowl.")]
    [SerializeField] Transform cruetPour;

    [Tooltip("The dressing running from the cruet's mouth into the bowl, drawn while it pours.")]
    [SerializeField] Transform stream;

    [Header("Feel")]
    [SerializeField] float warmUpSeconds = 0.6f;
    [SerializeField] float coolDownSeconds = 1.2f;
    [SerializeField] float chopsPerSecond = 3.2f;
    [SerializeField] float tossSeconds = 1.3f;

    [Tooltip("How far the handle lifts on each chop, in degrees.")]
    [SerializeField] float chopLift = 24f;

    [Tooltip("Seconds between pours of dressing, at random between the two.")]
    [SerializeField] Vector2 pourEvery = new Vector2(3.5f, 6f);

    float work;
    float chop, toss;
    Quaternion knifeRest;
    Vector3 saladRest, serversRest;
    Quaternion saladRestRotation, serversRestRotation;
    Vector3 cruetRest;
    Quaternion cruetRestRotation;
    Vector3 streamRest;
    Bit[] bitStates;
    Bit[] leafStates;
    int nextBit, nextLeaf;
    float pourWait, pourClock = -1f;

    /// <summary>A piece in flight: where it set off and where it lands, how long it has been going, how it tumbles.</summary>
    struct Bit
    {
        public Vector3 From, To;
        public float Clock, Flight, Rest, Height;
        public Vector3 Spin;
        public Quaternion Start;
        public bool Live;
    }

    const float PourSeconds = 2.4f, PourLift = 0.45f, PourBack = 1.8f;

    /// <summary>Whether the station has anything in progress (or the preview is on).</summary>
    public bool Busy => previewBusy || (station != null && station.recipiesInProgress != null && station.recipiesInProgress.Count > 0);

    /// <summary>0 to 1: how far the corner has picked up to full work.</summary>
    public float Work => work;

    void Awake()
    {
        if (station == null) station = GetComponentInParent<CookeryStation>();

        if (knife != null) knifeRest = knife.localRotation;

        if (salad != null)
        {
            saladRest = salad.localPosition;
            saladRestRotation = salad.localRotation;
        }

        if (servers != null)
        {
            serversRest = servers.localPosition;
            serversRestRotation = servers.localRotation;
        }

        if (cruet != null)
        {
            cruetRest = cruet.localPosition;
            cruetRestRotation = cruet.localRotation;
        }

        if (stream != null) streamRest = stream.localScale;

        bitStates = new Bit[bits != null ? bits.Length : 0];
        leafStates = new Bit[leaves != null ? leaves.Length : 0];
        pourWait = Random.Range(1f, pourEvery.y);
    }

    void OnEnable()
    {
        HideAll(bits);
        HideAll(leaves);
        if (stream != null) stream.gameObject.SetActive(false);
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
        float w = work * work * (3f - 2f * work);
        Chop(w, dt);
        Toss(w, dt);
        Pour(w, dt);
        Fly(bits, bitStates, dt, true);
        Fly(leaves, leafStates, dt, false);
    }

    // --- Chopping ----------------------------------------------------------------------------------

    /// <summary>
    /// The knife rocking on its tip: the handle lifts quickly and comes down hard. Each time the
    /// blade comes down a round of carrot hops off it onto the pile. Running down, the knife
    /// finishes its stroke and stays down.
    /// </summary>
    void Chop(float w, float dt)
    {
        if (knife == null) return;

        float before = chop;
        if (w > 0.01f) chop += chopsPerSecond * dt * Mathf.Lerp(0.5f, 1f, w);
        else chop = Mathf.Ceil(chop);

        // Up over the first two thirds of a stroke, easing to the top; down in the last third, falling faster.
        float c = chop - Mathf.Floor(chop);
        float lift = c < 0.65f ? 1f - (1f - c / 0.65f) * (1f - c / 0.65f) : 1f - Mathf.Pow((c - 0.65f) / 0.35f, 2f);
        knife.localRotation = Quaternion.AngleAxis(chopLift * lift * w, Vector3.right) * knifeRest;

        if (Mathf.Floor(chop) > Mathf.Floor(before) && w > 0.3f)
        {
            LaunchBit();
            SoundController.Play(SoundId.ToolChop, transform.position);
        }
    }

    void LaunchBit()
    {
        if (bitStates.Length == 0) return;

        int i = nextBit;
        nextBit = (nextBit + 1) % bitStates.Length;
        Vector2 spot = Random.insideUnitCircle * pileSpread;

        bitStates[i] = new Bit
        {
            From = bitFrom,
            To = bitPile + new Vector3(spot.x, 0f, spot.y),
            Clock = 0f,
            Flight = Random.Range(0.25f, 0.34f),
            Rest = 1.1f,
            Height = Random.Range(0.035f, 0.06f),
            Spin = new Vector3(Random.Range(-540f, 540f), Random.Range(-180f, 180f), Random.Range(-540f, 540f)),
            Start = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
            Live = true,
        };
    }

    // --- Tossing ----------------------------------------------------------------------------------

    /// <summary>
    /// The servers dig into the salad, lift and turn it, and let it fall: the heap rises and turns
    /// a little with them, and two leaves are thrown up out of it each time.
    /// </summary>
    void Toss(float w, float dt)
    {
        float before = toss;
        if (w > 0.01f) toss += dt / Mathf.Max(0.1f, tossSeconds) * Mathf.Lerp(0.6f, 1f, w);
        float t = toss - Mathf.Floor(toss);

        // Dig in (0 - 0.35), lift (0.35 - 0.6), settle (0.6 - 1).
        float dig = t < 0.35f ? Mathf.Sin(Mathf.PI * t / 0.35f * 0.5f) : t < 0.6f ? 1f - (t - 0.35f) / 0.25f : 0f;
        float lift = t > 0.3f && t < 0.75f ? Mathf.Sin(Mathf.PI * (t - 0.3f) / 0.45f) : 0f;
        dig *= w;
        lift *= w;

        if (servers != null)
        {
            Quaternion swing = Quaternion.Euler(-12f * dig + 16f * lift, 10f * Mathf.Sin(toss * Mathf.PI), 0f);
            servers.localRotation = swing * serversRestRotation;
            servers.localPosition = serversRest + Vector3.up * (0.05f * lift - 0.025f * dig);
        }

        if (salad != null)
        {
            // Turned a little further each toss, so the salad comes round.
            float turned = (Mathf.Floor(toss) + Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.35f, 0.75f, t))) * 23f;
            salad.localRotation = Quaternion.AngleAxis(turned, Vector3.up) * saladRestRotation;
            salad.localPosition = saladRest + Vector3.up * (0.03f * lift);
            salad.localScale = new Vector3(1f - 0.03f * lift, 1f + 0.1f * lift, 1f - 0.03f * lift);
        }

        // At the top of each lift, a couple of leaves fly.
        if (w > 0.3f && Mathf.Floor(toss - 0.45f) > Mathf.Floor(before - 0.45f))
        {
            LaunchLeaf();
            LaunchLeaf();
            SoundController.Play(SoundId.SaladToss, transform.position);
        }
    }

    void LaunchLeaf()
    {
        if (leafStates.Length == 0) return;

        int i = nextLeaf;
        nextLeaf = (nextLeaf + 1) % leafStates.Length;
        Vector2 from = Random.insideUnitCircle * 0.1f;
        Vector2 to = Random.insideUnitCircle * 0.12f;

        leafStates[i] = new Bit
        {
            From = saladRest + new Vector3(from.x, 0.05f, from.y),
            To = saladRest + new Vector3(to.x, 0.04f, to.y),
            Clock = 0f,
            Flight = Random.Range(0.5f, 0.65f),
            Rest = 0f,
            Height = Random.Range(0.14f, 0.22f),
            Spin = new Vector3(Random.Range(-500f, 500f), Random.Range(-300f, 300f), Random.Range(-500f, 500f)),
            Start = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
            Live = true,
        };
    }

    // --- Dressing ---------------------------------------------------------------------------------

    /// <summary>Now and then, while the work is in full swing, the cruet lifts, tips over the bowl, pours and goes back.</summary>
    void Pour(float w, float dt)
    {
        if (cruet == null || cruetPour == null) return;

        if (pourClock < 0f)
        {
            cruet.localPosition = cruetRest;
            cruet.localRotation = cruetRestRotation;
            if (w < 0.9f) return;

            pourWait -= dt;
            if (pourWait > 0f) return;
            pourClock = 0f;
            SoundController.Play(SoundId.DressingPour, cruet.position);
        }

        pourClock += dt;
        float k;
        if (pourClock < PourLift) k = Smooth(pourClock / PourLift);
        else if (pourClock < PourBack) k = 1f;
        else k = 1f - Smooth((pourClock - PourBack) / (PourSeconds - PourBack));

        // Lifted clear of the worktop on the way, not slid across it.
        Vector3 arc = Vector3.up * (0.08f * Mathf.Sin(Mathf.PI * k));
        cruet.localPosition = Vector3.Lerp(cruetRest, cruetPour.localPosition, k) + arc * (1f - k);
        cruet.localRotation = Quaternion.Slerp(cruetRestRotation, cruetPour.localRotation, k);

        if (stream != null)
        {
            bool pouring = pourClock > PourLift + 0.1f && pourClock < PourBack - 0.05f;
            if (stream.gameObject.activeSelf != pouring) stream.gameObject.SetActive(pouring);
            if (pouring)
            {
                float grow = Mathf.Clamp01((pourClock - PourLift - 0.1f) / 0.12f);
                float wobble = 1f + 0.12f * Mathf.Sin(Time.time * 37f);
                stream.localScale = new Vector3(streamRest.x * wobble, streamRest.y * grow, streamRest.z * wobble);
            }
        }

        if (pourClock >= PourSeconds)
        {
            pourClock = -1f;
            pourWait = Random.Range(pourEvery.x, pourEvery.y);
            cruet.localPosition = cruetRest;
            cruet.localRotation = cruetRestRotation;
        }
    }

    // --- Pieces in flight -------------------------------------------------------------------------

    /// <summary>
    /// Moves every live piece along its hop, tumbling, then lets it lie for its rest time. A carrot
    /// round settles flat on the pile and shrinks away; a leaf drops back into the salad and goes.
    /// </summary>
    void Fly(Transform[] parts, Bit[] states, float dt, bool settleFlat)
    {
        if (parts == null) return;

        for (int i = 0; i < states.Length; i++)
        {
            Transform part = parts[i];
            if (part == null) continue;

            ref Bit b = ref states[i];
            if (!b.Live)
            {
                if (part.gameObject.activeSelf) part.gameObject.SetActive(false);
                continue;
            }

            b.Clock += dt;
            if (!part.gameObject.activeSelf) part.gameObject.SetActive(true);

            if (b.Clock < b.Flight)
            {
                float k = b.Clock / b.Flight;
                part.localPosition = Vector3.Lerp(b.From, b.To, k) + Vector3.up * (4f * b.Height * k * (1f - k));
                part.localRotation = Quaternion.Euler(b.Spin * b.Clock) * b.Start;
                part.localScale = Vector3.one;
                continue;
            }

            float lying = b.Clock - b.Flight;
            part.localPosition = b.To;
            if (settleFlat) part.localRotation = b.Start;

            const float shrink = 0.25f;
            float size = lying < b.Rest ? 1f : 1f - (lying - b.Rest) / shrink;
            if (size <= 0f)
            {
                b.Live = false;
                part.gameObject.SetActive(false);
                continue;
            }

            part.localScale = Vector3.one * size;
        }
    }

    static void HideAll(Transform[] parts)
    {
        if (parts == null) return;
        foreach (Transform part in parts)
            if (part != null) part.gameObject.SetActive(false);
    }

    static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    /// <summary>Wires the parts in, for the builder that makes them.</summary>
    public void Configure(Transform chopKnife, Transform[] carrotRounds, Vector3 roundsFrom, Vector3 roundsPile, Transform saladHeap,
                          Transform saladServers, Transform[] tossedLeaves, Transform dressing, Transform dressingPour, Transform dressingStream)
    {
        knife = chopKnife;
        bits = carrotRounds;
        bitFrom = roundsFrom;
        bitPile = roundsPile;
        salad = saladHeap;
        servers = saladServers;
        leaves = tossedLeaves;
        cruet = dressing;
        cruetPour = dressingPour;
        stream = dressingStream;
    }
}
