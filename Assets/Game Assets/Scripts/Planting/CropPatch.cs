using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The plants growing in a pot: one <see cref="CropSprite"/> on each spot of the seed's
/// <see cref="CropLook"/>, every one showing the stage the pot has reached. When the stage changes
/// they spring up one after another rather than all at once.
///
/// The first plant is the template; the others are copies of it and of its shadow, made as a crop
/// needs them. Spots are laid out square to the world, like the soil's rows, whichever way the pot
/// faces. Which variant each plant shows, and how big, is seeded from where the pot stands, so a
/// pot looks the same every time it is planted with the same crop.
/// </summary>
[DisallowMultipleComponent]
public sealed class CropPatch : MonoBehaviour
{
    [Tooltip("The first plant, and the pattern for the rest: each further spot gets a copy of it and of its shadow.")]
    [SerializeField] CropSprite template;

    [Tooltip("The middle of the pot's cell at the soil's crown, in this object's own space.")]
    [SerializeField] Vector3 centre = new Vector3(0.5f, 0.12f, 0.5f);

    [Tooltip("Seconds between one plant and the next springing into a new stage.")]
    [SerializeField] float stagger = 0.045f;

    readonly List<CropSprite> plants = new List<CropSprite>();
    CropLook look;
    int stage = -1;
    int seed;

    void Awake()
    {
        if (template != null && !plants.Contains(template)) plants.Insert(0, template);
    }

    /// <summary>Wires the patch up, for builders that add it from code.</summary>
    public void Configure(CropSprite first, Vector3 middle)
    {
        template = first;
        centre = middle;
    }

    /// <summary>Shows <paramref name="crop"/> at <paramref name="atStage"/> on every spot.</summary>
    public void Show(CropLook crop, int atStage)
    {
        if (crop == null || crop.StageCount == 0 || crop.SpotCount == 0)
        {
            Clear();
            return;
        }

        atStage = Mathf.Clamp(atStage, 0, crop.StageCount - 1);
        if (crop == look && atStage == stage) return;

        look = crop;
        stage = atStage;
        seed = SeedHere();

        Grow(crop.SpotCount);
        Lay();

        for (int i = 0; i < plants.Count; i++)
        {
            CropSprite plant = plants[i];
            if (i >= crop.SpotCount)
            {
                Retire(plant);
                continue;
            }

            plant.gameObject.SetActive(true);
            plant.Size = crop.SizeFor(i, seed);
            plant.Show(crop.SpriteFor(atStage, i, seed), i * stagger + (i % 3) * stagger * 0.4f);
        }
    }

    /// <summary>One plant in the middle showing <paramref name="sprite"/>: for a seed with grow stages but no look.</summary>
    public void ShowSingle(Sprite sprite)
    {
        look = null;
        stage = -1;
        if (template == null) return;

        Grow(1);
        for (int i = 1; i < plants.Count; i++) Retire(plants[i]);

        template.gameObject.SetActive(true);
        template.transform.position = transform.TransformPoint(centre);
        template.Size = 1f;
        template.Show(sprite);
    }

    /// <summary>Nothing growing: every plant cleared at once, so the next planting starts from bare soil.</summary>
    public void Clear()
    {
        look = null;
        stage = -1;
        foreach (CropSprite plant in plants)
            if (plant != null) plant.Show(null);
    }

    /// <summary>A plant not needed by this crop: cleared, so its shadow goes too, and switched off.</summary>
    static void Retire(CropSprite plant)
    {
        plant.Show(null);
        plant.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        // Moved or turned with the pot: back onto the rows.
        if (look != null && transform.hasChanged)
        {
            transform.hasChanged = false;
            Lay();
        }
    }

    /// <summary>Enough plants for <paramref name="count"/> spots, each a copy of the template with a shadow of its own.</summary>
    void Grow(int count)
    {
        if (template == null) return;
        if (plants.Count == 0) plants.Add(template);

        while (plants.Count < count)
        {
            CropSprite copy = Instantiate(template, transform);
            copy.name = $"{template.name} {plants.Count + 1}";
            copy.Show(null);

            Transform blob = template.Shadow;
            if (blob != null)
            {
                Transform blobCopy = Instantiate(blob, transform);
                blobCopy.name = $"{blob.name} {plants.Count + 1}";
                blobCopy.gameObject.SetActive(false);
                copy.SetShadow(blobCopy);
            }

            plants.Add(copy);
        }
    }

    void Lay()
    {
        if (look == null) return;

        Vector3 middle = transform.TransformPoint(centre);
        for (int i = 0; i < look.SpotCount && i < plants.Count; i++)
        {
            Vector2 at = look.PositionFor(i, seed);
            plants[i].transform.position = middle + new Vector3(at.x, 0f, at.y);
        }
    }

    int SeedHere()
    {
        Vector3 middle = transform.TransformPoint(centre);
        unchecked
        {
            return Mathf.RoundToInt(middle.x * 4f) * 73856093 ^ Mathf.RoundToInt(middle.z * 4f) * 19349663;
        }
    }
}
