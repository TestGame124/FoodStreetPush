using System;
using System.Collections.Generic;
using DG.Tweening;
using LastOrders.Core;
using LastOrders.Data;
using LastOrders.DayCycle;
using LastOrders.Defense;
using LastOrders.Interaction;
using LastOrders.Survivors;
using LastOrders.UI.Popups;
using LastOrders.UI.World;
using UnityEngine;

namespace LastOrders.Housing
{
    /// <summary>
    /// A shelter in the scene: a little hut with a window for each bed. Either an empty site
    /// with nothing built on it yet, or the hut itself.
    ///
    /// Everything about how it looks is authored here and nothing about the rules is. It
    /// registers a <see cref="ShelterUnit"/> with the shelter service and draws what that unit
    /// says - a curtain in the window of every bed that is taken, and that window lit at night
    /// while whoever sleeps there is home. A guard on watch or a medic in the tent is not home,
    /// so their window stays dark. An empty site shows a pad and a build button; buying the
    /// shelter raises it on the spot, with no worker involved.
    ///
    /// Tapping a built hut is where hiring happens: the popup lists who can be taken on and
    /// what each costs, and a hire takes a bed here or, with this one full, in any shelter that
    /// has one.
    ///
    /// It is also where the staff take cover: after dark, and whenever zombies are inside the
    /// walls, they walk to the nearest built hut and in through its door. The guards stay up in
    /// their towers.
    /// </summary>
    public sealed class ShelterHut : MonoBehaviour, ITappable, IInteractableObject
    {
        [Serializable]
        public struct Bed
        {
            [Tooltip("Shown while somebody has this bed - the curtain in its window.")]
            public GameObject occupied;

            [Tooltip("Shown at night while they are home - the window lit.")]
            public GameObject lit;
        }

        [SerializeField] GameLifetimeScope scope;

        [SerializeField] ShelterDefinition definition;

        [Tooltip("Stable identity. Recorded in progression flags, so renaming it forgets the purchase.")]
        [SerializeField] string shelterId = "shelter";

        [Tooltip("Stands built from the start - the shelter the restaurant opens with, never charged " +
                 "for. Otherwise this is an empty site the player can buy one for.")]
        [SerializeField] bool startsBuilt;

        [Header("Parts")]
        [Tooltip("The hut itself. Hidden while the site is empty.")]
        [SerializeField] GameObject builtVisuals;

        [Tooltip("What an empty site looks like: the pad the hut will stand on.")]
        [SerializeField] GameObject siteVisuals;

        [Tooltip("One per bed, in bed order.")]
        [SerializeField] Bed[] beds;

        [Header("Feedback")]
        [Tooltip("The line over the hut when somebody moves in.")]
        [SerializeField] Color movedInColour = new Color(0.55f, 0.95f, 0.55f);

        [SerializeField] Vector3 calloutOffset = new Vector3(0f, 3.4f, 0f);

        [Header("Tapping")]
        [Tooltip("Resized to the hut or to the empty pad, so what the player can tap is what they can see.")]
        [SerializeField] BoxCollider tapCollider;

        [SerializeField] Vector3 builtColliderSize = new Vector3(3.4f, 2.9f, 3.2f);
        [SerializeField] Vector3 siteColliderSize = new Vector3(3.4f, 0.6f, 3.2f);

        [Tooltip("Where the build button floats over an empty site.")]
        [SerializeField] Vector3 buildButtonOffset = new Vector3(0f, 1.3f, 0f);

        [Header("Taking cover")]
        [Tooltip("In front of the door, local space. Where the walk in ends and the walk out starts.")]
        [SerializeField] Vector3 doorOutside = new Vector3(0f, 0f, 2.3f);

        [Tooltip("Just inside the doorway, local space. Somebody who reaches it is inside and out of sight.")]
        [SerializeField] Vector3 doorInside = new Vector3(0f, 0f, 0.35f);

        [Tooltip("Seconds between one person stepping out of the door and the next, so a crowd " +
                 "leaving together comes out one at a time rather than as one body.")]
        [SerializeField] float doorwayGap = 0.8f;

