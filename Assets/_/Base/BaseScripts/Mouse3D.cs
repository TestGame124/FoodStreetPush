using System;
using System.Collections.Generic;
using FoodStreet.Interaction;
using UnityEngine;

/// <summary>
/// Turns a tap on the world into a call on the object under it.
///
/// The class keeps its old name so the component already wired into the scenes, and the touch
/// layer mask serialized on it, survive untouched. Nothing of the old implementation is left:
///
///   - It fired on press, so starting a camera pan on a stove opened the stove.
///   - It read the mouse only, so a pinch to zoom also registered as a tap.
///   - It took the single closest collider, so any scenery sharing the tap layers ate the tap
///     meant for the object behind it.
///   - It asked the collider's own GameObject for the interface, but every interactable prefab
///     here keeps its script on the root and its colliders on child visuals, so the lookup found
///     nothing and the tap vanished without a trace.
///
/// A tap is now a press and a release in roughly the same place, with no second finger involved,
/// that neither began nor ended on the UI. The first object along the ray that accepts it wins;
/// one that declines lets the tap carry on, which is how tapping the floor while a building is
/// being positioned still reaches BuildingGhost and drops the building.
/// </summary>
public class Mouse3D : MonoBehaviour
{
    [Header("Raycast")]
    [Tooltip("Layers a tap can land on.")]
    [SerializeField] LayerMask touchLayerMask = ~0;

    [Tooltip("How far into the scene a tap reaches.")]
    [SerializeField] float maxDistance = 999f;

    [Header("Tap")]
    [Tooltip("How far the pointer may travel between press and release and still count as a tap, " +
             "as a fraction of the shorter screen side. Anything further is a camera drag. A " +
             "fraction rather than raw pixels, so a phone and a desktop behave the same.")]
    [Range(0f, 0.2f)]
    [SerializeField] float tapSlopFraction = 0.02f;

    [Tooltip("Longest press that still counts as a tap, in seconds. Zero removes the limit.")]
    [SerializeField] float tapMaxDuration = 0.75f;

    [Header("Debug")]
    [Tooltip("Logs what each tap hit and what it resolved to, and draws the last hit point.")]
    [SerializeField] bool logInteractions;

    /// <summary>
    /// Raised after every accepted tap on the world, with whether anything took it.
    ///
    /// For UI that should close when the player taps elsewhere - the debris action button.
    /// Listening here rather than to the raw pointer means the same rules apply as for the tap
    /// itself: a camera drag, a pinch or a press on the UI never counts.
    /// </summary>
    public static event Action<bool> WorldTapResolved;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetEvents() => WorldTapResolved = null;

    // Cached so a tap costs no delegate allocation.
    static readonly Func<IInteractableObject, bool> PlayTouch = target => target.OnTouch();
    static readonly Func<IInteractableObjectEditorState, bool> EditTouch = target => target.OnTouch();
    static readonly DistanceComparer NearestFirst = new DistanceComparer();

    Camera view;
    RaycastHit[] hits = new RaycastHit[16];
    Vector3 lastHitPoint;
    bool hasLastHit;

    void Update()
    {
        if (!IsTap()) return;
        if (!TryResolveCamera(out Camera camera)) return;

        Ray ray = camera.ScreenPointToRay(PointerInput.Position);

        bool handled = GameHandler.currentState == GameHandler.GameState.EditState
            ? Dispatch(ray, EditTouch)
            : Dispatch(ray, PlayTouch);

        // Claim the release only once something has actually acted on it. Consuming a tap nobody
        // wanted would block the systems that read the same release later in the frame.
        if (handled) PointerInput.Consume();

        WorldTapResolved?.Invoke(handled);
    }

