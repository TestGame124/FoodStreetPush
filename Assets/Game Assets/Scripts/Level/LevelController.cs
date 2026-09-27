using UnityEngine;

/// <summary>
/// Owns the zone and brings it up at boot.
///
/// Nothing here is persisted: the zone is whatever the scene authored, every
/// session.
/// </summary>
public class LevelController : MonoBehaviour
{
    public static LevelController Instance;

    [SerializeField] public Zone zone;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void Initialize()
    {
        zone.Initialize();
    }
}
