using LastOrders.Audio;
using UnityEngine;

/// <summary>
/// One tool at a kitchen station working while the station cooks, and laid down again when it is
/// done: a knife rocking through a tomato with the pieces hopping off onto a pile, a whisk beating
/// egg round a bowl, a rolling pin rolling dough out, a bread knife sawing a loaf, a ball of dough
/// being kneaded. It sits on the tool it moves, whose pivot is where the motion turns: a chopping
/// knife on the tip of its blade, a whisk on the middle of its bowl, a rolling pin and a bread knife
/// on their own middle, dough on its foot. What the tool works on can follow it - the dough
/// spreading under the pin, the egg swirling under the whisk - and a puff can go up each stroke.
///
/// "Busy" is the station having anything in progress (<see cref="CookeryStation.recipiesInProgress"/>),
/// eased in and out, so the work picks up and winds down. Everything is in the tool's parent's
/// space - the corner the station's builder made - as are the poses it starts from.
/// </summary>
[DisallowMultipleComponent]
public sealed class KitchenToolMotion : MonoBehaviour
{
    public enum Motion { Chop, Whisk, Roll, Saw, Knead }

    [Tooltip("The station whose work drives this. Found among the parents when left empty.")]
    [SerializeField] CookeryStation station;

    [Tooltip("Works with nothing being made, for checking how it looks.")]
    [SerializeField] bool previewBusy;

    [SerializeField] Motion motion;

    [Tooltip("Strokes a second at full work: chops, turns of the whisk, passes of the pin or the knife, kneads.")]
    [SerializeField] float rate = 3f;

    [Tooltip("Chop: degrees the handle lifts. Whisk: unused. Roll, Saw: metres either way along Axis. Knead: how far the dough squashes.")]
    [SerializeField] float amount = 24f;

    [Tooltip("In the parent's space. Chop: the hinge the blade rocks about. Whisk: the axis it goes round. Roll, Saw: the way it travels.")]
    [SerializeField] Vector3 axis = Vector3.right;

    [Tooltip("Roll: the pin's radius, for how far it turns as it travels.")]
    [SerializeField] float radius = 0.03f;

    [Header("Worked on")]
    [Tooltip("Roll: the dough, stretched the way the pin travels as the work goes on. Whisk: what is beaten, turned round after the whisk.")]
    [SerializeField] Transform follower;

    [Tooltip("Roll: how much longer the dough is rolled out at full work, as a fraction.")]
    [SerializeField] float stretch = 0.25f;

    [Header("Bits")]
    [Tooltip("Chop: pieces hopping off the blade onto the pile, one a stroke.")]
    [SerializeField] Transform[] bits;

    [Tooltip("Where a piece comes off the blade, in the parent's space.")]
    [SerializeField] Vector3 bitFrom;

    [Tooltip("The middle of the pile the pieces land on, in the parent's space.")]
    [SerializeField] Vector3 bitPile;

    [SerializeField] float pileSpread = 0.035f;

    [Tooltip("A puff or two given out each stroke: flour off the pin, a fleck off the whisk.")]
    [SerializeField] ParticleSystem puff;

    [Header("Feel")]
    [SerializeField] float warmUpSeconds = 0.7f;
    [SerializeField] float coolDownSeconds = 1.2f;

    float work, stroke;
    Vector3 restPosition, restScale, followerScale;
    Quaternion restRotation, followerRotation;
    Bit[] flights;
    int nextBit;

    struct Bit
    {
        public Vector3 From, To;
        public float Clock, Flight, Rest, Height;
        public Vector3 Spin;
        public Quaternion Start;
        public bool Live;
    }

    /// <summary>Whether the station has anything in progress (or the preview is on).</summary>
    public bool Busy => previewBusy || (station != null && station.recipiesInProgress != null && station.recipiesInProgress.Count > 0);

    /// <summary>0 to 1: how far the tool has picked up to full work.</summary>
    public float Work => work;

