using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An item just made - a crop picked, a dish cooked - shown out in the level as its icon. It pops out
/// of where it was made with a blink, lands close by with a squash and a little hop, glows a moment,
/// then flies into the storage (the zone's inventory), which bounces as it takes it - or, when the
/// storage is off screen or far away, floats up and away. Either way it ends in a twinkle.
///
/// The item goes into the inventory the moment it is made; the pickup is only the show. Several made
/// together (<see cref="Initialize(Item, InventorySystem, int, int)"/>) fan out toward the camera so
/// none hides behind the others or behind what made them.
/// </summary>
public class ItemReceiver : MonoBehaviour/*,IInteractableObject*/
{
    [Tooltip("The item's icon. Sized to Icon Size, standing on the body's origin, turned to face the camera.")]
    [SerializeField] SpriteRenderer itemImage;

    [Tooltip("What moves along the pickup's path and faces the camera, carrying the icon and its glow. " +
             "This object itself when not set.")]
    [SerializeField] Transform body;

    [Tooltip("A soft glow behind the icon, turning slowly. Optional.")]
    [SerializeField] SpriteRenderer glow;

    [Tooltip("A soft blob kept on the ground under the pickup. Optional.")]
    [SerializeField] Transform shadow;

    [Tooltip("Played where the pickup is collected, then removed. Optional.")]
    [SerializeField] GameObject collectBurst;

    [Header("Size")]
    [Tooltip("How big the icon is drawn across its longer side, in metres.")]
    [SerializeField] float iconSize = 0.7f;

    [Tooltip("How wide the glow is, against the icon.")]
    [SerializeField] float glowSize = 1.55f;

    [Tooltip("How wide the shadow is, against the icon.")]
    [SerializeField] float shadowSize = 0.6f;

    [Tooltip("The least share of the screen's height the icon takes: with the camera zoomed out, the pickup " +
             "and its throw grow to keep it this big.")]
    [SerializeField] float minScreenHeight = 0.085f;

    [Header("Motion")]
    [SerializeField] float throwDistance = 0.75f;
    [SerializeField] float throwHeight = 0.8f;
    [SerializeField] float throwTime = 0.5f;
    [SerializeField] float restTime = 0.35f;
    [SerializeField] float flyTime = 0.6f;

    [Tooltip("Seconds between one pickup of a batch setting off and the next.")]
    [SerializeField] float stagger = 0.08f;

    [Tooltip("Pickups farther than this from the storage float away instead of flying into it.")]
    [SerializeField] float storageRange = 24f;

    [Tooltip("What a pickup can land on.")]
    [SerializeField] LayerMask groundLayers = 1;

    /// <summary>The plant beds' soil, above the foot of a pot's tap box (SoilBed.Crown).</summary>
    const float BedSoil = 0.12f;

    const float LandTime = 0.42f, HopStart = 0.05f, HopTime = 0.26f, HopHeight = 0.14f;
    const float WindUp = 0.18f;
    static readonly Color GlowColour = new Color(1f, 0.85f, 0.45f);
    static readonly int FlashId = Shader.PropertyToID("_Flash");

    static InventoryObject foundStorage;
    static readonly Dictionary<Transform, Vector3> restScales = new();

    Camera view;
    MaterialPropertyBlock block;
    Vector3 from, land, restPoint, flyTarget, position;
    float groundY, time, roll, iconHeight, glowSpin, zoom = 1f;
    Transform storage;
    bool launched, collecting, intoStorage;

    Transform Body => body != null ? body : transform;

    public void Initialize(Item item, InventorySystem inventory) => Initialize(item, inventory, 0, 1);

    /// <summary>
    /// Puts <paramref name="item"/> into <paramref name="inventory"/> and throws its icon out from
    /// here: pickup <paramref name="index"/> of <paramref name="count"/> made at once.
    /// </summary>
    public void Initialize(Item item, InventorySystem inventory, int index, int count)
    {
        inventory.AddItem(item);
        Show(item.itemDataSO.itemImage);
        Launch(transform.position, index, Mathf.Max(1, count));
    }

    void Show(Sprite sprite)
    {
        if (itemImage == null) return;

        itemImage.sprite = sprite;
        // The first pickup drew its icon sliced into a 1 m square, squashing every icon that was not square.
        itemImage.drawMode = SpriteDrawMode.Simple;
        if (sprite == null) return;

        Bounds bounds = sprite.bounds;
        float scale = iconSize / Mathf.Max(bounds.size.x, bounds.size.y, 1e-4f);
        iconHeight = bounds.size.y * scale;

        Transform icon = itemImage.transform;
        if (icon != Body)
        {
            // Standing on the body's origin, so the body squashes from the ground up.
            icon.SetLocalPositionAndRotation(new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, 0f), Quaternion.identity);
            icon.localScale = Vector3.one * scale;
        }