        float doorwayFreeAt = float.NegativeInfinity;

        static readonly List<ShelterHut> all = new List<ShelterHut>();

        readonly HashSet<Survivor> away = new HashSet<Survivor>();

        GameComposition composition;
        ShelterUnit unit;
        ActionPromptView buildButton;

        public string ShelterId => shelterId;
        public ShelterDefinition Definition => definition;
        public ShelterUnit Unit => unit;

        public string DisplayName => definition != null ? definition.displayName : name;

        /// <summary>Standing, rather than an empty site. What was authored until the rules say otherwise.</summary>
        public bool IsBuilt => unit != null ? unit.IsBuilt : startsBuilt;

        public Vector3 DoorOutside => transform.TransformPoint(doorOutside);
        public Vector3 DoorInside => transform.TransformPoint(doorInside);

        /// <summary>A turn at the door on the way out: seconds to wait before stepping through it.
        /// The first to ask goes at once, each after that a moment behind the last.</summary>
        public float ReserveDoorway()
        {
            float now = Time.time;
            float wait = Mathf.Max(0f, doorwayFreeAt - now);

            doorwayFreeAt = now + wait + doorwayGap;
            return wait;
        }

        /// <summary>The built hut nearest to <paramref name="from"/>, or null when none stands yet.</summary>
        public static ShelterHut NearestBuilt(Vector3 from)
        {
            ShelterHut best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < all.Count; i++)
            {
                ShelterHut hut = all[i];
                if (hut == null || !hut.IsBuilt) continue;

                float distance = (hut.DoorOutside - from).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = hut;
            }

            return best;
        }

        void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        void OnDisable() => all.Remove(this);

        void Start()
        {
            if (scope == null) scope = FindFirstObjectByType<GameLifetimeScope>();
            composition = scope != null ? scope.Composition : null;

            if (composition == null)
            {
                Debug.LogError($"[ShelterHut] {name} found no GameLifetimeScope, so nobody can be hired here.", this);
                return;
            }

            if (definition == null)
            {
                Debug.LogError($"[ShelterHut] {name} has no shelter definition.", this);
                return;
            }

            if (beds == null || beds.Length < definition.beds)
            {
                Debug.LogWarning($"[ShelterHut] {name} draws {(beds != null ? beds.Length : 0)} beds but the " +
                                 $"{definition.displayName} has {definition.beds}. The rest are slept in but not shown.", this);
            }

            unit = composition.Shelters.Register(definition, shelterId, transform.position, startsBuilt);
            if (unit == null) return;

            unit.OnChanged += HandleChanged;
            composition.Shelters.OnHired += HandleHired;

            // Whatever it came back as - built or an empty site - show it as it is, with no fanfare.
            Show(animate: false);
        }

        void OnDestroy()
        {
            if (unit == null) return;

            unit.OnChanged -= HandleChanged;
            composition.Shelters.OnHired -= HandleHired;

            HideBuildButton();
            composition.Shelters.Unregister(shelterId);
            unit = null;
        }

        void Update()
        {
            if (unit != null) DrawBeds();
        }

        // --- Drawing the unit -------------------------------------------------------------------

        void Show(bool animate)
        {
            bool built = unit.IsBuilt;

            SetActive(builtVisuals, built);
            SetActive(siteVisuals, !built);

            if (tapCollider != null)
            {
                Vector3 size = built ? builtColliderSize : siteColliderSize;
                tapCollider.size = size;
                tapCollider.center = new Vector3(0f, size.y * 0.5f, 0f);
            }

            if (built) HideBuildButton();
            else ShowBuildButton();

            DrawBeds();

            if (!built || !animate || builtVisuals == null) return;

            // Rises into place rather than blinking in, so the player sees what they bought.
            Transform t = builtVisuals.transform;
            Vector3 scale = t.localScale;

            t.DOKill(true);
            t.localScale = new Vector3(scale.x, scale.y * 0.05f, scale.z);
            t.DOScale(scale, 0.5f).SetEase(Ease.OutBack);
        }

