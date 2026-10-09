using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// How a crop looks growing in a pot: a small field of plants, not one in the middle. Each grow
/// stage comes in a few variants, and each plant spot in the pot picks one of them and a size of
/// its own, so no two plants in a row are quite alike.
///
/// Spots are in metres from the middle of the pot's cell, x east and y north, on the soil's rows
/// (which run east-west), so the plants of pots joined into one bed line up into long rows.
///
/// Built by Last Orders / Farming / Build Crop Sprites.
/// </summary>
[CreateAssetMenu(menuName = "Food Street/Farming/Crop Look")]
public sealed class CropLook : ScriptableObject
{
    [Serializable]
    public struct Stage
    {
        [Tooltip("The same stage drawn a few ways; each plant picks one.")]
        public Sprite[] variants;
    }

    [Tooltip("Seedling first, ripe last.")]
    [SerializeField] Stage[] stages = Array.Empty<Stage>();

    [Tooltip("Where the plants stand, in metres from the middle of the cell: x east, y north.")]
    [SerializeField] Vector2[] spots = { Vector2.zero };

    [Tooltip("How much bigger or smaller than drawn a plant may be, either way.")]
    [Range(0f, 0.4f)]
    [SerializeField] float sizeJitter = 0.1f;

    [Tooltip("How far off its spot a plant may stand, in metres.")]
    [Range(0f, 0.1f)]
    [SerializeField] float spotJitter = 0.02f;

    public int StageCount => stages?.Length ?? 0;
    public int SpotCount => spots?.Length ?? 0;

    /// <summary>Sets every stage's variants and the layout: what the crop builder calls.</summary>
    public void Configure(Sprite[][] stageVariants, Vector2[] plantSpots, float sizeSpread, float spotSpread)
    {
        stages = new Stage[stageVariants.Length];
        for (int i = 0; i < stageVariants.Length; i++) stages[i].variants = (Sprite[])stageVariants[i].Clone();
        spots = (Vector2[])plantSpots.Clone();
        sizeJitter = sizeSpread;
        spotJitter = spotSpread;
    }

    /// <summary>The sprite the plant on <paramref name="spot"/> shows at <paramref name="stage"/>, in a pot seeded <paramref name="seed"/>.</summary>
    public Sprite SpriteFor(int stage, int spot, int seed)
    {
        if (StageCount == 0) return null;
        Sprite[] variants = stages[Mathf.Clamp(stage, 0, StageCount - 1)].variants;
        if (variants == null || variants.Length == 0) return null;

        // The same variant at every stage, so a plant keeps its character as it grows.
        return variants[(int)(Hash(spot, seed, 0) % (uint)variants.Length)];
    }

    /// <summary>How big the plant on <paramref name="spot"/> is drawn: around 1.</summary>
    public float SizeFor(int spot, int seed) => 1f + sizeJitter * (Unit(spot, seed, 1) * 2f - 1f);

    /// <summary>Where the plant on <paramref name="spot"/> stands, in metres from the middle of the cell (x east, y north).</summary>
    public Vector2 PositionFor(int spot, int seed) =>
        spots[spot] + new Vector2(Unit(spot, seed, 2) * 2f - 1f, Unit(spot, seed, 3) * 2f - 1f) * spotJitter;

    public IReadOnlyList<Vector2> Spots => spots;

    static float Unit(int spot, int seed, int salt) => (Hash(spot, seed, salt) & 0xFFFF) / 65535f;

    static uint Hash(int spot, int seed, int salt)
    {
        unchecked
        {
            uint h = (uint)seed * 0x9E3779B1u ^ (uint)(spot + 1) * 0x85EBCA77u ^ (uint)(salt + 7) * 0xC2B2AE3Du;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            h ^= h >> 15;
            return h;
        }
    }
}
