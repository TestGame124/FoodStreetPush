using System.Collections;
using FoodStreet.Saving;
using UnityEngine;

/// <summary>
/// Owns the zone and decides when its saved state is pushed back into it.
///
/// It no longer runs a save loop of its own. The old one serialised the entire
/// zone every two seconds whether or not anything had changed, on the main
/// thread, and could fire while the level was still loading - overwriting a good
/// save with a half-built world. Writing is now the save system's job, triggered
/// by actual changes.
/// </summary>
public class LevelController : MonoBehaviour
{
    public static LevelController Instance;

    [SerializeField] public Zone zone;

    private bool restored;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        SaveSystem.Unregister(zone);

        if (Instance == this)
            Instance = null;
    }

    public void Initialize()
    {
        zone.Initialize();

        // Registering rather than saving directly: the save system asks the zone
        // for its state when it is about to write, so there is exactly one
        // snapshot per write and never one mid-load.
        SaveSystem.Register(zone);
    }

    /// <summary>
    /// Restores the zone once the build grid has finished spawning everything,
    /// so every pot and station exists and carries its ID.
    /// </summary>
    public void LoadData(bool isLoading)
    {
        if (restored)
            return;

        StartCoroutine(RestoreWhenGridIsReady());
    }

    private IEnumerator RestoreWhenGridIsReady()
    {
        // Read the live flag rather than a bool copied at call time: the caller's
        // copy can never change, so waiting on it was waiting on nothing.
        while (GridBuildingSystem3D.Instance != null && GridBuildingSystem3D.Instance.isLoadingData)
        {
            yield return null;
        }

        // One frame for Start/OnEnable on freshly spawned objects to run, so
        // pots and stations have registered themselves with the zone.
        yield return null;

        ClearAuthoredObjectsThatWereSold();

        restored = true;
        zone.RestoreState(SaveSystem.Data);
    }

    /// <summary>
    /// The grid claims every scene-authored object that still has a save entry
    /// and drops it from the zone's authored list. Whatever is left is something
    /// the player sold in an earlier session, so it must not come back.
    /// </summary>
    private void ClearAuthoredObjectsThatWereSold()
    {
        if (!TutorialController.TutorialCompleted())
            return;

        // Over a copy: demolishing pots and stations unregisters them, which
        // edits lists the zone is holding.
        PlacedObject_Done[] leftovers = zone.GetAlreadyObjectsList().ToArray();

        for (int i = 0; i < leftovers.Length; i++)
        {
            PlacedObject_Done authored = leftovers[i];
            if (authored == null)
                continue;

            if (authored.GetPlacedObjectTypeSO().categoryType == ObjectCategory.Floor)
                authored.DestroySelf();
            else
                GridBuildingSystem3D.Instance.RemoveObject(authored.transform.position);
        }
    }
}