    void Awake()
    {
        if (station == null) station = GetComponentInParent<CookeryStation>();
        restPosition = transform.localPosition;
        restRotation = transform.localRotation;
        restScale = transform.localScale;

        if (follower != null)
        {
            followerScale = follower.localScale;
            followerRotation = follower.localRotation;
        }

        flights = new Bit[bits != null ? bits.Length : 0];
    }

    void OnEnable()
    {
        if (bits != null)
            foreach (Transform bit in bits)
                if (bit != null) bit.gameObject.SetActive(false);
        if (flights != null) for (int i = 0; i < flights.Length; i++) flights[i].Live = false;

        work = 0f;
        Apply(0f);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float rate01 = Busy ? 1f / Mathf.Max(0.01f, warmUpSeconds) : -1f / Mathf.Max(0.01f, coolDownSeconds);
        work = Mathf.Clamp01(work + rate01 * dt);
        Apply(dt);
    }

    void Apply(float dt)
    {
        float w = work * work * (3f - 2f * work);
        float before = stroke;

        // Running down, the tool finishes the stroke it is on and stops at its start.
        if (w > 0.01f) stroke += rate * dt * Mathf.Lerp(0.5f, 1f, w);
        else stroke = Mathf.Ceil(stroke);

        float c = stroke - Mathf.Floor(stroke);
        bool struck = Mathf.Floor(stroke) > Mathf.Floor(before);

        switch (motion)
        {
            case Motion.Chop: Chop(c, w, struck); break;
            case Motion.Whisk: Whisk(w); break;
            case Motion.Roll: Roll(c, w, before); break;
            case Motion.Saw: Saw(c, w); break;
            case Motion.Knead: Knead(c, w, struck); break;
        }

        // Every stroke is heard at the tool, while the station is working in earnest.
        if (struck && w > 0.3f) SoundController.Play(StrokeSound, transform.position);

        Fly(dt);
    }

    SoundId StrokeSound
    {
        get
        {
            switch (motion)
            {
                case Motion.Chop: return SoundId.ToolChop;
                case Motion.Whisk: return SoundId.ToolWhisk;
                case Motion.Roll: return SoundId.ToolRoll;
                case Motion.Saw: return SoundId.ToolSaw;
                default: return SoundId.ToolKnead;
            }
        }
    }

    // --- The motions ------------------------------------------------------------------------------

    /// <summary>The knife rocking on its tip: the handle lifts easing to the top, then comes down hard; a piece hops off each time it lands.</summary>
    void Chop(float c, float w, bool struck)
    {
        float lift = c < 0.65f ? 1f - (1f - c / 0.65f) * (1f - c / 0.65f) : 1f - Mathf.Pow((c - 0.65f) / 0.35f, 2f);
        transform.localRotation = Quaternion.AngleAxis(amount * lift * w, axis) * restRotation;
        if (struck && w > 0.3f)
        {
            Launch();
            Puff();
        }
    }

    /// <summary>The whisk going round the bowl, a little bob as it goes; what is beaten swirls after it.</summary>
    void Whisk(float w)
    {
        float turned = stroke * 360f;
        Vector3 up = axis.sqrMagnitude > 0f ? axis.normalized : Vector3.up;
        transform.localRotation = Quaternion.AngleAxis(turned, up) * restRotation;
        transform.localPosition = restPosition + up * (0.008f * w * Mathf.Sin(stroke * Mathf.PI * 4f));
        if (follower != null) follower.localRotation = Quaternion.AngleAxis(turned * 0.4f, up) * followerRotation;
    }