        // Behind the icon's middle; Pose sizes it.
        if (glow != null)
            glow.transform.localPosition = new Vector3(0f, iconHeight * 0.5f, 0.02f);
    }

    void Launch(Vector3 origin, int index, int count)
    {
        view = Camera.main;
        from = origin;
        zoom = ZoomScale(origin);

        // Out toward the camera, a batch fanned across that way.
        Vector3 toCamera = view != null ? view.transform.position - origin : Vector3.back;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude < 1e-4f) toCamera = Vector3.back;
        float fan = count > 1 ? Mathf.Lerp(-55f, 55f, index / (float)(count - 1)) : 0f;
        Vector3 direction = Quaternion.Euler(0f, fan + Random.Range(-18f, 18f), 0f) * toCamera.normalized;

        land = origin + direction * (throwDistance * zoom * Random.Range(0.85f, 1.1f));
        groundY = GroundUnder(land, origin.y);
        land.y = groundY;
        roll = Random.Range(18f, 30f) * (Random.value < 0.5f ? -1f : 1f);

        time = -index * stagger;
        position = from;
        launched = true;
        Pose(0f, Vector2.one, 0f, 0f, 1f);
    }

    /// <summary>How much bigger than Icon Size the pickup must be drawn at <paramref name="at"/> to fill Min Screen Height.</summary>
    float ZoomScale(Vector3 at)
    {
        if (view == null || iconSize <= 0f) return 1f;

        float screen = view.orthographic
            ? view.orthographicSize * 2f
            : 2f * Mathf.Max(0.1f, Vector3.Dot(at - view.transform.position, view.transform.forward)) * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return Mathf.Max(1f, minScreenHeight * screen / iconSize);
    }

    /// <summary>The top of what is under <paramref name="point"/>: the ground, a floor, a bed's soil.</summary>
    float GroundUnder(Vector3 point, float fromY)
    {
        float best = float.NegativeInfinity;
        foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(point.x, fromY + 0.3f, point.z), Vector3.down, 6f, groundLayers,
                                                      QueryTriggerInteraction.Ignore))
        {
            // Not on anyone's head.
            if (hit.collider.attachedRigidbody != null) continue;

            // A pot's tap box stands well above its soil.
            float y = hit.collider.GetComponentInParent<PlantPot>() != null ? hit.collider.bounds.min.y + BedSoil : hit.point.y;
            best = Mathf.Max(best, y);
        }

        return float.IsNegativeInfinity(best) ? fromY - 0.8f : best;
    }

    void LateUpdate()
    {
        if (!launched) return;

        time += Time.deltaTime;
        if (view == null) view = Camera.main;

        float landAt = throwTime, restAt = landAt + LandTime, collectAt = restAt + restTime;

        if (time < 0f)
        {
            Pose(0f, Vector2.one, 0f, 0f, 1f);
        }
        else if (time < landAt)
        {
            // Thrown: out along a high arc, popping up to size with a blink, turning back upright.
            float x = time / throwTime;
            position = Vector3.Lerp(from, land, x) + Vector3.up * (throwHeight * zoom * 4f * x * (1f - x));
            float grow = OutBack(Mathf.Clamp01(time / 0.22f));
            Pose(grow, Vector2.one, roll * (1f - x) * (1f - x), Mathf.Clamp01(1f - time / 0.18f), Mathf.Lerp(0.5f, 0.8f, x));
        }
        else if (time < restAt)
        {
            // Landed: a squash that springs back, a little hop, and a smaller squash as it comes down again.
            float t = time - landAt;
            float hop = t > HopStart && t < HopStart + HopTime ? Mathf.Sin(Mathf.PI * (t - HopStart) / HopTime) : 0f;
            position = land + Vector3.up * (HopHeight * zoom * hop);
            float squash = 0.26f * Mathf.Exp(-t * 10f) * Mathf.Cos(t * 30f);
            float after = t - HopStart - HopTime;
            if (after > 0f) squash += 0.12f * Mathf.Exp(-after * 12f) * Mathf.Cos(after * 30f);
            Pose(1f, new Vector2(1f + squash, 1f - squash), 0f, 0f, 1f + 0.35f * Mathf.Exp(-t * 6f));
        }
        else if (time < collectAt)
        {
            float t = time - restAt;
            position = land + Vector3.up * (0.035f * zoom * Mathf.Sin(t * 9f));
            Pose(1f, Vector2.one, 0f, 0f, 1f);
        }
        else
        {
            Collect((time - collectAt) / flyTime);
        }
    }

    void Collect(float x)
    {
        if (!collecting)
        {
            collecting = true;
            restPoint = position;
            intoStorage = FindStorage(out storage, out flyTarget);
        }

        if (x >= 1f)
        {
            Finish();
            return;
        }

        // A wind-up - a squash where it sits - then away, faster and faster.
        if (x < WindUp)
        {
            float k = Mathf.Sin(Mathf.PI * x / WindUp);
            Pose(1f, new Vector2(1f + 0.14f * k, 1f - 0.14f * k), 0f, 0f, 1f);
            return;
        }

        float f = (x - WindUp) / (1f - WindUp);
        float e = f * f;

        if (intoStorage)
        {
            Vector3 control = (restPoint + flyTarget) * 0.5f + Vector3.up * (1f + 0.12f * Vector3.Distance(restPoint, flyTarget));
            position = Vector3.Lerp(Vector3.Lerp(restPoint, control, e), Vector3.Lerp(control, flyTarget, e), e);
            float stretch = 0.12f * Mathf.Sin(Mathf.PI * f);
            Pose(Mathf.Lerp(1f, 0.45f, e), new Vector2(1f - stretch, 1f + stretch), 0f, 0f, 1f - f);
        }
        else
        {
            position = restPoint + Vector3.up * (1.6f * zoom * e);
            Pose((1f - e) * (1f + 0.6f * f), Vector2.one, 0f, 0f, 1f - f);
        }
    }

    void Finish()
    {
        launched = false;
        Vector3 at = intoStorage ? flyTarget : position + Vector3.up * (iconHeight * zoom * 0.5f);

        if (collectBurst != null)
        {
            // Brought toward the camera, to the same spot on screen: the storage's middle is inside its walls.
            if (view != null) at += (view.transform.position - at).normalized * (intoStorage ? 2f : 0.5f);
            GameObject burst = Instantiate(collectBurst, at, Quaternion.identity);
            foreach (ParticleSystem ps in burst.GetComponentsInChildren<ParticleSystem>()) ps.Play();
            Destroy(burst, 2.5f);
        }

        if (intoStorage && storage != null) Punch(storage);
        Destroy(gameObject);
    }

    /// <summary>
    /// Places the body at the current position, <paramref name="grow"/> of full size, squashed by
    /// <paramref name="squash"/> (across, up) and rolled <paramref name="degrees"/> while it faces the
    /// camera; whitened by <paramref name="flash"/>, its glow at <paramref name="glowAmount"/>.
    /// </summary>
    void Pose(float grow, Vector2 squash, float degrees, float flash, float glowAmount)
    {
        Transform b = Body;
        b.position = position;
        if (view != null) b.rotation = view.transform.rotation * Quaternion.Euler(0f, 0f, degrees);
        b.localScale = new Vector3(squash.x, squash.y, 1f) * (grow * zoom);

        if (itemImage != null)
        {
            block ??= new MaterialPropertyBlock();
            itemImage.GetPropertyBlock(block);
            block.SetFloat(FlashId, flash);
            itemImage.SetPropertyBlock(block);
        }

        if (glow != null)
        {
            glowSpin += Time.deltaTime * 35f;
            glow.transform.localRotation = Quaternion.Euler(0f, 0f, glowSpin);
            Color c = GlowColour;
            c.a = Mathf.Clamp01(glowAmount) * 0.85f;
            glow.color = c;
            float swell = Mathf.Max(1f, glowAmount);
            glow.transform.localScale = Vector3.one * (iconSize * glowSize * swell / Mathf.Max(glow.sprite != null ? glow.sprite.bounds.size.x : 1f, 1e-4f));
        }

        if (shadow != null)
        {
            float height = Mathf.Max(0f, position.y - groundY);
            float size = iconSize * zoom * shadowSize * grow / (1f + height / zoom * 1.5f) * (collecting ? Mathf.Clamp01(glowAmount) : 1f);
            shadow.SetPositionAndRotation(new Vector3(position.x, groundY + 0.012f, position.z), Quaternion.Euler(90f, 0f, 0f));
            shadow.localScale = new Vector3(size, size * 0.85f, 1f);
        }
    }

    /// <summary>The storage pickups fly into, and the point on it they make for, when it is on screen and near.</summary>
    bool FindStorage(out Transform target, out Vector3 point)
    {
        target = null;
        point = default;

        InventoryObject inventory = null;
        LevelController level = LevelController.Instance;
        if (level != null && level.zone != null) inventory = level.zone.InventoryPosition;
        if (inventory == null)
        {
            if (foundStorage == null) foundStorage = FindFirstObjectByType<InventoryObject>();
            inventory = foundStorage;
        }

        if (inventory == null || view == null) return false;

        var bounds = new Bounds(inventory.transform.position, Vector3.zero);
        foreach (Renderer r in inventory.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
        point = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.65f, bounds.center.z);

        Vector3 onScreen = view.WorldToViewportPoint(point);
        bool visible = onScreen.z > 0f && onScreen.x > 0.02f && onScreen.x < 0.98f && onScreen.y > 0.02f && onScreen.y < 0.98f;
        if (!visible || Vector3.Distance(restPoint, point) > storageRange) return false;

        target = inventory.transform;
        return true;
    }

    /// <summary>A quick squash-and-stretch of <paramref name="target"/>, always settling back to its own scale.</summary>
    static void Punch(Transform target)
    {
        if (!restScales.TryGetValue(target, out Vector3 rest))
            restScales[target] = rest = target.localScale;

        string id = $"ItemReceiver.Punch.{target.GetInstanceID()}";
        DOTween.Kill(id);
        target.localScale = rest;
        target.DOPunchScale(new Vector3(rest.x * 0.05f, rest.y * 0.09f, rest.z * 0.05f), 0.32f, 6, 0.5f).SetId(id);
    }

    static float OutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