    /// <summary>
    /// Whether this frame ends a gesture that should be read as a tap on the world.
    /// </summary>
    bool IsTap()
    {
        // On release, not on press. Pressing is also how a camera pan starts, and until the finger
        // lifts there is no way to tell which of the two the player meant.
        if (!PointerInput.Released) return false;

        // Something earlier in the frame already owns this gesture.
        if (PointerInput.Consumed) return false;

        // A pinch is a zoom, never a tap, even once one finger has already lifted.
        if (PointerInput.GestureUsedMultiTouch) return false;

        // A press that began on a button belongs to that button, wherever it happens to end.
        if (PointerInput.PressStartedOverUI) return false;
        if (PointerInput.OverUI) return false;

        if (PointerInput.DragPixels > TapSlopPixels) return false;
        if (tapMaxDuration > 0f && PointerInput.HoldSeconds > tapMaxDuration) return false;

        return true;
    }

    float TapSlopPixels => Mathf.Min(Screen.width, Screen.height) * tapSlopFraction;

    bool TryResolveCamera(out Camera camera)
    {
        // Camera.main is a tagged scene lookup, so it is cached -- but re-fetched whenever the
        // cached one has been destroyed by a scene load.
        if (view == null) view = Camera.main;
        camera = view;
        return camera != null;
    }

    /// <summary>
    /// Offers the tap to each <typeparamref name="T"/> along the ray, nearest first, and stops at
    /// the first one that accepts it.
    /// </summary>
    bool Dispatch<T>(Ray ray, Func<T, bool> touch) where T : class
    {
        int count = CastAll(ray);
        T previous = null;

        for (int i = 0; i < count; i++)
        {
            Collider collider = hits[i].collider;
            if (collider == null) continue;

            // Upwards from the collider, because the collider is rarely the object that owns the
            // behaviour: the stoves, the plant pot and the sign board all keep their script on the
            // prefab root while their colliders sit on child visuals ("Area", "Anchor", "Sign",
            // "Cube"...). Starting from hit.collider rather than hit.transform matters too --
            // hit.transform reports the attached rigidbody's object when there is one.
            T target = collider.GetComponentInParent<T>();

            // One object often puts several colliders in the ray's path; offer it the tap once.
            if (target == null || ReferenceEquals(target, previous)) continue;
            previous = target;

            if (!touch(target))
            {
                if (logInteractions)
                    Debug.Log($"Tap declined by '{Name(target)}' via collider '{collider.name}'.", collider);
                continue;
            }

            lastHitPoint = hits[i].point;
            hasLastHit = true;

            if (logInteractions)
                Debug.Log($"Tap handled by '{Name(target)}' via collider '{collider.name}'.", collider);

            return true;
        }

        if (logInteractions)
            Debug.Log($"Tap passed through {count} collider(s); no {typeof(T).Name} took it.");

        return false;
    }

    /// <summary>
    /// Every collider along the ray, nearest first. Not just the closest one: scenery, the grid
    /// floor and other props share the tap layers, and a single-hit raycast let any of them
    /// swallow a tap meant for the interactable behind. The trade is that a tap can reach an
    /// object standing behind a prop, which in a fixed isometric view is what a player expects.
    /// </summary>
    int CastAll(Ray ray)
    {
        int count = Physics.RaycastNonAlloc(ray, hits, maxDistance, touchLayerMask);

        if (count == hits.Length)
        {
            // The buffer filled exactly, so there may be hits we never saw. Grow once, take them all.
            hits = new RaycastHit[hits.Length * 2];
            count = Physics.RaycastNonAlloc(ray, hits, maxDistance, touchLayerMask);
        }

        // RaycastNonAlloc promises no particular order, so "nearest first" has to be made true
        // rather than assumed.
        Array.Sort(hits, 0, count, NearestFirst);
        return count;
    }

    static string Name<T>(T target) where T : class => (target as Component) != null ? (target as Component).name : typeof(T).Name;

    sealed class DistanceComparer : IComparer<RaycastHit>
    {
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }

    void OnDrawGizmosSelected()
    {
        if (!hasLastHit) return;
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(lastHitPoint, 0.3f);
    }
}