    /// <summary>The pin rolled out and back along its way, turning as it goes; the dough under it lengthens as the work goes on, a puff of flour at each end.</summary>
    void Roll(float c, float w, float before)
    {
        Vector3 travel = axis.sqrMagnitude > 0f ? axis.normalized : Vector3.forward;
        float along = amount * w * Mathf.Sin(c * Mathf.PI * 2f);
        transform.localPosition = restPosition + travel * along;

        Vector3 pin = Vector3.Cross(Vector3.up, travel).normalized;
        float turned = -along / Mathf.Max(0.005f, radius) * Mathf.Rad2Deg;
        transform.localRotation = Quaternion.AngleAxis(turned, pin) * restRotation;

        if (follower != null)
        {
            Vector3 local = Quaternion.Inverse(follower.localRotation) * travel;
            float grow = 1f + stretch * w;
            follower.localScale = new Vector3(
                followerScale.x * Mathf.Lerp(1f, grow, Mathf.Abs(local.x)),
                followerScale.y * Mathf.Lerp(1f, 1f / grow, 0.5f),
                followerScale.z * Mathf.Lerp(1f, grow, Mathf.Abs(local.z)));
        }

        // At each end of a pass, where the pin turns back: a quarter and three quarters through a stroke.
        if (w > 0.3f && Mathf.Floor(stroke * 2f - 0.5f) > Mathf.Floor(before * 2f - 0.5f)) Puff();
    }

    /// <summary>The knife sawing back and forth along its blade, dipping a little into the cut on each push.</summary>
    void Saw(float c, float w)
    {
        Vector3 travel = axis.sqrMagnitude > 0f ? axis.normalized : Vector3.right;
        float s = Mathf.Sin(c * Mathf.PI * 2f);
        transform.localPosition = restPosition + travel * (amount * w * s) + Vector3.down * (0.006f * w * Mathf.Max(0f, s));
    }

    /// <summary>The dough pressed down and spread, let up, and given a quarter turn each time.</summary>
    void Knead(float c, float w, bool struck)
    {
        float press = Mathf.Max(0f, Mathf.Sin(c * Mathf.PI * 2f)) * w;
        transform.localScale = new Vector3(restScale.x * (1f + amount * 0.6f * press), restScale.y * (1f - amount * press),
                                           restScale.z * (1f + amount * 0.6f * press));
        float turned = (Mathf.Floor(stroke) + Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, c))) * 90f;
        transform.localRotation = Quaternion.AngleAxis(turned, Vector3.up) * restRotation;
        if (struck && w > 0.3f) Puff();
    }

    // --- Pieces and puffs -------------------------------------------------------------------------

    void Launch()
    {
        if (flights == null || flights.Length == 0) return;

        int i = nextBit;
        nextBit = (nextBit + 1) % flights.Length;
        Vector2 spot = Random.insideUnitCircle * pileSpread;

        flights[i] = new Bit
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

    void Puff()
    {
        if (puff != null) puff.Emit(Random.Range(1, 3));
    }

    /// <summary>Moves every piece along its hop, tumbling, lets it lie on the pile a while, then shrinks it away.</summary>
    void Fly(float dt)
    {
        if (bits == null) return;

        for (int i = 0; i < flights.Length; i++)
        {
            Transform part = bits[i];
            if (part == null) continue;

            ref Bit b = ref flights[i];
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
            part.localRotation = b.Start;

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

    // --- Set up by the builders -------------------------------------------------------------------

    /// <summary>What the tool does, how fast, how far, and about or along what - for the builder that makes it.</summary>
    public KitchenToolMotion Configure(Motion how, float strokesPerSecond, float howFar, Vector3 aboutOrAlong)
    {
        motion = how;
        rate = strokesPerSecond;
        amount = howFar;
        axis = aboutOrAlong;
        return this;
    }

    /// <summary>The pin's radius, so it turns as far as it rolls.</summary>
    public KitchenToolMotion Rolls(float pinRadius)
    {
        radius = pinRadius;
        return this;
    }

    /// <summary>What the tool works on, and how much it is rolled out.</summary>
    public KitchenToolMotion Works(Transform on, float rolledOut = 0.25f)
    {
        follower = on;
        stretch = rolledOut;
        return this;
    }

    /// <summary>The pieces it cuts, where they come off and the pile they land on.</summary>
    public KitchenToolMotion Cuts(Transform[] pieces, Vector3 from, Vector3 pile, float spread = 0.035f)
    {
        bits = pieces;
        bitFrom = from;
        bitPile = pile;
        pileSpread = spread;
        return this;
    }

    public KitchenToolMotion Puffs(ParticleSystem each)
    {
        puff = each;
        return this;
    }
}
