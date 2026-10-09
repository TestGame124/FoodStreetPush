using DG.Tweening;
using UnityEngine;

/// <summary>
/// One plant growing in a plant pot. It is a sprite, not a model: each growing stage was rendered
/// from the game camera's angle (Last Orders / Farming / Build Crop Sprites), so turned to face the
/// camera it reads as the smooth toon model it was drawn from, for the cost of one quad.
///
/// It turns with the camera every frame, sways a little on its base, springs into each new stage
/// with a squash and settle, and keeps a soft shadow on the soil under it, as wide as the stage.
/// A <see cref="CropPatch"/> sets several of them out across a pot.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class CropSprite : MonoBehaviour
{
    [SerializeField] SpriteRenderer sprite;

    [Tooltip("A soft blob lying on the soil under the crop. Sized to each stage.")]
    [SerializeField] Transform shadow;

    [Tooltip("How wide the shadow is, against the crop's width.")]
    [SerializeField] float shadowWidth = 0.7f;

    [Tooltip("How far the crop leans each way as it sways, in degrees.")]
    [SerializeField] float sway = 1.5f;

    [Tooltip("Sways per second, roughly.")]
    [SerializeField] float swaySpeed = 0.25f;

    Transform view;
    float phase;
    float size = 1f;
    Sprite target;
    bool targetSet;

    public Transform Shadow => shadow;

    /// <summary>How big the plant is drawn, against the size it was rendered at.</summary>
    public float Size
    {
        get => size;
        set
        {
            size = Mathf.Max(0.01f, value);
            if (!DOTween.IsTweening(transform)) transform.localScale = Vector3.one * size;
        }
    }

    void Awake()
    {
        if (sprite == null)
            sprite = GetComponent<SpriteRenderer>();

        // Pots side by side should not sway in step.
        phase = Random.value * Mathf.PI * 2f;
    }

    public void SetShadow(Transform blob) => shadow = blob;

    /// <summary>Shows <paramref name="stage"/>, springing into it if it is a new one.</summary>
    public void Show(Sprite stage) => Show(stage, 0f);

    /// <summary>
    /// Shows <paramref name="stage"/> after <paramref name="delay"/> seconds, springing into it if
    /// it is a new one; until then the plant stays as it was. Null clears it at once.
    /// </summary>
    public void Show(Sprite stage, float delay)
    {
        if (sprite == null)
            sprite = GetComponent<SpriteRenderer>();

        if (targetSet && target == stage)
            return;

        target = stage;
        targetSet = true;
        transform.DOKill();

        if (stage == null || delay <= 0f || !isActiveAndEnabled)
        {
            Swap(stage);
            return;
        }

        DOVirtual.DelayedCall(delay, () => Swap(stage), false).SetTarget(transform);
    }

    void Swap(Sprite stage)
    {
        bool changed = sprite.sprite != stage;
        sprite.sprite = stage;
        FitShadow(stage);

        transform.DOKill();
        if (stage == null || !changed)
        {
            transform.localScale = Vector3.one * size;
            return;
        }

        transform.localScale = new Vector3(1.12f, 0.78f, 1f) * size;
        transform.DOScale(Vector3.one * size, 0.45f).SetEase(Ease.OutBack);
    }

    void FitShadow(Sprite stage)
    {
        if (shadow == null)
            return;

        shadow.gameObject.SetActive(stage != null);
        if (stage == null)
            return;

        // A sibling, not a child: the crop turns to the camera and the shadow must lie flat.
        shadow.position = transform.position + Vector3.up * 0.006f;
        float width = Mathf.Clamp(stage.bounds.size.x * size * shadowWidth, 0.1f, 0.6f);
        shadow.localScale = new Vector3(width, width, 1f);
    }

    void LateUpdate()
    {
        if (view == null)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;
            view = cam.transform;
        }

        float lean = Mathf.Sin(Time.time * swaySpeed * Mathf.PI * 2f + phase) * sway;
        transform.rotation = view.rotation * Quaternion.Euler(0f, 0f, lean);

        // Kept under the plant as it is laid out, the shadow is a sibling and does not follow.
        if (shadow != null && shadow.gameObject.activeSelf)
            shadow.position = transform.position + Vector3.up * 0.006f;
    }

    void OnDisable()
    {
        transform.DOKill();
        transform.localScale = Vector3.one * size;

        // A swap still waiting was dropped with the tween: what shows is what is shown.
        if (sprite != null)
            target = sprite.sprite;
        if (shadow != null && sprite != null && sprite.sprite == null)
            shadow.gameObject.SetActive(false);
    }
}
