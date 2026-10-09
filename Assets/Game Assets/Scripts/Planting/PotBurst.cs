using UnityEngine;

/// <summary>When in a pot's life a <see cref="PotBurst"/> goes off.</summary>
public enum PotMoment
{
    /// <summary>A seed has just gone in.</summary>
    Planted,

    /// <summary>The ripe crop has just been picked.</summary>
    Harvested,
}

/// <summary>
/// A one-shot particle effect on a plant pot: every particle system under this object plays
/// together whenever the <see cref="PlantPot"/> it belongs to is planted - seeds dropping into
/// the soil, a puff of earth - or harvested - leaves and sparkles thrown up, a ring of gold.
///
/// The particles are authored by the Last Orders / Farming effect builders; this only plays them.
/// </summary>
[DisallowMultipleComponent]
public sealed class PotBurst : MonoBehaviour
{
    [Tooltip("The pot whose planting or harvest sets this off. Found among the parents when left empty.")]
    [SerializeField] PlantPot pot;

    [Tooltip("Which moment in the pot's life plays the burst.")]
    [SerializeField] PotMoment playOn = PotMoment.Planted;

    ParticleSystem[] systems;

    public PotMoment PlayOn
    {
        get => playOn;
        set => playOn = value;
    }

    void Awake()
    {
        systems = GetComponentsInChildren<ParticleSystem>(true);
        if (pot == null) pot = GetComponentInParent<PlantPot>();
    }

    void OnEnable()
    {
        if (pot == null) return;
        if (playOn == PotMoment.Planted) pot.Planted += OnMoment;
        else pot.Harvested += OnMoment;
    }

    void OnDisable()
    {
        if (pot == null) return;
        pot.Planted -= OnMoment;
        pot.Harvested -= OnMoment;
    }

    void OnMoment(PlantPot moment) => Play();

    /// <summary>Plays the burst from the start, cutting short one already running.</summary>
    public void Play()
    {
        if (systems == null) systems = GetComponentsInChildren<ParticleSystem>(true);

        foreach (ParticleSystem system in systems)
        {
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.Play(false);
        }
    }
}