        void DrawBeds()
        {
            if (beds == null) return;

            bool night = composition.Machine.CurrentPhase.IsSiege();
            if (night) CollectAway();

            for (int i = 0; i < beds.Length; i++)
            {
                Survivor resident = unit.IsBuilt ? unit.Resident(i) : null;

                SetActive(beds[i].occupied, resident != null);
                SetActive(beds[i].lit, resident != null && night && resident.IsAvailable && !away.Contains(resident));
            }
        }

        /// <summary>Everyone who is somewhere else tonight: up a tower, or looking after the tent.</summary>
        void CollectAway()
        {
            away.Clear();

            IReadOnlyList<DefenseUnit> posts = composition.Defense.All;
            for (int i = 0; i < posts.Count; i++)
                if (posts[i].Crew != null && posts[i].CrewOnDuty) away.Add(posts[i].Crew);

            if (composition.Hospital.Medic != null) away.Add(composition.Hospital.Medic);
        }

        /// <summary>Raised on building and on every move-in. Only the first is worth a fanfare.</summary>
        void HandleChanged()
        {
            bool wasShowing = builtVisuals != null && builtVisuals.activeSelf;
            Show(animate: !wasShowing);
        }

        void HandleHired(ShelterUnit home, Survivor survivor)
        {
            if (home != unit) return;

            composition.WorldUi.ShowFloatingText(transform.position + calloutOffset, $"{survivor.Name} moved in", movedInColour);
        }

        // --- Building and hiring ------------------------------------------------------------------

        public bool OnTouch() => TapFeedback.Report(OnTapped());

        public string OnTapped()
        {
            if (unit == null) return $"The {DisplayName} is not connected to the game.";

            return OpenPopup() ? null : composition.Shelters.Describe(shelterId);
        }

        void ShowBuildButton()
        {
            if (buildButton != null && buildButton.IsLive) return;

            WorldUiCatalog catalog = composition.WorldUi.Catalog;
            ActionPromptView prefab = catalog != null ? catalog.actionPrompt : null;

            buildButton = composition.WorldUi.ShowButton(prefab, transform, buildButtonOffset, null, () => OpenPopup());
        }

        void HideBuildButton()
        {
            if (buildButton != null && buildButton.IsLive && buildButton.Anchor == transform) buildButton.Release();
            buildButton = null;
        }

        /// <summary>Opens the build or the hire offer, whichever applies. False when there is no popup to open.</summary>
        bool OpenPopup()
        {
            WorldUiCatalog catalog = composition.WorldUi.Catalog;
            ShelterPopupView prefab = catalog != null ? catalog.shelterPopup : null;
            if (prefab == null) return false;

            ShelterPopupView popup = composition.Popups.Show(prefab);
            if (popup == null) return false;

            popup.Bind(this, Offer, Take);
            return true;
        }

        ShelterOffer Offer() =>
            unit.IsBuilt ? composition.Shelters.GetHireOffer(shelterId) : composition.Shelters.GetBuildOffer(shelterId);

        void Take(int option)
        {
            bool done = unit.IsBuilt
                ? composition.Shelters.TryHire(shelterId, option, out string message)
                : composition.Shelters.TryBuild(shelterId, out message);

            TapFeedback.Report(message);

            // A refusal leaves the popup open with the reason on it.
            if (done && composition.Popups.Current is ShelterPopupView open && open.Owner == (object)this)
                composition.Popups.Hide();
        }

        static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }

        /// <summary>Editor wiring, for the setup tool. Keeps the fields private to everything else.</summary>
        public void Configure(GameLifetimeScope lifetimeScope, ShelterDefinition shelter, string id, bool built)
        {
            scope = lifetimeScope;
            definition = shelter;
            shelterId = id;
            startsBuilt = built;
        }

        /// <summary>Editor wiring for the parts, for the prefab generator.</summary>
        public void ConfigureParts(GameObject built, GameObject site, Bed[] bedParts, BoxCollider tap)
        {
            builtVisuals = built;
            siteVisuals = site;
            beds = bedParts;
            tapCollider = tap;
        }
    }
}
